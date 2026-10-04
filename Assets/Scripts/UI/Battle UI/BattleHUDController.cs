using MFramework.Event;

public class BattleHUDController : MonoBehaviour,
    IEventReceiver<BattleStartedEvent>,
    IEventReceiver<GameModeChangedEvent>,
    IEventReceiver<BattleResultViewEnterEvent>
{
    [Header("HUD Panels")] 
    [SerializeField] private GameObject ctbPanel;
    [SerializeField] private GameObject healthBarPanel;

    private void OnEnable()
    {
        EventBus.Subscribe<BattleStartedEvent>(this);
        EventBus.Subscribe<GameModeChangedEvent>(this);
        EventBus.Subscribe<BattleResultViewEnterEvent>(this);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<BattleStartedEvent>(this);
        EventBus.Unsubscribe<GameModeChangedEvent>(this);
        EventBus.Unsubscribe<BattleResultViewEnterEvent>(this);
    }

    private void SetHUDVisible(bool visible)
    {
        ctbPanel.SetActive(visible);
        healthBarPanel.SetActive(visible);
    }

    public void OnEvent(BattleStartedEvent evt)
    {
        SetHUDVisible(true);
    }

    public void OnEvent(GameModeChangedEvent evt)
    {
        SetHUDVisible(false);
    }

    public void OnEvent(BattleResultViewEnterEvent evt)
    {
        SetHUDVisible(false);
    }
}
