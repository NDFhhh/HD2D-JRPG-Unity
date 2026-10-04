[CreateAssetMenu(menuName = "Character/Enemy")]
public class EnemyDefinition_SO : CharacterDefinition_SO
{
    #region 敌人奖励配置

    [Header("Rewards")] 
    public int ExpReward;
    public int MoneyReward;

    public List<InventoryItem> Drops;

    [Header("弱点及护盾")] 
    [Min(1)] public int MaxShields;
    public List<E_DamageType> Weaknesses;
    
    #endregion

    [Header("Enemy AI Turning"), Tooltip("敌人平时的基础出招倾向与阈值配置，BOSS 阶段里的倍率只负责临时放大或缩小当前阶段的偏向")]
    public EnemyAITuningConfig AiTurning = EnemyAITuningConfig.CreateDefault();

    [Header("Boss Phase (MVP)"), Tooltip("按列表顺序依次检查阶段。建议阈值从高到低配置，例如 0.75 -> 0.5 -> 0.25")]
    public List<BossPhaseConfig> BossPhases = new();
}

/// <summary>
/// Boss 阶段配置 (最小可用版)：
/// 1) 达到 HP 阈值后触发
/// 2) 可切换护盾上限
/// 3) 可替代弱点列表
/// 4) 可显示阶段提示文本，并支持入场延迟
/// 5) 可按阶段临时放大或缩小部分行动权重 (只负责阶段偏向，不重复定义整套 AI 性格)
/// </summary>
[Serializable]
public class BossPhaseConfig
{
    #region 阶段触发条件

    [Header("Trigger"), Range(0f, 1f), Tooltip("当当前血量比例 <= 此值时触发阶段")]
    public float triggerHpRatio = 0.5f;

    #endregion

    #region 护盾切换配置

    [Header("Shield"), Tooltip("是否覆盖敌人的护盾上限，并在阶段时重置当前护盾")]
    public bool overrideMaxShield;
    [Min(1), Tooltip("新的护盾上限")] 
    public int maxShield = 1;

    #endregion

    #region 弱点切换配置

    [Header("Weakness"),Tooltip("是否替换弱点列表")]
    public bool overrideWeaknesses;
    [Tooltip("新的弱点列表 (会覆盖旧弱点)")] 
    public List<E_DamageType> weaknesses = new();

    #endregion

    #region 阶段提示文本

    [Header("Prompt"), TextArea, Tooltip("阶段切换时显示在战斗通知中的文本")]
    public string promptText;
    [Min(0), Tooltip("阶段提示后等待多久再开始行动")] 
    public float introDelay = 0.6f;

    #endregion

    #region 阶段出招倾向倍率

    // 这一层只负责 BOSS 进入当前阶段后的临时偏向
    // 基础权重仍然来自 EnemyAITurningConfig
    [Header("AI Phase Bias"), Min(0f), Tooltip("进入该阶段后，普攻权重要乘上的倍率")]
    public float basicAttackWeightMultiplier = 1f;
    [Min(0f), Tooltip("进入该阶段后，普通伤害技能权重要乘上的倍率")]
    public float damageWeightMultiplier = 1f;
    [Min(0f), Tooltip("进入该阶段后，治疗技能权重要乘上的倍率")]
    public float healWeightMultiplier = 1f;
    [Min(0f), Tooltip("进入该阶段后，蓄力大招权重要乘上的倍率")]
    public float telegraphWeightMultiplier = 1f;

    #endregion

    #region 阶段动画切换

    [Header("Animation"), Tooltip("进入该阶段后切换的战斗动画控制器（AnimatorOverrideController，与基础状态机共用同一套参数，只换 clips）。留空则沿用当前动画")]
    public AnimatorOverrideController phaseBattleAnimator;

    #endregion

    #region 阶段切换音效

    [Header("Audio"), Tooltip("进入该阶段时播放的专属音效。留空则播放全局默认 Battle_Phase_Change")]
    public AudioCue phaseSound;
    /// <summary>
    /// 阶段切换音效的实际播放时长（秒）：专属音效优先，否则查全局默认 Battle_Phase_Change
    /// 无音频系统 / 未配置时返回 0，调用方把 0 视为 “无需等待”
    /// </summary>
    public float ResolveSoundDuration()
    {
        if (phaseSound != null)
            return phaseSound.LongestPlayableDuration;
        return AudioManager.GetSfxDuration(E_SoundId.Battle_Phase_Change);
    }

    #endregion
}

/// <summary>
/// 敌人 AI 调参表 (Utility AI)
/// 说明：
/// 1) 这层负责 “这个敌人平时更爱做什么”，也就是基础性格
/// 2) 数值越大，抽中概率越高 (轮盘赌权重)
/// 3) BossPhaseConfig 的阶段倍率只是在特定阶段临时放大或缩小这里的结果
/// 4) Buff / Debuff 技能目前共用 defaultSkillWeight，且 AI 不会重复给自己上已有的 Buff；需要更细的倾向时再补独立权重
/// </summary>
[Serializable]
public class EnemyAITuningConfig
{
    #region 基础权重

    // 当前教程只保留真正会讲到的几类权重
    // Buff / Debuff 共用 defaultSkillWeight，如需独立倾向再回来补字段
    [Header("Base Weights"), Min(0f), Tooltip("普通基础权重")]
    public float basicAttackWeight;
    [Min(0f), Tooltip("普通伤害技能基础权重")] 
    public float damageSkillWeight;
    [Min(0f),Tooltip("当前项目里未单独区分的技能类型基础权重")]
    public float defaultSkillWeight;
    
    #endregion

    #region 蓄力大招阈值

    [Header("Telegraph Threshold"), Min(0), Tooltip("判定为蓄力大招所需最低基础威力")]
    public float telegraphMinBasePower;
    [Min(0), Tooltip("判定为蓄力大招所需最低 SP 消耗")] 
    public float telegraphMinSpCost;
    [Range(0f, 1f), Tooltip("HP 比例低于该数值时提高蓄力大招权重；被判定为蓄力技的技能选中后仍会先进入蓄力回合")]
    public float telegraphHpRatioThreshold;
    [Min(0f), Tooltip("蓄力大招基础权重")] 
    public float telegraphWeight;

    #endregion

    #region 治疗策略

    [Header("Heal Strategy")] 
    [Range(0f, 1f), Tooltip("队友 HP 比例低于该值判定为残血")] 
    public float healLowHpRatioThreshold;
    [Min(0f), Tooltip("治疗技能基础权重")] 
    public float healBaseWeight;
    [Min(0f), Tooltip("每多 1 个残血队友增加的治疗权重")]
    public float healPerLowHpBonus;
    [Range(0f, 1f), Tooltip("单体治疗在多人残血时的惩罚系数")]
    public float singleHealMultiLowHpPenalty;

    #endregion

    #region 默认配置工厂

    public static EnemyAITuningConfig CreateDefault()
    {
        return new EnemyAITuningConfig()
        {
            basicAttackWeight = 10f,
            damageSkillWeight = 15f,
            defaultSkillWeight = 10f,
            telegraphMinBasePower = 50,
            telegraphMinSpCost = 10,
            telegraphHpRatioThreshold = 0.7f,
            telegraphWeight = 40f,
            healLowHpRatioThreshold = 0.4f,
            healBaseWeight = 40f,
            healPerLowHpBonus = 20f,
            singleHealMultiLowHpPenalty = 0.8f
        };
    }

    #endregion
}