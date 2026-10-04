namespace MFramework.Event;

/// <summary>
/// 接收者
/// </summary>
/// <typeparam name="TEvent">事件类型，必须实现IEvent接口</typeparam>
public interface IEventReceiver<TEvent> where TEvent : IEvent
{
    void OnEvent(TEvent evt);
}
