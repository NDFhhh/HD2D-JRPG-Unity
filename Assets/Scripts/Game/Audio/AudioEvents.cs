using MFramework.Event;

/// <summary>
/// 漫游脚步事件：由 PlayerController 按步频节拍发布，音频系统订阅播放脚步声
/// </summary>
public readonly struct FootstepEvent : IEvent
{
    public readonly Vector3 Position;

    public FootstepEvent(Vector3 position)
    {
        Position = position;
    }
}
