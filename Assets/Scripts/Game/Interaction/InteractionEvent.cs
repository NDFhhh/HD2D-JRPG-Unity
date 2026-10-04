using MFramework.Event;

public readonly struct InteractionChangedEvent : IEvent
{
    public readonly InteractionBase target;
    public readonly bool inRange;

    /// <summary>
    /// 交互提示变化事件
    /// </summary>
    /// <param name="target"></param>
    /// <param name="inRange">inRange=false 或无可以用命令:隐藏头顶 icon + 关闭菜单</param>
    public InteractionChangedEvent(InteractionBase target, bool inRange)
    {
        this.target = target;
        this.inRange = inRange;
    }
}

public readonly struct InteractionMenuRequestEvent : IEvent
{
    public readonly InteractionBase target;
    
    public InteractionMenuRequestEvent(InteractionBase target)
    {
        this.target = target;
    }
}

public readonly struct InteractionRefreshEvent : IEvent
{
    public readonly InteractionBase target;

    public InteractionRefreshEvent(InteractionBase target)
    {
        this.target = target;
    }
}

