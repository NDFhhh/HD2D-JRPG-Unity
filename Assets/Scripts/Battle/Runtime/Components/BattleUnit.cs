using Unity.Cinemachine;

public class BattleUnit : MonoBehaviour
{
    private static readonly int IsDead = Animator.StringToHash("isDead");
    private static readonly int Hp01 = Animator.StringToHash("hp01");
    private static readonly int IsMoving = Animator.StringToHash("isMoving");
    private static readonly int Attack = Animator.StringToHash("attack");
    private static readonly int Use = Animator.StringToHash("use");
    private static readonly int IsWin = Animator.StringToHash("isWin");

    public BattleEntity Entity { get; private set; }
    
    [Header("Base Components")]
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;
    
    [Header("目标选择光标")]
    [SerializeField] private SpriteRenderer targetCursorRenderer;

    [Header("破盾眩晕特效")] 
    [SerializeField] private GameObject breakStunFx;
    [SerializeField] private float breakStunYOffset = 0.4f;

    [Header("镜头震动")] 
    [SerializeField] private CinemachineImpulseSource impulseSource;

    private GameObject _telegraphVfxInstance;

    private BattleUnitDissolveFX _dissolveFX;
    
    private void Awake()
    {
        _dissolveFX = GetComponent<BattleUnitDissolveFX>();
        targetCursorRenderer.enabled = false;    // 默认不显示目标选择光标
        UpdateTargetCursorPosition();
    }

    private void UpdateTargetCursorPosition()
    {
        if (spriteRenderer.sprite is null) 
            return;
        
        Vector3 worldCenter = spriteRenderer.bounds.center;
        Vector3 localCenter = transform.InverseTransformPoint(worldCenter);
        
        Vector3 localPos = targetCursorRenderer.transform.localPosition;
        localPos.y = localCenter.y;
        localPos.z = localCenter.z - 0.1f;
        targetCursorRenderer.transform.localPosition = localPos;
    }
    
    public void SetTargetSelection(bool visible)
    {
        if (visible)
            UpdateTargetCursorPosition();
        
        targetCursorRenderer.enabled = visible;
    }
    
    public void Bind(BattleEntity entity)
    {
        Entity = entity;

        if (entity.Definition.battleAnimator is not null)
            animator.runtimeAnimatorController = entity.Definition.battleAnimator;
        
        UpdateVisuals();
    }

    public void UpdateVisuals()
    {
        if (!Entity.IsAlive)
        {
            // 死亡动画
            SetBreakStunVisual(false);
            animator.SetBool(IsDead, true);
            StopTelegraphVfx();
            return;
        }
        
        animator.SetBool(IsDead, false);
        float maxHP = Entity.TotalStats.MaxHp;
        float hpRatio = Mathf.Clamp01(Entity.CurrentHP / maxHP);
        animator.SetFloat(Hp01, hpRatio);
    }

    public IEnumerator MoveToPosition(Vector3 targetPos, float duration = 0.5f)
    {
        animator.SetBool(IsMoving, true);
        
        Vector3 startPos = transform.position;
        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            float t = elapsedTime / duration;
            transform.position = Vector3.Lerp(startPos, targetPos, t);
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        transform.position = targetPos;
        animator.SetBool(IsMoving, false);
    }

    public void PlayVictoryAnimation()
    {
        animator.SetTrigger(IsWin);
    }

    public void PlayAttackAnimation()
    {
        animator.SetTrigger(Attack);
    }

    public void PlayUseItemAnimation()
    {
        animator.SetTrigger(Use);
    }

    /// <summary>
    /// 战斗中切换动画控制器（BOSS 进入新阶段换动画用）
    /// 前提是新旧控制器共用同一套基础状态机与参数（AnimatorOverrideController），只换 clips
    /// </summary>
    public void SetBattleAnimator(RuntimeAnimatorController controller)
    {
        if (controller == null || animator.runtimeAnimatorController == controller)
            return;

        animator.runtimeAnimatorController = controller;
        // 重置到默认状态（Idle），清掉旧控制器的状态残留，再刷回 isDead / hp01 等参数
        animator.Rebind();
        animator.Update(0f);
        UpdateVisuals();
    }

    #region 飘字

    public Vector3 GetPopupAnchorPosition() => targetCursorRenderer.transform.position;

    #endregion

    #region 破盾

    public void SetBreakStunVisual(bool visible)
    {
        if (visible)
            UpdateBreakStunPosition();
        
        breakStunFx.SetActive(visible);
    }

    private void UpdateBreakStunPosition()
    {
        // 获取精灵在世界空间中的最高点
        Vector3 worldTop = spriteRenderer.bounds.max;
        // 将世界坐标转换为本地坐标，以便在物体自身的坐标系中进行操作
        Vector3 localTop = transform.InverseTransformPoint(worldTop);
        // 这个特效物体本身就是锚点，直接移动它的本地位置
        Transform breakstunTransform = breakStunFx.transform;
        Vector3 localPos = breakstunTransform.localPosition;
        localPos.y = localTop.y + breakStunYOffset;
        breakstunTransform.localPosition = localPos;
    }

    #endregion

    public void PlayImpulse(float strength)
    {
        impulseSource.GenerateImpulse(strength);
    }

    #region 蓄力动画

    public void StopTelegraphVfx()
    {
        if (_telegraphVfxInstance is null) return;
        Destroy(_telegraphVfxInstance);
        _telegraphVfxInstance = null;
    }

    public void PlayTelegraphVfx(SkillData_SO skill)
    {
        StopTelegraphVfx();
        if (skill.telegraphVfxPrefab == null)
            return;

        var spawnPos = GetPopupAnchorPosition() + skill.telegraphVfxOffset;
        Transform parent = skill.telegraphVfxAttachToCaster ? transform : null;

        _telegraphVfxInstance = Instantiate(skill.telegraphVfxPrefab, spawnPos, Quaternion.identity, parent);

        if (skill.telegraphVfxLifetime > 0)
            Destroy(_telegraphVfxInstance, skill.telegraphVfxLifetime); 
    }

    #endregion

    #region 死亡特效

    public void SetBodyVisible(bool visible)
    {
        spriteRenderer.enabled = visible;
    }

    public void PlayEnemyDissolve(float delay = 0f)
    {
        // 这里只负责桥接，不在 BattleUnit 里重复写死亡 VFX 逻辑
        _dissolveFX.PlayDeathVfx(delay);
    }

    #endregion
}
