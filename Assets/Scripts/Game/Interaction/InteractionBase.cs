
using MFramework.Event;

public class InteractionBase : MonoBehaviour,
    IEventReceiver<InteractionRefreshEvent> 
{
    [Header("Sign Trans")] 
    public Transform HeadAnchor;
    
    private AllyDefinition_SO _currentInteractor;
    private ActionBase[] _actionsCache;

    private readonly List<ActionCommandInfo> _cachedCommandInfos = new(8);
    public IReadOnlyList<ActionCommandInfo> cachedCommandInfos => _cachedCommandInfos;
    
    private readonly List<VisibleActionEntry> _visibleEntries = new(8);

    private struct VisibleActionEntry
    {
        public ActionBase Action;
        public ActionCommandInfo CommandInfo;
    }
    
    private void Awake()
    {
        CacheActions();
        HeadAnchor = transform.GetChild(0);
    }

    private void OnEnable()
    {
        EventBus.Subscribe<InteractionRefreshEvent>(this);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<InteractionRefreshEvent>(this);
        _currentInteractor = null;
        _cachedCommandInfos.Clear();
        _visibleEntries.Clear();
    }

    public void Interact(AllyDefinition_SO interactor)
    {
        EventBus.Publish(new InteractionMenuRequestEvent(this));
    }
    
    public void OnFocus(AllyDefinition_SO interactor)
    {
        CacheActions();
        _currentInteractor = interactor;
        
        RebuildCommands();
        PublishEvent(true);
    }
    
    public void OnLostFocus(AllyDefinition_SO interactor)
    {
        _currentInteractor = null;
        _cachedCommandInfos.Clear();
        
        PublishEvent(false);
        HeadAnchor.gameObject.SetActive(true);
    }

    private void CacheActions() => _actionsCache = GetComponents<ActionBase>();

    private void RebuildCommands()
    {
        _cachedCommandInfos.Clear();
        _visibleEntries.Clear();

        for (int i = 0; i < _actionsCache.Length; i++)
        {
            ActionBase action = _actionsCache[i];

            if (!CanAnyPartyMemberExecute(action))
                continue;

            _visibleEntries.Add(new VisibleActionEntry
            {
                Action = action,
                CommandInfo = action.CommandInfo
            });
        }
        
        if(_visibleEntries.Count > 1)
            _visibleEntries.Sort((a,b)=>a.CommandInfo.Order.CompareTo(b.CommandInfo.Order));

        for (int i = 0; i < _visibleEntries.Count; i++)
        {
            _cachedCommandInfos.Add(_visibleEntries[i].CommandInfo);
        }

        if (_visibleEntries.Count > 0)
        {
            HeadAnchor.gameObject.SetActive(false);
        }
    }

    private void PublishEvent(bool inRange)
    {
        EventBus.Publish(new InteractionChangedEvent(this,inRange));
    }

    private bool CanAnyPartyMemberExecute(ActionBase actionBase)
    {
        var partyMembers = PartyManager.Instance.PartyMembers;
        if (partyMembers.Count > 0)
        {
            for (int i = 0; i < partyMembers.Count; i++)
            {
                var member = partyMembers[i];
                if (member.Definition is null)
                    continue;
                if (actionBase.CanShow(member.Definition as AllyDefinition_SO))
                    return true;
            }
        }
        return false;
    }

    #region UI 回调入口

    /// <summary>
    /// UI调用
    /// </summary>
    /// <param name="commandIndex"></param>
    /// <returns></returns>
    public bool ExecuteCommandFromUI(int commandIndex)
    {
        if (commandIndex >= _visibleEntries.Count || commandIndex < 0)
            return false;

        var action = _visibleEntries[commandIndex].Action;
        if(!action.CanExecute(_currentInteractor))
            return false;
        
        action.TriggerAction(_currentInteractor);
        return true;
    }

    #endregion

    public void OnEvent(InteractionRefreshEvent evt)
    {
        if (evt.target != this) return;
        if (_currentInteractor == null) return;

        RebuildCommands();

        // 刷新后没有可见命令，隐藏 ActionIcon，恢复头顶标记
        if (_visibleEntries.Count == 0)
        {
            _cachedCommandInfos.Clear();
            PublishEvent(false);
            HeadAnchor.gameObject.SetActive(true);
        }
    }
}
