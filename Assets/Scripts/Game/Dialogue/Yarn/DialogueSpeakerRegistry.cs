using System.Collections.Generic;
using UnityEngine;

// 注意：本文件刻意**不使用可空引用类型注解（`?`）**。
// 本工程 Assembly-CSharp 的编译参数里没有 -nullable，且全工程只有本目录新加的文件
// 是后来引入 Yarn 才出现的，没有开启 nullable 的先例。写了 `SomeType?` 会被判为
// CS8632 警告（如果顺手加了 `#nullable enable`，泛型上的 `T?` 又会被解析成
// Nullable<T> 而与基类签名冲突，报 CS0115/CS0534/CS0453）。
// 因此这里统一走「不用注解」的写法。

/// <summary>
/// 说话人注册表：把 Yarn 脚本里的说话人标识（<c>LocalizedLine.CharacterName</c>）
/// 映射回项目的 <see cref="Speaker_SO"/>，从而拿回姓名颜色、立绘、打字机音效。
///
/// 用法：Yarn 里写
/// <code>
/// VillageElder: 欢迎来到晨风村。
/// </code>
/// → <c>line.CharacterName == "VillageElder"</c> → 查到 <c>Speaker_VillageElder.asset</c>。
///
/// <see cref="Speaker_SO.Id"/> 留空时回落到资产文件名（如 <c>Speaker_Traveller</c>）。
///
/// <para><b>加载方式（重要）：</b>
/// Speaker 资产通常不被任何场景/预制体引用（对话播放时才用得上），所以它们
/// <b>永远不会被自动加载进内存</b>。早期实现只用
/// <c>Resources.FindObjectsOfTypeAll&lt;Speaker_SO&gt;()</c> 扫描「已加载对象」，
/// 结果在重启编辑器后表是空的 → 姓名牌和立绘一起消失（表现为「立绘丢失」）。
/// 现在改为以 <c>Resources.LoadAll</c> 为主：把 Speaker 资产放在任意
/// <c>Assets/Resources/</c> 子目录下即可被找到（当前在
/// <c>Assets/Resources/DialogueSpeakers/</c>），打包后同样有效。</para>
/// </summary>
public static class DialogueSpeakerRegistry
{
    private static Dictionary<string, Speaker_SO> _map;

    /// <summary>已经告警过的 Id，避免每行对话刷一条日志</summary>
    private static readonly HashSet<string> _warned = new();

    /// <summary>失效缓存（编辑器里改了 Speaker 资产后调用；通常重进 Play 即可）</summary>
    public static void Invalidate()
    {
        _map = null;
        _warned.Clear();
    }

    public static Speaker_SO Get(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        EnsureBuilt();

        if (_map.TryGetValue(id, out Speaker_SO so))
            return so;

        // 查不到时给一条能直接定位问题的日志：大多数「立绘/姓名牌不显示」都是这里没命中。
        if (_warned.Add(id))
        {
            Debug.LogWarning($"[DialogueSpeaker] 未找到 Id=\"{id}\" 的 Speaker_SO。" +
                             $"请确认它有同名 Id，且放在 Assets/Resources 下（当前已注册：{DescribeKeys()}）。");
        }
        return null;
    }

    private static string DescribeKeys()
    {
        if (_map is null || _map.Count == 0)
            return "<空>";

        var builder = new System.Text.StringBuilder();
        foreach (string key in _map.Keys)
        {
            if (builder.Length > 0)
                builder.Append(", ");
            builder.Append(key);
        }
        return builder.ToString();
    }

    private static void EnsureBuilt()
    {
        if (_map is not null)
            return;

        _map = new Dictionary<string, Speaker_SO>();

        // 1) 主路径：Resources 下的全部 Speaker（放哪个子目录都行，打包后依然有效）
        Speaker_SO[] fromResources = Resources.LoadAll<Speaker_SO>(string.Empty);
        for (int i = 0; i < fromResources.Length; i++)
            Register(fromResources[i]);

        // 2) 补充：内存里已加载的（万一有人把 Speaker 直接挂到场景/资产上）
        Speaker_SO[] loaded = Resources.FindObjectsOfTypeAll<Speaker_SO>();
        for (int i = 0; i < loaded.Length; i++)
            Register(loaded[i]);
    }

    private static void Register(Speaker_SO so)
    {
        if (so is null)
            return;

        string key = string.IsNullOrEmpty(so.Id) ? so.name : so.Id;
        if (string.IsNullOrEmpty(key))
            return;

        // 先到先得：Id 显式重复时保留第一个，避免随机覆盖
        if (!_map.ContainsKey(key))
            _map[key] = so;
    }
}
