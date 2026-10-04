/// <summary>
/// 敌方死亡隐藏与特效播放组件
///
/// 这个组件现在只做一件事：
/// 先隐藏敌人本体，再按需延迟播放一次死亡 VFX，并在播完后自动清掉特效实例
/// </summary>
public class BattleUnitDissolveFX : MonoBehaviour
{
    [Header("死亡特效")] 
    [SerializeField, Tooltip("死亡特效预制体")] 
    private ParticleSystem deathVfxPrefab;
    
    [SerializeField,Tooltip("死亡特效的世界坐标偏移")]
    private Vector3 deathVfxOffset = Vector3.zero;

    [SerializeField, Tooltip("死亡特效对象多久后自动清掉")]
    private float deathVfxDestroyDelay = 3f;
    
    private BattleUnit _battleUnit;
    private bool _deathVfxPlayed;

    private void Awake()
    {
        _battleUnit = GetComponent<BattleUnit>();
    }

    /// <summary>
    /// 播放死亡特效
    /// 这里保留一个延迟参数，方便击杀演出做错峰播放
    /// </summary>
    public void PlayDeathVfx(float delay = 0f)
    {
        if (_deathVfxPlayed)
            return;
        // 先把状态锁住，避免重复触发同一段死亡演出
        _deathVfxPlayed = true;

        if (delay <= 0f)
        {
            HideBodyThenSpawnVfx();
            return;
        }

        StartCoroutine(CoPlayDeathVfx(delay));
    }
    
    private IEnumerator CoPlayDeathVfx(float delay)
    {
        // 1. 延迟阶段要兼容慢动作和暂停状态，所以优先读真实时间
        float elapsed = 0f;
        while (elapsed < delay)
        {
            float deltaTime = Time.timeScale <= 0f ? Time.unscaledDeltaTime : Time.deltaTime;
            elapsed += deltaTime;
            yield return null;
        }
        
        // 2. 时间到了以后，再真正生成死亡特效
        HideBodyThenSpawnVfx();
    }

    #region 特效生成

    private void HideBodyThenSpawnVfx()
    {
        // 1. 先把战斗单位本体隐藏掉
        _battleUnit.SetBodyVisible(false);
        
        // 2. 再在原地生成死亡 VFX
        var pos = transform.position + deathVfxOffset;
        var vfxInstance = Instantiate(deathVfxPrefab, pos, Quaternion.identity);
        Destroy(vfxInstance.gameObject, deathVfxDestroyDelay);
    }

    #endregion
}
