using MFramework.Event;

public class UIManager : MonoBehaviour,
    IEventReceiver<PanelRequestEvent>,
    IEventReceiver<GameModeChangedEvent>
{
    [Header("根节点与特殊面板引用")]
    [SerializeField] private GameObject fieldUIRoot;
    [SerializeField] private GameObject battleUIRoot;

    [SerializeField] private GameObject mainPanel;

    private readonly Dictionary<Type, PanelController> _panelControllerDict = new();
    private readonly List<PanelController> _allPanelList = new();

    private void Awake()
    {
        _panelControllerDict.Clear();
        _allPanelList.Clear();
        
        GetPanelFromRoot(transform);
    }

    private void OnEnable()
    {
        EventBus.Subscribe<PanelRequestEvent>(this);
        EventBus.Subscribe<GameModeChangedEvent>(this);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<PanelRequestEvent>(this);
        EventBus.Unsubscribe<GameModeChangedEvent>(this);
    }

    private void Update()
    {
        var mode = GameModeManager.Instance.CurrentGameMode;
        var input = InputSystemController.Instance;
        if (mode is E_GameMode.Battle) return;

        if (input.GetUICancelPressed())
            HandleGlobalCancelInput(mode);

        if (input.GetMenuPressed())
            HandleGlobalMenuInput(mode);
    }

    private void HandleGlobalCancelInput(E_GameMode currentMode)
    {
        if (TryHandleCancelByActionPanel())
            return;

        if (IsAnyPanelActive())
        {
            GameModeManager.Instance.RequestChangeMode(E_GameMode.Explore);
            CloseAllPanels();
            return;
        }

        if (currentMode == E_GameMode.InteractionMenu)
            GameModeManager.Instance.RequestChangeMode(E_GameMode.Explore);


    }

    private void HandleGlobalMenuInput(E_GameMode currentMode)
    {
        if (mainPanel.activeInHierarchy)
        {
            CloseAllPanels();
            mainPanel.SetActive(false);
            GameModeManager.Instance.RequestChangeMode(E_GameMode.Explore);
            return;
        }

        if (currentMode == E_GameMode.InteractionMenu)
            return;

        if (IsAnyPanelActive())
            return;
        
        GameModeManager.Instance.RequestChangeMode(E_GameMode.Pause);
        mainPanel.SetActive(true);
    }

    private void GetPanelFromRoot(Transform root)
    {
        PanelController[] panels = root.GetComponentsInChildren<PanelController>(true);
        foreach (PanelController panel in panels)
        {
            _allPanelList.Add(panel);
            if (panel.PanelActionType is null)
                continue;
            _panelControllerDict.Add(panel.PanelActionType, panel);
        }
    }

    private bool TryHandleCancelByActionPanel()
    {
        foreach (PanelController panel in _allPanelList)
        {
            if (!panel.gameObject.activeSelf)
                continue;

            if (panel.HandleCancelInput())
                return true;
            
        }
        return false;
    }

    private bool IsAnyPanelActive()
    {
        foreach (PanelController panel in _allPanelList)
        {
            if(panel.gameObject.activeSelf)
                return true;
        }
        return false;
    }

    private void CloseAllPanels()
    {
        foreach (PanelController panel in _allPanelList)
        {
            panel.gameObject.SetActive(false);
        }
    }

    private void ApplyUIRootMode(E_GameMode mode)
    {
        bool isBattle = mode == E_GameMode.Battle;
        fieldUIRoot.SetActive(!isBattle);
        battleUIRoot.SetActive(isBattle);
    }

    #region 事件函数

    public void OnEvent(PanelRequestEvent evt)
    {
        var panelType = evt.actionBase.GetType();
        _panelControllerDict.TryGetValue(panelType, out var panelController);

        panelController?.gameObject.SetActive(true);
        panelController?.SetupPanel(evt.actionBase);

    }
    
    public void OnEvent(GameModeChangedEvent evt)
    {
        ApplyUIRootMode(evt.newMode);
    }

    #endregion

    
}
