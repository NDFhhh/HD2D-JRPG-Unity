using MFramework.Event;
using UnityEngine.Pool;

public class BattleController : MonoBehaviour,
IEventReceiver<GameModeChangedEvent>
{
    [Header("配置参数")]
    [SerializeField] private BattleConfig_SO config;
    public BattleConfig_SO Config => config;
    
    [Header("场地管理")]
    [SerializeField] private BattleFieldManager fieldManager;
    public BattleFieldManager FieldManager => fieldManager;

    [Header("UI引用")]
    [SerializeField] private BattleCommandUI commandUI;
    [SerializeField] private BattleTimelineUI timelineUI;

    [Header("战斗演出")] 
    [SerializeField] private BattleCinematicService cinematicService;
    public BattleCinematicService CinematicService => cinematicService;
    
    public BattleTimelineUI TimelineUI => timelineUI;
    
    private readonly List<BattleEntity> _allEntities = new();
    public List<BattleEntity> AllEntities => _allEntities;
    
    private BattleState _currentState;
    private bool _battleRunning;
    public bool IsBattleRunning => _battleRunning;
    private Coroutine _battleLoopRoutine;
    
    private readonly BattleRoundScheduler _roundScheduler = new();
    
    [Header("飘字对象池")]
    [SerializeField] private DamagePopup damagePopupPrefab;
    [SerializeField] private bool useDamagePopupPool = true;
    [SerializeField] private int popupDefaultCapacity = 10;
    [SerializeField] private int popupMaxSize = 40;
    private ObjectPool<DamagePopup> _damagePopupPool;
    
    // 全局共享游标
    public BattleEntity CurrentEntity { get; set; }
    public BattleCommandRequest CurrentCommand { get; set; }

    private void Awake()
    {
        if (useDamagePopupPool)
            InitializeDamagePopupPool();
    }

    private void OnEnable()
    {
        EventBus.Subscribe(this);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe(this);
    }

    #region 对象池

    private void InitializeDamagePopupPool()
    {
        _damagePopupPool = new ObjectPool<DamagePopup>(
            createFunc: () =>
            {
                DamagePopup popup = Instantiate(damagePopupPrefab, transform);
                popup.gameObject.SetActive(false);
                popup.SetPool(_damagePopupPool);
                return popup;
            },
            actionOnGet: popup => popup.gameObject.SetActive(true),
            actionOnRelease: popup => popup.gameObject.SetActive(false),
            actionOnDestroy: popup => Destroy(popup.gameObject),
            defaultCapacity: popupDefaultCapacity,
            maxSize: popupMaxSize
        );
    }

    #endregion
    
    #region 回合调度桥接入口

    /// <summary>
    /// 开战完成后，正式初始化第一轮 CTB 顺序
    /// BattleController 自己不排顺序，只负责把参战列表交给调度器
    /// </summary>
    public void StartNewRound() =>_roundScheduler.Initialize(_allEntities);

    /// <summary>
    /// 向正式调度器请求 “下一位真正要行动的人”
    /// </summary>
    public BattleEntity GetNextActorByRound() => _roundScheduler.GetNextActor(_allEntities);

    /// <summary>
    /// 逐个取出登记了 BOSS 阶段切换的敌人（取空后返回 false）
    /// </summary>
    public bool TryDequeuePendingPhaseEntity(out BattleEntity entity)
        => _roundScheduler.TryDequeuePendingPhaseEntity(out entity);

    #endregion
    
    #region 事件监听

    public void OnEvent(GameModeChangedEvent evt)
    {
        if (evt.newMode == E_GameMode.Battle)
        {
            StartBattleIfReady();
            return;
        }

        if (_battleRunning)
            StopBattle();
    }

    #endregion

    public void SetState(BattleState nextState) => _currentState = nextState;

    private void StartBattleIfReady()
    {
        if (_battleRunning) return;
        if (GameModeManager.Instance.CurrentGameMode is not E_GameMode.Battle) return;
        if (!BattleService.Instance.HasPendingPayload) return;

        var payload = BattleService.Instance.ConsumeStartPayload();
        // 进入战斗第一个阶段
        SetState(new BattleSetupState(this, payload));
        
        _battleRunning = true;
        _battleLoopRoutine = StartCoroutine(BattleLoopRoutine());

    }

    /// <summary>
    /// 标准状态机
    /// (Enter 资源准备 -> Execute 行为触发 -> Exit 资源回收)
    /// </summary>
    private IEnumerator BattleLoopRoutine()
    {
        while (_battleRunning && _currentState != null)
        {
            // 先记住这一轮开始时的状态，避免 Enter 里切状态后还继续跑错流程
            var stateSnapshot =  _currentState;
            
            yield return StartCoroutine(stateSnapshot.Enter());
            
            // 如果 Enter 期间已经切到别的状态了，这一轮就不要继续往下执行
            if (stateSnapshot != _currentState)
            {
                yield return StartCoroutine(stateSnapshot.Exit());
                continue;
            }
            
            // 当前状态真正执行业务
            yield return StartCoroutine(stateSnapshot.Execute());
            
            // 当前状态自己的收尾清理
            yield return StartCoroutine(stateSnapshot.Exit());
        }
        
        _battleLoopRoutine = null;
    }

    public void StopBattle()
    {
        _battleRunning = false;
        _currentState = null;

        if (_battleLoopRoutine != null)
        {
            StopCoroutine(_battleLoopRoutine);
            _battleLoopRoutine = null;
        }
    }

    #region 目标选择高亮桥接

    /// <summary>
    /// 单体目标高亮：只让当前这个目标显示选中光标
    /// </summary>
    public void SetSelectedTarget(BattleEntity target)
    {
        foreach (var entity in _allEntities)
        {
            entity.Unit.SetTargetSelection(entity == target);
        }
    }

    /// <summary>
    /// 群体目标高亮：让候选集合里的所有目标一起显示选中光标
    /// </summary>
    public void SetselectedTargets(List<BattleEntity> targets)
    {
        foreach (var entity in _allEntities)
            entity.Unit.SetTargetSelection(targets.Contains(entity));
    }

    /// <summary>
    /// 退出目标选择时，统一关闭全部目标高亮
    /// </summary>
    public void ClearTargetSelection()
    {
        foreach (var entity in _allEntities)
            entity.Unit.SetTargetSelection(false);
    }

    #endregion

    #region 时间轴预测与破盾队列同步

    public void UpdateTimelinePrediction()
    {
        timelineUI.UpdateTimeline(_roundScheduler.BuildTimelinePrediction());
    }

    #endregion

    #region 伤害飘字

    public void SpawnDamagePopup(BattleEntity target, int amount, E_DamagePopupType type = E_DamagePopupType.Normal,
        Vector3 offset = default)
    {
        var anchorPos = target.Unit.GetPopupAnchorPosition() + offset;
        var popup = _damagePopupPool.Get();
        
        popup.transform.position = anchorPos;
        popup.transform.rotation = Quaternion.identity;
        popup.Setup(amount, type);
    }

    #endregion

    #region 破盾

    public void NotifyEntityBrokenOrDead(BattleEntity brokenEntity)
    {
        // 破盾或死亡后统一交给调度器重新排时间轴
        _roundScheduler.KickOutFromTimeLine(brokenEntity);
    }

    #endregion

    #region 胜负判定与结算分发桥接

    /// <summary>
    /// 结束战斗并发布结算事件 (供胜利演出等需要先做特写再结算的场景使用)
    /// </summary>
    public void EndBattle(BattleEndedEvent endedEvent)
    {
        StopBattle();
        EventBus.Publish(endedEvent);
    }

    #endregion
    
}
