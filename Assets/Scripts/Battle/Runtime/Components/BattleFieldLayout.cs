public class BattleFieldLayout : MonoBehaviour
{
    [Header("Scene References")] 
    public Transform ActionTrans;
    public Transform InitTrans;

    [Header("Ally Slot Segment")] 
    [SerializeField] private Transform allyTopTrans;
    [SerializeField] private Transform allyBottomTrans;

    [Header("Enemy Slot Segment (Normal)")] 
    [SerializeField] private Transform enemyTopTrans;
    [SerializeField] private Transform enemyBottomTrans;
    
    [Header("Enemy Formation (BossTriangle)")]
    [SerializeField] private Transform bossCenterTrans;
    [SerializeField] private Transform minionTopTrans;
    [SerializeField] private Transform minionBottomTrans;

    public Vector3 GetAllySlotPos(int index, int count)
    {
        return LerpByMidpointRule(allyTopTrans.position, allyBottomTrans.position, index, count);
    }

    public Vector3 GetEnemySlotPos(int index, int count, E_EnemyLayoutFormation formation)
    {
        switch (formation)
        {
            case E_EnemyLayoutFormation.Line:
                return LerpByMidpointRule(enemyTopTrans.position, enemyBottomTrans.position, index, count);
            case E_EnemyLayoutFormation.BossTriangle:
                return GetEnemyBossTrianglePos(index, count);
            default:
                return Vector3.zero;
        }
    }

    #region 线段等分算法

    private Vector3 LerpByMidpointRule(Vector3 start, Vector3 end, int index, int count)
    {
        float t = (index +0.5f) / count;
        return Vector3.Lerp(start, end, t);
    }

    private Vector3 GetEnemyBossTrianglePos(int index, int count)
    {
        if (index == 0)
            return bossCenterTrans.position;
        return LerpByMidpointRule(enemyTopTrans.position, enemyBottomTrans.position, index, count);
    }

    #endregion

    #region 站位中心点

    public Vector3 GetAllyGroupCenter()
    {
        return Vector3.Lerp(allyTopTrans.position, allyBottomTrans.position, 0.5f);
    }

    public Vector3 GetEnemyGroupCenter(E_EnemyLayoutFormation formation)
    {
        if (formation == E_EnemyLayoutFormation.BossTriangle)
        {
            Vector3 bossPos =  bossCenterTrans.position;
            Vector3 minionPos = Vector3.Lerp(minionTopTrans.position, minionBottomTrans.position, 0.5f);
            return Vector3.Lerp(minionPos, bossPos, 0.5f);
        }
        
        return Vector3.Lerp(enemyTopTrans.position, enemyBottomTrans.position, 0.5f);
    }

    #endregion

}
