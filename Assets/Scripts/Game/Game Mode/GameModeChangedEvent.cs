using MFramework.Event;

public class GameModeChangedEvent : IEvent
{
    public readonly E_GameMode newMode;

    public GameModeChangedEvent(E_GameMode newMode)
    {
        this.newMode = newMode;
    }
}
