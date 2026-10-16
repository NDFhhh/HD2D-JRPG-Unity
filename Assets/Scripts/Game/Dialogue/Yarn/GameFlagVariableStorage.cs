using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;
using Yarn.Unity;

/// <summary>
/// Yarn 变量 ↔ 本项目 GameFlagManager 的桥接。
/// 这是「换成 Yarn 但现有 Flag 体系零改动」的关键一环。
///
/// 设计要点：
/// - **bool / 整数 float 双向同步到 GameFlagManager**：Yarn 里写
///   <c>&lt;&lt;set $has_sword = true&gt;&gt;</c> 会落成 Flag <c>has_sword = 1</c>，
///   于是现有的 <see cref="FlagCondition"/> 判定、任务进度监听
///   （<see cref="FlagChangedEvent"/>）全部照常工作，旧代码一行都不用动。
/// - **外部系统写的 Flag 也能被 Yarn 读到**：<see cref="TryGetValue{T}"/> 会回落到
///   GameFlagManager，所以任务/剧情/存档系统写进去的 Flag 在 Yarn 里直接用 <c>$key</c> 访问。
/// - **string 只存本地字典**（GameFlagManager 只支持 int）。
/// - GameFlag 里落地的都是 int，变量类型由 Yarn 侧 <c>&lt;&lt;declare&gt;&gt;</c> 决定，
///   读回时按 <c>T</c> 还原成 bool / float。
///
/// 挂载：由 <c>DialogueManager</c> 自动创建并赋给 Yarn DialogueRunner 的 VariableStorage，
/// 不需要手动挂场景。
/// </summary>
public class GameFlagVariableStorage : VariableStorageBehaviour
{
    /// <summary>Yarn 变量名前缀，落成 Flag Key 时要剥掉</summary>
    private const string VariablePrefix = "$";

    private readonly Dictionary<string, bool> _bools = new();
    private readonly Dictionary<string, float> _floats = new();
    private readonly Dictionary<string, string> _strings = new();

    private static string ToFlagKey(string variableName)
        => variableName.StartsWith(VariablePrefix)
            ? variableName.Substring(VariablePrefix.Length)
            : variableName;

    private static GameFlagManager Flags => GameFlagManager.EnsureInstance();

    #region 写入

    public override void SetValue(string variableName, bool boolValue)
    {
        _bools[variableName] = boolValue;
        Flags.Set(ToFlagKey(variableName), boolValue ? 1 : 0);
        NotifyVariableChanged(variableName, boolValue);
    }

    public override void SetValue(string variableName, float floatValue)
    {
        _floats[variableName] = floatValue;

        // 整数值同步为 Flag：让「询问 N 个村民」这类计数型任务目标照常工作。
        // 非整数（如 1.5 倍伤害系数）GameFlagManager 存不了，只留在本地字典。
        if (Mathf.Approximately(floatValue, Mathf.Round(floatValue)))
            Flags.Set(ToFlagKey(variableName), Mathf.RoundToInt(floatValue));

        NotifyVariableChanged(variableName, floatValue);
    }

    public override void SetValue(string variableName, string stringValue)
    {
        string value = stringValue ?? string.Empty;
        _strings[variableName] = value;
        NotifyVariableChanged(variableName, value);
    }

    #endregion

    #region 读取

    /// <summary>
    /// 读取变量。**签名必须与基类完全一致**：
    /// <c>VariableStorageBehaviour.TryGetValue&lt;T&gt;(string, out T)</c>。
    /// 注意这里写的是 <c>out T</c> 而不是 <c>out T?</c> ——
    /// 本工程编译参数未开启 nullable 上下文，泛型上的 <c>T?</c> 会被解析成
    /// <c>Nullable&lt;T&gt;</c>，与基类签名冲突（CS0115 / CS0534 / CS0453）。
    /// </summary>
    public override bool TryGetValue<T>(string variableName, [NotNullWhen(true)] out T result)
    {
        // 1) Yarn 自己写过的值（本次运行内的权威来源）
        object boxed = null;
        if (_bools.TryGetValue(variableName, out bool b))
            boxed = b;
        else if (_floats.TryGetValue(variableName, out float f))
            boxed = f;
        else if (_strings.TryGetValue(variableName, out string s))
            boxed = s;

        if (boxed is T typed)
        {
            result = typed;
            return true;
        }

        // 2) 外部系统（任务 / 剧情 / 存档）写进 GameFlagManager 的值。
        //    注意：必须用 Has 而不是 Get != 0 —— 玩家可能真的把 Flag 设成 0。
        string flagKey = ToFlagKey(variableName);
        if (Flags.Has(flagKey))
        {
            int raw = Flags.Get(flagKey);
            if (typeof(T) == typeof(bool))
            {
                result = (T)(object)(raw != 0);
                return true;
            }
            if (typeof(T) == typeof(float))
            {
                result = (T)(object)(float)raw;
                return true;
            }
        }

        // 3) 回落到 Yarn 脚本 <<declare>> 声明的初始值
        if (Program is not null && Program.TryGetInitialValue<T>(variableName, out result))
            return true;

        result = default;
        return false;
    }

    public override bool Contains(string variableName)
        => _bools.ContainsKey(variableName)
        || _floats.ContainsKey(variableName)
        || _strings.ContainsKey(variableName)
        || Flags.Has(ToFlagKey(variableName));

    /// <summary>
    /// 清空 Yarn 侧变量。**刻意不动 GameFlagManager** ——
    /// Flag 是跨系统共享且要进存档的持久状态，Yarn 的一次 Clear 不该抹掉任务进度。
    /// </summary>
    public override void Clear()
    {
        _bools.Clear();
        _floats.Clear();
        _strings.Clear();
    }

    #endregion

    #region 批量存取（存档接口）

    public override (Dictionary<string, float> FloatVariables, Dictionary<string, string> StringVariables, Dictionary<string, bool> BoolVariables) GetAllVariables()
    {
        var floats = new Dictionary<string, float>(_floats);
        var strings = new Dictionary<string, string>(_strings);
        var bools = new Dictionary<string, bool>(_bools);

        // 把 GameFlagManager 里的 Flag 也一并导出，保证存档完整。
        // 本地已有更精确的值时不覆盖（float 可能不是整数，转 int 会丢精度）。
        foreach (var pair in Flags.ExportFlags())
        {
            string key = VariablePrefix + pair.Key;
            if (floats.ContainsKey(key) || strings.ContainsKey(key) || bools.ContainsKey(key))
                continue;
            floats[key] = pair.Value;
        }

        return (floats, strings, bools);
    }

    public override void SetAllVariables(
        Dictionary<string, float> floats,
        Dictionary<string, string> strings,
        Dictionary<string, bool> bools,
        bool clear = true)
    {
        if (clear)
            Clear();

        foreach (var pair in floats)
            SetValue(pair.Key, pair.Value);
        foreach (var pair in strings)
            SetValue(pair.Key, pair.Value);
        foreach (var pair in bools)
            SetValue(pair.Key, pair.Value);
    }

    #endregion
}
