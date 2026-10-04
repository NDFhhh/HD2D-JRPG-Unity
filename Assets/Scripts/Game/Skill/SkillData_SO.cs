[CreateAssetMenu(menuName = "Battle/Skill")]
public class SkillData_SO : ScriptableObject
{
    [Header("Special Logic Strategy")]
    public SkillLogic_SO specialLogic;
    
    [Header("Identity")] 
    public string skillID;
    public string skillName;
    
    [TextArea]
    public string description;
    public Sprite icon;

    [Header("Cost")] 
    [Min(0)] public int spCost;

    [Header("Targeting")] 
    public E_TargetType targetType = E_TargetType.SingleEnemy;

    [Header("Type")] 
    public E_SkillType skillType = E_SkillType.Damage;
    public E_DamageKind damageKind = E_DamageKind.Physical;
    public E_ElementType elementType = E_ElementType.None;
    public E_WeaponType weaponType = E_WeaponType.None;

    [Header("Effect (Prototype)")]
    [Min(0)] public int basePower;
    [Min(0)] public int hitCount = 1;
    [Min(0)] public int healAmount;

    [Header("Buff / Debuff (skillType 为 Buff / Debuff 时生效)")]
    [Tooltip("属性变化列表：0.25 = 提升 25%，-0.25 = 降低 25%。仅支持 物攻/物防/属攻/属防/速度/命中/回避")]
    public List<BuffEffectConfig> buffEffects = new();
    [Min(1), Tooltip("基础持续回合数；Boost 时会额外加上 boostTiers 里的 durationBonus")]
    public int buffDuration = 2;

    public BoostTierConfig[] boostTiers = new BoostTierConfig[4]
    {
        BoostTierConfig.Default(0),
        BoostTierConfig.Default(1),
        BoostTierConfig.Default(2),
        BoostTierConfig.Default(3)
    };

    [Header("VFX (Prototype)")] 
    public GameObject hitVfxPrefab;
    public E_SkillVfxSpawnMode vfxSpawnMode = E_SkillVfxSpawnMode.AntoByTargetType;
    [Tooltip("勾选后，特效会从施法者当前的位置发出；不勾选时，沿用 SpawnMode 决定位置")]
    public bool vfxSpawnFromCaster = false;
    public Vector3 vfxOffset;
    [Tooltip("额外的 Y 轴旋转。默认 0，想把左右方向反过来就填 180")]
    public float vfxYRotation = 0f;
    [Tooltip("特效出现后，等多久才真正结算命中。0 代表立即生效")]
    [Min(0)] public float vfxHitDelay = 0f;
    [Tooltip("特效生成后多久自动销毁。0 代表不自动销毁")]
    [Min(0)] public float vfxLifetime = 2f;

    [Header("Camera Impulse")]
    [Min(0f)] public float cameraImpulseStrength = 0f;

    [Header("Audio (技能专属音效，留空则用全局默认)")]
    [Tooltip("技能咏唱/起手音效。留空则播放全局默认 Skill_Cast")]
    public AudioCue castSound;
    [Tooltip("技能命中音效。留空则按 物理/魔法/弱点/治疗 播放全局默认音效")]
    public AudioCue hitSound;

    #region 敌人蓄力提示特效参数
    
    [Header("Telegraph VFX (蓄力提示特效)"), Tooltip("敌人进入蓄力状态时播放的特效 (特效)")]
    public GameObject telegraphVfxPrefab;
    [Tooltip("蓄力特效生成偏移 (相关于施法者锚点)")]
    public Vector3 telegraphVfxOffset;
    [Min(0f), Tooltip("蓄力特效自动销毁时间 (0 = 不自动销毁，等逻辑手动清除)")]
    public float telegraphVfxLifetime = 0f;
    [Tooltip("是否将蓄力特效挂到施法者身上，便于角色移动时跟随")]
    public bool telegraphVfxAttachToCaster = true;
    
    #endregion
    
    public BoostTierConfig GetBoostTier(int boostLevel)
    {
        int t = Mathf.Clamp(boostLevel, 0, 3);
        return boostTiers[t];
    }
    
    public float GetBasePowerMultiplier(int bpSpend)
    {
        int spend = Mathf.Clamp(bpSpend, 0, 3);
        return GetBoostTier(bpSpend).powerMultiplier;
    }

    public int GetFinalHitCount(E_BattleCommandType commandType, int bpSpend)
    {
        if (commandType == E_BattleCommandType.Attack && bpSpend <= 0)
            return 1;

        int finalHitCount = hitCount;
        int spend = Mathf.Clamp(bpSpend, 0, 3);
        return finalHitCount + GetBoostTier(spend).hitCountBonus;
    }

    /// <summary>
    /// 把技能资源上的武器 / 属性配置解析成统一伤害类型
    /// </summary>
    public E_DamageType ResolveDamageType()
    {
        if (weaponType != E_WeaponType.None)
        {
            return weaponType switch
            {
                E_WeaponType.Sword => E_DamageType.Sward,
                E_WeaponType.Spear => E_DamageType.Spear,
                E_WeaponType.Dagger => E_DamageType.Dagger,
                E_WeaponType.Axe => E_DamageType.Axe,
                E_WeaponType.Bow => E_DamageType.Bow,
                E_WeaponType.Staff => E_DamageType.Staff,
                _ => E_DamageType.Untyped
            };
        }

        if (elementType != E_ElementType.None)
        {
            return elementType switch
            {
                E_ElementType.Fire => E_DamageType.Fire,
                E_ElementType.Ice => E_DamageType.Ice,
                E_ElementType.Lightning => E_DamageType.Lightning,
                E_ElementType.Wind => E_DamageType.Wind,
                E_ElementType.Light => E_DamageType.Light,
                E_ElementType.Dark => E_DamageType.Dark,
                _ => E_DamageType.Untyped
            };
        }
        
        return E_DamageType.Untyped;
    }
}

#region Boost 分层配置结构

[Serializable]
public struct BoostTierConfig
{
    [Tooltip("Boost 等级 (0~3)"), Range(0, 3)]
    public int tier;
    
    [Header("Combat Stats"), Tooltip("倍率加成：伤害时乘 basePower, 治疗时乘 healAmount(例如：1.0，1.5，2.0，3.0)"), Min(0.01f)]
    public float powerMultiplier;
    
    [Tooltip("命中次数加成：最终命中 = hitCount + hitCountBonus"), Min(0)]
    public int hitCountBonus;

    [Header("Utility Stats"), Tooltip("概率加成 (0~1)：用于偷窃、即死、异常附加等机制的成功率提升")]
    public float chanceBonus;
    
    [Tooltip("持续回合加成：用于 Buff / Debuff 持续时间的延长")]
    public int durationBonus;
    
    [Tooltip("通用数值加成：用于特殊技能(如传递BP的数量增加、恢复固定SP等)")]
    public int genericValueBonus;

    public static BoostTierConfig Default(int tier)
    {
        return new BoostTierConfig
        {
            tier = tier,
            powerMultiplier = 1f,
            hitCountBonus = 0,
            chanceBonus = 0f,
            durationBonus = 0,
            genericValueBonus = 0
        };
    }
}

#endregion

#region Buff 效果配置结构

/// <summary>
/// 单条 Buff / Debuff 效果配置
/// 一个技能可以挂多条，实现 “同时提升物攻与速度” 这类复合 Buff
/// </summary>
[Serializable]
public struct BuffEffectConfig
{
    [Tooltip("作用的属性 (MaxHP / MaxSP 等永久属性暂不支持 Buff)")]
    public E_StatType stat;

    [Tooltip("百分比加成：0.25 = +25%，负数表示 Debuff")]
    public float percentBonus;
}

#endregion

