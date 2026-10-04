using Random = UnityEngine.Random;

/// <summary>
/// 玩家暗雷追踪器
/// 负责在遇敌区域内累计水平移动距离，到阈值后直接抽敌并发起战斗
/// </summary>
public class PlayerEncounterTracker : MonoBehaviour
{
    private EncounterZone _currentZone;
    private Vector3 _lastPosition;
    
    // 距离计数器
    private float _accumulatedDistance;
    private float _targetEncounterDistance;

    private void Start()
    {
        _lastPosition = transform.position;
    }

    void Update()
    {
        Vector3 currentPos = transform.position;
        // 1. 只有处于遇敌区域内，才开始累计距离。
        if (_currentZone == null)
        {
            _lastPosition = currentPos;
            return;
        }

        // 2. 只计算水平位移，Y轴掉落不算进暗雷步数。
        Vector3 horizontalDelta = currentPos - _lastPosition;
        horizontalDelta.y = 0f;
        float distanceMoved = horizontalDelta.magnitude; // 计算水平位移距离
        _lastPosition = currentPos;

        if (distanceMoved <= 0.001f)
            return;

        // 3. 累计步数，并判断是否达到阈值。
        _accumulatedDistance += distanceMoved;
        if (_accumulatedDistance >= _targetEncounterDistance)
        {
            TriggerEncounter();
        }
    }

    #region 触发战斗
    private void TriggerEncounter()
    {
        // 1. 先切到战斗前的过渡模式，锁住玩家继续走路。
        GameModeManager.Instance.RequestChangeMode(E_GameMode.InteractionMenu);
        StartCoroutine(StartBattleRoutine(_currentZone));
    }

    private IEnumerator StartBattleRoutine(EncounterZone zone)
    {
        // 等待过渡动画播放完毕。
        yield return new WaitForSeconds(0.35f);

        // 1. 先抽取这次真正要出现的敌人组合。
        EncounterGroup encounter = zone.GetRandomEncounter();

        // 2. 再把当前队伍快照转成盟友数据。
        List<CharacterRuntimeData> allies = new(PartyManager.Instance.PartyMembers);

        // 3. 把抽到的敌方定义直接转成本场战斗要用的 RuntimeData。
        List<CharacterRuntimeData> enemies = new();
        foreach (CharacterDefinition_SO enemyDef in encounter.Enemies)
        {
            enemies.Add(new CharacterRuntimeData(enemyDef));
        }

        // 4. 最后把所有数据传给战斗管理器，开启战斗。
        BattleService.Instance.StartBattle(allies, enemies, zone.battleSceneReference, encounter.Formation);
        ResetEncounterDistance(zone);
    }
    #endregion


    #region 进出区域
    void OnTriggerEnter(Collider other)
    {
        if (!other.TryGetComponent(out EncounterZone zone))
            return;

        // 1. 进入新的遇敌区域后，先记录当前区域。
        _currentZone = zone;
        // 2. 再把当前位置作为新的计步起点。
        _lastPosition = transform.position;
        // 3. 最后重新随机这一区域的遇敌阈值。
        ResetEncounterDistance(zone);
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.TryGetComponent(out EncounterZone zone))
            return;

        if (_currentZone != zone)
            return;

        // 离开当前区域后，直接清掉区域引用和计步基准。
        _currentZone = null;
        _lastPosition = transform.position;
    }
    #endregion

    #region Helper

    /// <summary>
    /// 重置目标遇敌距离 (在 Min 和 Max 之间随机)
    /// </summary>
    private void ResetEncounterDistance(EncounterZone zone)
    {
        // 1. 先把累计步数清零
        _accumulatedDistance = 0f;
        // 2. 再在当前区域配置的最小 / 最大值之间随机出一个新的阈值
        _targetEncounterDistance = Random.Range(zone.minEncounterDistance, zone.maxEncounterDistance);
    }

    /// <summary>
    /// 传送后重置暗雷计步状态，避免把瞬移距离和旧进度算进步数
    /// </summary>
    public void ResetEncounterTracking(Vector3 position)
    {
        _lastPosition = position;
        _accumulatedDistance = 0f;
    }

    #endregion
}
