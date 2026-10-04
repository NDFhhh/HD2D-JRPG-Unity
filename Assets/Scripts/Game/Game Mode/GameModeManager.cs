
public class GameModeManager : Singleton<GameModeManager>
{
    public E_GameMode CurrentGameMode;
    
    [SerializeField] private E_GameMode defaultGameMode = E_GameMode.Explore;

    protected override void Awake()
    {
        base.Awake();
        CurrentGameMode = defaultGameMode;
        Application.targetFrameRate = 60;
    }

    private void Start()
    {
        // 广播当前游戏模式
        AppleMode(CurrentGameMode);
    }

    /// <summary>
    /// 外部请求入口
    /// </summary>
    /// <param name="newMode"></param>
    public void RequestChangeMode(E_GameMode newMode)
    {
        if (Instance != this)
            return;

        if (!CanSwitchMode(newMode))
            return;

        AppleMode(newMode);
    }

    /// <summary>
    /// 模式切换闸门
    /// 关键规则：当战斗会话仍在运行时，禁止 Battle -> Explore 直切
    /// 正常离场路径应通过 SceneLoadManager
    /// Battle -> InteractionMenu -> 场景切换 -> Explore
    /// </summary>
    private bool CanSwitchMode(E_GameMode newMode)
    {
        if (CurrentGameMode != E_GameMode.Battle || CurrentGameMode != E_GameMode.Explore)
            return true;

        return false;
    }
    
    private void AppleMode(E_GameMode newMode)
    {
        CurrentGameMode = newMode;
        EventBus.Publish(new GameModeChangedEvent(CurrentGameMode));
    }
    
}
