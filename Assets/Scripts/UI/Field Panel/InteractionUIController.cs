using MFramework.Event;
using UnityEngine.EventSystems;
using UnityEngine.Pool;
using UnityEngine.UI;

public class InteractionUIController : MonoBehaviour , 
    IEventReceiver<InteractionChangedEvent>,
    IEventReceiver<InteractionMenuRequestEvent>,
    IEventReceiver<GameModeChangedEvent>
{
    [Header("Head Icon")] 
    [SerializeField] private RectTransform actionIconHolder;
    [SerializeField] private GameObject actionIconPrefab;

    [Header("Menu Button")]
    [SerializeField] private RectTransform actionMenuHolder;
    [SerializeField] private GameObject actionMenuButtonPrefab;
    
    private ObjectPool<GameObject> _iconPool;
    private readonly List<GameObject> _activeIcons = new(8);
    
    private ObjectPool<GameObject> _menuButtonPool;
    private readonly List<GameObject> _activeButtons = new(8);
    
    private IReadOnlyList<ActionCommandInfo> _currentCommandList;
    
    private Transform _headAnchor;

    private InteractionBase _target;

    private void Awake()
    {
        InitPool();
        actionIconHolder.gameObject.SetActive(false);
        actionMenuHolder.gameObject.SetActive(false);
    }
    
    private void OnEnable()
    {
        EventBus.Subscribe<InteractionChangedEvent>(this);
        EventBus.Subscribe<InteractionMenuRequestEvent>(this);
        EventBus.Subscribe<GameModeChangedEvent>(this);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<InteractionChangedEvent>(this);
        EventBus.Unsubscribe<InteractionMenuRequestEvent>(this);
        EventBus.Unsubscribe<GameModeChangedEvent>(this);
    }

    private void LateUpdate()
    {
        if (!actionIconHolder.gameObject.activeSelf || _headAnchor is null)
            return;

        UpdateHeadIconPosition();
    }

    private void UpdateHeadIconPosition()
    {
        if (_target is null || !_target.isActiveAndEnabled)
        {
            HideHeadIcon();
            return;
        }

        Vector3 worldPos = _headAnchor.position;
        Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(transform as RectTransform, screenPos, null,
                out Vector2 localPoint))
            actionIconHolder.anchoredPosition = localPoint;
    }

    #region 对象池相关方法
    
    private void InitPool()
    {
        _iconPool = new ObjectPool<GameObject>
        (
            createFunc: () => Instantiate(actionIconPrefab, actionIconHolder),
            actionOnGet: (obj) =>
            {
                obj.SetActive(true);
                obj.transform.SetAsLastSibling();
                
            },
            actionOnRelease: (obj) => obj.SetActive(false),
            actionOnDestroy: (obj) => Destroy(obj),
            defaultCapacity : 8,
            maxSize : 20
        );
        
        _menuButtonPool = new ObjectPool<GameObject>
        (
            createFunc: () => Instantiate(actionMenuButtonPrefab, actionMenuHolder),
            actionOnGet: (obj) =>
            {
                obj.SetActive(true);
                obj.transform.SetAsLastSibling();
            },
            actionOnRelease: (obj) => obj.SetActive(false),
            actionOnDestroy: (obj) => Destroy(obj),
            defaultCapacity : 8,
            maxSize : 20
        );
    }

    private void SyncPool(List<GameObject> activeList, ObjectPool<GameObject> pool, int targetCount)
    {
        while (activeList.Count > targetCount)
        {
            int lastIndex = activeList.Count - 1;
            GameObject item = activeList[lastIndex];
            pool.Release(item);
            activeList.RemoveAt(lastIndex);
        }

        while (activeList.Count < targetCount)
        {
            GameObject item = pool.Get();
            activeList.Add(item);
        }
    }

    private void ReleaseAll(List<GameObject> activeList, ObjectPool<GameObject> pool)
    {
        for (int i = 0; i < activeList.Count; i++)
        {
            pool.Release(activeList[i]);
        }
        activeList.Clear();
    }

    #endregion

    #region 事件相关方法

    /// <summary>
    /// 启动头顶Icon
    /// </summary>
    /// <param name="evt"></param>
    public void OnEvent(InteractionChangedEvent evt)
    {
        _target = evt.target;
        
        if (!evt.inRange || evt.target is null)
        {
            // 关闭显示头顶Icon
            HideHeadIcon();
            _currentCommandList = null;
            _target = null;
            return;
        }
        // 启动显示头顶Icon
        _currentCommandList = evt.target.cachedCommandInfos;
        _headAnchor = evt.target.HeadAnchor;
        
        ShowHeadIcons();
    }

    /// <summary>
    /// 启动菜单
    /// </summary>
    /// <param name="evt"></param>
    /// <exception cref="NotImplementedException"></exception>
    public void OnEvent(InteractionMenuRequestEvent evt)
    {
        // 关闭显示头顶Icon
        HideHeadIcon();
        
        actionMenuHolder.gameObject.SetActive(true);
        
        OpenMenu(evt.target);
    }
    
    public void OnEvent(GameModeChangedEvent evt)
    {
        if (evt.newMode == E_GameMode.InteractionMenu)
            return;
        
        if(actionMenuHolder.gameObject.activeSelf)
            HideActionMenu();

        if (evt.newMode == E_GameMode.Explore)
        {
            if (_currentCommandList is not null && _currentCommandList.Count > 0)
                ShowHeadIcons();
            return;
        }
        
        HideHeadIcon();
    }
    
    #endregion

    private void ShowHeadIcons()
    {
        if (_currentCommandList.Count == 0)
            return;

        actionIconHolder.gameObject.SetActive(true);

        SyncPool(_activeIcons, _iconPool, _currentCommandList.Count);

        for (int i = 0; i < _activeIcons.Count; i++)
        {
            var obj = _activeIcons[i];
            var cmd = _currentCommandList[i];

            obj.GetComponent<Image>().sprite = cmd.Icon;
        }
    }
    
    private void OpenMenu(InteractionBase target)
    {
        // 通知GameMode切换
        GameModeManager.Instance.RequestChangeMode(E_GameMode.InteractionMenu);
        
        // 同步对象池，确保活动按钮数量与当前命令列表数量匹配
        SyncPool(_activeButtons, _menuButtonPool, _currentCommandList.Count);

        Button firstBtn = null; 
        for (int i = 0; i < _activeButtons.Count; i++)
        {
            var btn = _activeButtons[i];
            var cmd = _currentCommandList[i];

            int commandIndex = i;
            btn.GetComponent<ActionMenuButton>().SetButton(cmd, () =>
            {
                target.ExecuteCommandFromUI(commandIndex);
                CloseMenu(false);
            });

            if (firstBtn is null)
                firstBtn = btn.GetComponent<Button>();
        }

        if (firstBtn is not null)
        {
            firstBtn.Select();
            EventSystem.current.SetSelectedGameObject(firstBtn.gameObject);
        }
    }

    private void CloseMenu(bool restoreHeadIcons)
    {
        HideActionMenu();
        
        if (restoreHeadIcons)
            ShowHeadIcons();
        else
            HideHeadIcon();
    }

    private void HideHeadIcon()
    {
        actionIconHolder?.gameObject.SetActive(false);
        ReleaseAll(_activeIcons, _iconPool);    
    }

    private void HideActionMenu()
    {
        actionMenuHolder?.gameObject.SetActive(false);
        ReleaseAll(_activeButtons, _menuButtonPool);
    }

    
}
