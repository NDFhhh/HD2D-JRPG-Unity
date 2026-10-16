using MFramework.Event;

/// <summary>一段对话开始播放（任务/剧情系统可监听，如"与某 NPC 交谈过"类任务）</summary>
public struct DialogueStartedEvent : IEvent
{
    /// <summary>Yarn 脚本里的节点名</summary>
    public readonly string NodeName;

    public DialogueStartedEvent(string nodeName)
    {
        NodeName = nodeName;
    }
}

/// <summary>一段对话播放完毕（剧情系统"等对话播完再推进"的信号）</summary>
public struct DialogueEndedEvent : IEvent
{
    /// <summary>Yarn 脚本里的节点名</summary>
    public readonly string NodeName;

    public DialogueEndedEvent(string nodeName)
    {
        NodeName = nodeName;
    }
}

/// <summary>
/// 对话触发的自定义事件。Yarn 脚本里用 <c>&lt;&lt;evt key&gt;&gt;</c> 命令广播，
/// 是任务系统（触发任务节点）与剧情系统（推进剧情）的统一挂接点。
/// （对应旧实现的 DialogueNode.OnEnterEventKey）
/// </summary>
public struct DialogueCustomEvent : IEvent
{
    public readonly string Key;

    public DialogueCustomEvent(string key)
    {
        Key = key;
    }
}
