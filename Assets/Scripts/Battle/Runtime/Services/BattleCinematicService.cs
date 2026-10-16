using DG.Tweening;
using MFramework.Event;

public class BattleCinematicService : MonoBehaviour,
    IEventReceiver<KillCinematicRequestedEvent>,
    IEventReceiver<BreakCinematicRequestedEvent>
{
    [Header("Scene References")] 
    [SerializeField, Tooltip("直接拖拽 Break Volume 上的后处理组件")]
    private BattleHitPostFX breakPostFx;
    [SerializeField, Tooltip("直接拖拽 Battle Camera 的空父节点，用于击杀和破盾时轻微转镜头")]
    private Transform battleCameraPivot;

    [Header("Cinematic Config")]
    [SerializeField,Tooltip("战斗演出参数 SO，镜头和慢动作都集中在这里调")]
    private BattleCinematicConfig_SO cinematicConfig;

    public float KillDissolveStagger => cinematicConfig.KillDissolveStagger;

    #region 演出独占运行时状态

    // 当前正在播放的冲击演出协程
    // 新演出进来时，要靠它把旧演出强制收尾，避免两条协程同时写 Time.timeScale
    private Coroutine _impactRoutine;

    // 演出开始前的默认时间倍率
    // 注意：必须是成员字段而不是局部变量，否则第二条演出会把上一条的慢动作倍率
    // 当成 “默认值” 恢复回去，游戏就会卡在慢动作里
    private float _defaultTimeScale = 1f;

    // 镜头基准位缓存：强制中断旧演出时，要先把镜头拉回这里，再开新补间
    private bool _hasCameraBase;
    private Vector3 _cameraBasePos;
    private Quaternion _cameraBaseRot;

    #endregion

    /// <summary>
    /// 清掉缓存的镜头基准位。
    /// 供 BattleCameraRig 在入场重新定位相机后调用：基准位换了，旧缓存就成了错误目标，
    /// 下一次强制中断演出（或 OnDisable 兜底归位）会把镜头拉回上一场的机位。
    /// </summary>
    public void ResetCameraBaseCache()
    {
        _hasCameraBase = false;
    }

    private void OnEnable()
    {
        EventBus.Subscribe<KillCinematicRequestedEvent>(this);
        EventBus.Subscribe<BreakCinematicRequestedEvent>(this);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<KillCinematicRequestedEvent>(this);
        EventBus.Unsubscribe<BreakCinematicRequestedEvent>(this);

        // 保险：切场景 / 战斗结束时如果演出还在播，先把时间倍率和镜头归位
        // 否则游戏会永久卡在慢动作或镜头偏移里
        if (_impactRoutine != null)
        {
            StopCoroutine(_impactRoutine);
            _impactRoutine = null;
        }
        Time.timeScale = 1f;
        if (battleCameraPivot != null)
        {
            battleCameraPivot.DOKill();
            if (_hasCameraBase)
                battleCameraPivot.SetPositionAndRotation(_cameraBasePos, _cameraBaseRot);
        }
    }

    public void OnEvent(KillCinematicRequestedEvent evt)
    {
        // 击杀只发一次溶解，演出部分交给 PlayImpactCinematic 独占管理
        evt.Target.Unit.PlayEnemyDissolve();

        if (!cinematicConfig.EnableKillCinematic)
            return;

        ForceInterruptPreviousImpact();
        _impactRoutine = StartCoroutine(PlayKillCinematic(evt.Target));
    }

    public void OnEvent(BreakCinematicRequestedEvent evt)
    {
        if (!cinematicConfig.EnableBreakCinematic)
            return;

        ForceInterruptPreviousImpact();
        _impactRoutine = StartCoroutine(PlayBreakCinematic());
    }

    #region 击杀与破盾统一演出流程

    private IEnumerator PlayBreakCinematic()
    {
        breakPostFx.Play();
        yield return PlayImpactCinematic(cinematicConfig.Break);
        _impactRoutine = null;
    }

    private IEnumerator PlayKillCinematic(BattleEntity target)
    {
        yield return PlayImpactCinematic(cinematicConfig.Kill);
        _impactRoutine = null;
    }

    /// <summary>
    /// 把上一条还没播完的冲击演出强制收尾
    /// 关键点：如果不在这里收口，新旧两条协程会同时读写 Time.timeScale，
    /// 后结束的演出会把倍率恢复到慢动作值，游戏就永远卡在慢动作里
    /// </summary>
    private void ForceInterruptPreviousImpact()
    {
        if (_impactRoutine == null)
            return;

        // 1. 停掉旧演出协程（它可能正停在 HitStop 的 timeScale = 0 上）
        StopCoroutine(_impactRoutine);
        _impactRoutine = null;

        // 2. 停掉旧镜头补间，并把镜头拉回基准位
        //    否则新演出会把 “偏移到一半的位置” 当成基准，镜头越偏越远
        battleCameraPivot.DOKill();
        if (_hasCameraBase)
            battleCameraPivot.SetPositionAndRotation(_cameraBasePos, _cameraBaseRot);

        // 3. 先把时间倍率收回到默认，再让新演出重新采样基准
        Time.timeScale = _defaultTimeScale;
    }

    private IEnumerator PlayImpactCinematic(BattleImpactCinematicSettings settings)
    {
        // 1. 记录演出开始前的默认倍率，作为本次恢复的唯一基准
        _defaultTimeScale = Time.timeScale;

        // 2. 先按配置把镜头偏出去（转出去 -> 停留 -> 转回来，整条序列串好）
        Tween cameraTween = PlayCamera(settings);

        if (settings.HitStopDuration > 0f)
        {
            // 3. 命中瞬间先做一次短暂停顿    
            Time.timeScale = 0;
            yield return new WaitForSecondsRealtime(settings.HitStopDuration);
        }
        // 4. 接着进入慢动作
        yield return PlayTimeScale(_defaultTimeScale, settings.SlowMoScale, settings.SlowMoInDuration);

        // 5. 镜头序列已经把转向、停留和回位都串好了，直接等它播完就行
        // WaitForCompletion(): 在协程里等到这条 DOTween 动画真正播完
        yield return cameraTween.WaitForCompletion();

        // 6. 最后把时间倍率收回默认
        yield return PlayTimeScale(settings.SlowMoScale, _defaultTimeScale, settings.SlowMoOutDuration);
    }

    #endregion

    #region 相机演出

    private Tween PlayCamera(BattleImpactCinematicSettings settings)
    {
        // 1. 先记录镜头当前的基准位置和朝向（回位目标）
        //    这份基准同时会缓存到成员里，供 ForceInterruptPreviousImpact 强制回位使用
        _cameraBasePos = battleCameraPivot.position;
        _cameraBaseRot = battleCameraPivot.rotation;
        _hasCameraBase = true;

        // 2. 再根据配置算出这次要移动到的位置和朝向
        Vector3 toPos = _cameraBasePos + (_cameraBaseRot * settings.CameraPositionOffset);
        Quaternion toRot = _cameraBaseRot * Quaternion.Euler(settings.CameraEulerOffset);

        // 3. 先停掉这个 Transform 身上旧的补间动画，避免连续触发演出时互相抢位置
        battleCameraPivot.DOKill();

        // SetUpdate(true)：无视 Time.timeScale，用真实时间播放镜头动画
        Sequence sequence = DOTween.Sequence().SetUpdate(true);

        // 4. 先把镜头转到偏移位置
        AddCameraMove(sequence, toPos, toRot, settings.CameraTurnDuration);

        // 5. 镜头转到位后停留一小段时间
        if (settings.CameraHoldDuration > 0f)
            sequence.AppendInterval(settings.CameraHoldDuration);

        // 6. 最后回到基准位
        AddCameraMove(sequence, _cameraBasePos, _cameraBaseRot, settings.CameraReturnDuration);

        return sequence;
    }

    private void AddCameraMove(Sequence sequence, Vector3 toPos, Quaternion toRot, float duration)
    {
        if (duration <= 0f)
        {
            sequence.AppendCallback(()=>battleCameraPivot.SetPositionAndRotation(toPos, toRot));
            return;
        }

        sequence.Append(battleCameraPivot.DOMove(toPos, duration).SetEase(Ease.Linear));
        // Join：让旋转和位移同时播放
        sequence.Join(battleCameraPivot.DORotateQuaternion(toRot, duration).SetEase(Ease.Linear));
    }

    #endregion

    #region 时间工具

    private static IEnumerator PlayTimeScale(float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            Time.timeScale = to;
            yield break;
        }

        // 1. 用真实时间累加，避免慢动作过程中 Time.timeScale 影响自己
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            // 2. 从起始倍率平滑过渡到目标倍率
            Time.timeScale = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }

        // 3. 最后一帧直接到目标值，避免浮点误差残留
        Time.timeScale = to;
    }

    #endregion
}