public class BattleFieldManager : MonoBehaviour
{
    [Header("Prefab")]
    [SerializeField] private BattleUnit battleUnitPrefab;
    private BattleFieldLayout layout;

    [Header("Boost VFX")]
    [SerializeField] private GameObject[] boostVfxPrefabs;

    [Tooltip("Boost 特效相对 actionTrans 的本地偏移坐标")] 
    [SerializeField] private Vector3 boostVfxOffset;
    
    private GameObject _boostVfxInstance;
    private int _currentBoostVfxLevel;
    
    private Transform allyRoot;
    private Transform enemyRoot;
    private readonly Dictionary<BattleUnit, Vector3> _homePos = new();
    
    private readonly List<BattleUnit> _spawnedAllyUnits = new();
    private readonly List<BattleUnit> _spawnedEnemyUnits = new();
    public IReadOnlyList<BattleUnit> SpawnedAllyUnits => _spawnedAllyUnits;
    public IReadOnlyList<BattleUnit> SpawnedEnemyUnits => _spawnedEnemyUnits;

    private E_EnemyLayoutFormation _currentFormation = E_EnemyLayoutFormation.Line;

    public void SpawnAll(BattleStartPayload payload)
    {
        layout = FindAnyObjectByType<BattleFieldLayout>();
        
        allyRoot = new GameObject("Ally Root").transform;
        allyRoot.SetParent(layout.transform, false);
        
        enemyRoot = new GameObject("Enemy Root").transform;
        enemyRoot.SetParent(layout.transform, false);
        
        _currentFormation = payload.Formation;
        
        // 要先清空已生成的
        ClearAllUnits();
        _spawnedAllyUnits.AddRange(SpawnSide(payload.Allies.Count, true));
        _spawnedEnemyUnits.AddRange(SpawnSide(payload.Enemies.Count, false));
    }

    /// <summary>
    /// 按阵营生成一侧单位，并记录出生点/归位点
    /// </summary>
    private List<BattleUnit> SpawnSide(int count, bool isAlly)
    {
        List<BattleUnit> units = new();
        for (int i = 0; i < count; i++)
        {
            // 1. 计算最终战斗单位 (Home Position)
            Vector3 targetSlotPos = isAlly
                ? layout.GetAllySlotPos(i, count)
                : layout.GetEnemySlotPos(i, count, _currentFormation);
            
            // 2. 决定出生点 (Spawn Position)
            Vector3 spawnPos = isAlly
                ? layout.InitTrans.position
                : targetSlotPos;
            
            // 3. 生成单位
            BattleUnit unitObj = Instantiate(
                battleUnitPrefab,
                spawnPos,
                Quaternion.identity,
                isAlly ? allyRoot : enemyRoot);
            
            // 4. 记录缓存
            units.Add(unitObj);
            
            // 【关键】 这里记录的是 targetSlotPos(最终站位)，不是 spawnPos(出生点)
            // 后续 BattleSetupState 会用这个 Home 位置来执行 “跑进场” 动画
            _homePos[unitObj] = targetSlotPos;
        }
        
        return units;
    }

    public void ClearAllUnits()
    {
        foreach (var unit in _spawnedAllyUnits)
        {
            if (unit != null)
                Destroy(unit.gameObject);
        }
        _spawnedAllyUnits.Clear();

        foreach (var unit in _spawnedEnemyUnits)
        {
            if (unit != null)
                Destroy(unit.gameObject);
        }
        _spawnedEnemyUnits.Clear();
        
        _homePos.Clear();
    }
    
    public Vector3 GetHomePos(BattleUnit unit) => _homePos[unit];
    public Vector3 GetActionPos() => layout.ActionTrans.position;
    public Vector3 GetInitPos() => layout.InitTrans.position;   
    public Vector3 GetSideCenter(bool isAlly) 
        =>isAlly 
            ? layout.GetAllyGroupCenter()
            : layout.GetEnemyGroupCenter(_currentFormation);

    #region Boost VFX

    public void SetBoostVfxLevel(int level)
    {
        if (_currentBoostVfxLevel == level) return;
        
        _currentBoostVfxLevel = level;
        
        if(_boostVfxInstance is not null)
            Destroy(_boostVfxInstance);

        if (level <= 0) return;
        
        if (level > 0)
        {
            _boostVfxInstance = Instantiate(
                boostVfxPrefabs[level - 1],
                layout.ActionTrans.position,
                Quaternion.identity,
                layout.ActionTrans
            );
            
            _boostVfxInstance.transform.localPosition = boostVfxOffset;
        }
    }

    #endregion
}
