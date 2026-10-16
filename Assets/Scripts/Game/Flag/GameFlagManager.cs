using MFramework.Event;

/// <summary>
/// 全局游戏变量（Flag）系统。
/// 对话 / 任务 / 剧情三个系统共用的"状态黑板"：
/// 对话选项写 Flag → 任务条件读 Flag → 剧情分支判 Flag。
/// 所有变更通过 <see cref="FlagChangedEvent"/> 广播，其他系统零引用接入。
/// </summary>
public class GameFlagManager : Singleton<GameFlagManager>
{
    private readonly Dictionary<string, int> _flags = new();

    /// <summary>确保全局唯一实例存在（DialogueManager 启动时会自动调用）</summary>
    public static GameFlagManager EnsureInstance()
    {
        if (Instance is not null)
            return Instance;

        var go = new GameObject($"[{nameof(GameFlagManager)}]");
        DontDestroyOnLoad(go);
        return go.AddComponent<GameFlagManager>();
    }

    protected override void Awake()
    {
        base.Awake();
        if (Instance == this)
            DontDestroyOnLoad(gameObject);
    }

    /// <summary>读取 Flag，未设置过的 Key 一律返回 0</summary>
    public int Get(string key)
    {
        if (string.IsNullOrEmpty(key))
            return 0;
        return _flags.TryGetValue(key, out int value) ? value : 0;
    }

    public bool IsTrue(string key) => Get(key) != 0;

    /// <summary>
    /// 该 Key 是否被显式设置过（区别于"从未设置 → Get 返回 0"）。
    /// Yarn 变量桥接靠它区分「Flag 里真有值」和「默认 0」，
    /// 否则默认值会盖掉 Yarn 脚本 &lt;&lt;declare&gt;&gt; 声明的初始值。
    /// </summary>
    public bool Has(string key) => !string.IsNullOrEmpty(key) && _flags.ContainsKey(key);

    public void Set(string key, int value)
    {
        if (string.IsNullOrEmpty(key))
            return;

        int oldValue = Get(key);
        _flags[key] = value;

        if (oldValue != value)
            EventBus.Publish(new FlagChangedEvent(key, oldValue, value));
    }

    public void Add(string key, int delta) => Set(key, Get(key) + delta);

    /// <summary>0/1 翻转，常用于布尔开关</summary>
    public void Toggle(string key) => Set(key, Get(key) == 0 ? 1 : 0);

    #region 存档接口

    /// <summary>导出全部 Flag（接入存档系统时序列化此字典即可）</summary>
    public Dictionary<string, int> ExportFlags() => new(_flags);

    /// <summary>从存档恢复全部 Flag</summary>
    public void ImportFlags(Dictionary<string, int> data)
    {
        _flags.Clear();
        if (data is null)
            return;
        foreach (var pair in data)
            _flags[pair.Key] = pair.Value;
    }

    #endregion
}

/// <summary>某个全局 Flag 发生变化时广播（任务系统进度的主要监听源）</summary>
public struct FlagChangedEvent : IEvent
{
    public readonly string Key;
    public readonly int OldValue;
    public readonly int NewValue;

    public FlagChangedEvent(string key, int oldValue, int newValue)
    {
        Key = key;
        OldValue = oldValue;
        NewValue = newValue;
    }
}
