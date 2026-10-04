using MFramework.Event;

public class InputSystemController : Singleton<InputSystemController>, IEventReceiver<GameModeChangedEvent>
{
    private CharacteInputActions _inputActions;

    public CharacteInputActions InputActions => _inputActions;

    private bool _isInitialized = false;
    
    private E_ActiveMap _currentMap;
    
    protected override void Awake()
    {
        base.Awake();
        
        if (!_isInitialized)
        {
            _inputActions ??= new CharacteInputActions();
            _isInitialized = true;
        }
    }

    void OnEnable()
    {
        EventBus.Subscribe<GameModeChangedEvent>(this);
        _inputActions.Player.Enable();
    }

    void OnDisable()
    {
        EventBus.Unsubscribe<GameModeChangedEvent>(this);
        _inputActions.Player.Disable();
    }

    void OnDestroy()
    {
        _inputActions.Dispose();
    }

    public Vector2 GetMovementInput()
    {
        if (!_isInitialized || _currentMap != E_ActiveMap.Player)
            return Vector2.zero;
        
        return _inputActions.Player.Move.ReadValue<Vector2>();
    }

    public bool GetPlayerConfirmPressed()
    {
        if(!_isInitialized || _currentMap != E_ActiveMap.Player)
            return false;
        
        return _inputActions.Player.Confirm.WasPressedThisFrame();
    }
    
    public bool GetMenuPressed()
    {
        if(!_isInitialized)
            return false;
        
        return _inputActions.Player.Menu.WasPressedThisFrame() || _inputActions.UI.Menu.WasPressedThisFrame();
    }

    public bool GetUICancelPressed()
    {
        if(!_isInitialized || _currentMap != E_ActiveMap.UI)
            return false;
        
        return _inputActions.UI.Cancel.WasPressedThisFrame();
    }

    public Vector2 GetNavigateInput()
    {
        if (!_isInitialized || _currentMap != E_ActiveMap.UI)
            return Vector2.zero;
        
        return _inputActions.UI.Navigate.ReadValue<Vector2>();
    }

    public bool GetUISubmitPressed()
    {
        if (!_isInitialized || _currentMap != E_ActiveMap.UI)
            return false;
        return _inputActions.UI.Submit.WasPressedThisFrame();
    }

    public int GetBoostDelta()
    {
        if (!_isInitialized || _currentMap != E_ActiveMap.UI)
            return 0;

        if (!_inputActions.UI.Boost.WasPressedThisFrame())
            return 0;
        
        var value = _inputActions.UI.Boost.ReadValue<float>();
        if (value > 0.5f) return 1;
        if (value < 0.5f) return -1;
        return 0;
    }

    #region 事件实现
    
    public void OnEvent(GameModeChangedEvent evt)
    {
        _currentMap = GetMapFromGameMode(evt.newMode);

        switch (_currentMap)
        {
            case E_ActiveMap.Player:
                _inputActions.Player.Enable();
                _inputActions.UI.Disable();
                break;
            case E_ActiveMap.UI:
                _inputActions.UI.Enable();
                _inputActions.Player.Disable();
                break;

            case E_ActiveMap.None:
                break;

        }
    }

    private E_ActiveMap GetMapFromGameMode(E_GameMode gameMode)
    {
        switch (gameMode)
        {
            case E_GameMode.InteractionMenu: 
            case E_GameMode.Battle:
            case E_GameMode.Pause:
                return E_ActiveMap.UI;
                
            case E_GameMode.Explore:
            default:   
                return E_ActiveMap.Player;
        }
    }

    #endregion
    
    
}
