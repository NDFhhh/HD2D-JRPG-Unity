using MFramework.Event;

/// <summary>一段对话开始播放（任务/剧情系统可监听，如"与某 NPC 交谈过"类任务）</summary>
public struct DialogueStartedEvent : IEvent
{
    public readonly DialogueAsset_SO Dialogue;

    public DialogueStartedEvent(DialogueAsset_SO dialogue)
    {
        Dialogue = dialogue;
    }
}

/// <summary>一段对话播放完毕（剧情系统"等对话播完再推进"的信号）</summary>
public struct DialogueEndedEvent : IEvent
{
    public readonly DialogueAsset_SO Dialogue;

    public DialogueEndedEvent(DialogueAsset_SO dialogue)
    {
        Dialogue = dialogue;
    }
}

/// <summary>
/// 对话节点 OnEnterEventKey 触发的自定义事件。
/// 是后续任务系统（触发任务节点）与剧情系统（推进剧情）的统一挂接点。
/// </summary>
public struct DialogueCustomEvent : IEvent
{
    public readonly string Key;

    public DialogueCustomEvent(string key)
    {
        Key = key;
    }
}
