using MFramework.Event;
using UnityEngine.SceneManagement;

public readonly struct SceneLoadCompleteEvent : IEvent
{
    public readonly Scene LoadedScene;
    public readonly E_GameMode ModeAfterLoad;

    public SceneLoadCompleteEvent(Scene scene, E_GameMode mode)
    {
        LoadedScene = scene;
        ModeAfterLoad = mode;
    }
}
