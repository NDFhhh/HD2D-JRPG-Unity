using MFramework.Event;

public class CameraModeController : MonoBehaviour,
    IEventReceiver<GameModeChangedEvent>,
    IEventReceiver<BattleEndedEvent>
{
    [SerializeField] private GameObject followCamera;
    [SerializeField] private GameObject battleCamera;
    [SerializeField] private GameObject battleResultCameraRoot;

    private void OnEnable()
    {
        EventBus.Subscribe<GameModeChangedEvent>(this);
        EventBus.Subscribe<BattleEndedEvent>(this);
    }
    
    private void OnDisable()
    {
        EventBus.Unsubscribe<GameModeChangedEvent>(this);
        EventBus.Unsubscribe<BattleEndedEvent>(this);
    }

    private void SetCameraView(E_CameraView view)
    {
        followCamera.SetActive(view == E_CameraView.Explore);
        battleCamera.SetActive(view == E_CameraView.Battle);
        battleResultCameraRoot.SetActive(view == E_CameraView.BattleResult);
    }
    
    public void OnEvent(GameModeChangedEvent evt)
    {
        switch (evt.newMode)
        {
            case E_GameMode.Explore:
                SetCameraView(E_CameraView.Explore);
                break;
            case E_GameMode.Battle:
                SetCameraView(E_CameraView.Battle);
                break;
        }
    }

    public void OnEvent(BattleEndedEvent evt)
    {
        SetCameraView(E_CameraView.BattleResult);
        
        if (!evt.IsWin)
        {
            //SetCameraView(E_CameraView.Explore);
            EventBus.Publish(new BattleLoseViewEnterEvent());
            return;
        }

        EventBus.Publish(new BattleResultViewEnterEvent(evt.ExpReward, evt.MoneyReward, evt.DropRewards));
    }
}
