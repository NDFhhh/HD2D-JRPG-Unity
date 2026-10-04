using MFramework.Event;

/// <summary>
/// 战斗初始化完成，UI可以开始显示，音乐开始播放
/// </summary>
public readonly struct BattleStartedEvent: IEvent
{
    // 可以携带战斗类型(Boss战/普通战)用于切换BGM
}

public readonly struct ActiveEntityChangedEvent : IEvent
{
    public readonly BattleEntity Entity;
    public ActiveEntityChangedEvent(BattleEntity entity)
    {
        Entity = entity;
    }
}

public readonly struct EntityStatChangedEvent : IEvent
{
    public readonly BattleEntity Entity;
    public readonly E_StatType StatType;
    public readonly int NewValue;
    public readonly int MaxValue;

    public EntityStatChangedEvent(BattleEntity entity, E_StatType statType, int newValue, int maxValue)
    {
        Entity = entity;
        StatType = statType;
        NewValue = newValue;
        MaxValue = maxValue;
    }
}

public readonly struct SkillNameDisplayEvent : IEvent
{
    public readonly BattleEntity Actor;
    public readonly string SkillName;
    /// <summary> 技能专属咏唱音效（来自 SkillData_SO.castSound，可为 null） </summary>
    public readonly AudioCue CastCueOverride;
    /// <summary> 是否播放咏唱音效（蓄力预告只显示技能名，音效等技能真正打出时再播） </summary>
    public readonly bool PlayCastSound;

    public SkillNameDisplayEvent(BattleEntity actor, string skillName, AudioCue castCueOverride = null, bool playCastSound = true)
    {
        Actor = actor;
        SkillName = skillName;
        CastCueOverride = castCueOverride;
        PlayCastSound = playCastSound;
    }
}

public readonly struct BattleNotificationEvent : IEvent
{
    public readonly string Message;
    public readonly bool IsSuccess;
    /// <summary> 显示总时长覆盖（秒，<= 0 时使用 UI 默认时长；BOSS 阶段提示用它与阶段音效时长对齐） </summary>
    public readonly float DisplayDuration;

    public BattleNotificationEvent(string message, bool isSuccess = false, float displayDuration = 0f)
    {
        Message = message;
        IsSuccess = isSuccess;
        DisplayDuration = displayDuration;
    }
}

public readonly struct EntityShieldChangedEvent : IEvent
{
    public readonly BattleEntity Target;
    public readonly int NewShield;

    public EntityShieldChangedEvent(BattleEntity target, int newShield)
    {
        Target = target;
        NewShield = newShield;
    }
}

public readonly struct EntityWeaknessChangedEvent: IEvent
{
    public readonly BattleEntity Target;
    
    public EntityWeaknessChangedEvent(BattleEntity target)
    {
        Target = target;
    }
}

// 恢复事件：把 “谁刚刚从 Break 里恢复”同步给外部系统
public readonly struct EntityRecoverFromBreakEvent: IEvent
{
    public readonly BattleEntity Target;

    public EntityRecoverFromBreakEvent(BattleEntity target)
    {
        Target = target;
    }
}

/// <summary>
/// Buff / Debuff 变化事件：施加 / 刷新 / 到期 / 清空时发布
/// 供 Buff 栏 UI 与音频系统订阅 (读取 BattleEntity.ActiveBuffs 拿到最新列表)
/// </summary>
public readonly struct EntityBuffChangedEvent : IEvent
{
    public readonly BattleEntity Target;

    public EntityBuffChangedEvent(BattleEntity target)
    {
        Target = target;
    }
}

#region 演出事件

/// <summary>
/// 请求播放击杀演出 (慢动作 / 镜头 / 溶解)
/// </summary>
public readonly struct KillCinematicRequestedEvent : IEvent
{
    public readonly BattleEntity Target;

    public KillCinematicRequestedEvent(BattleEntity target)
    {
        Target = target;
    }
}

/// <summary>
/// 请求播放破盾演出 (慢动作 / 镜头 / Break Volume)
/// </summary>
public readonly struct BreakCinematicRequestedEvent : IEvent
{
}

#endregion

#region 结算视角进入事件

public readonly struct BattleEndedEvent : IEvent
{
    public readonly bool IsWin;
    public readonly int ExpReward;
    public readonly int MoneyReward;
    public readonly List<BattleDropReward> DropRewards;

    public BattleEndedEvent(bool isWin, int expReward = 0, int moneyReward = 0, List<BattleDropReward> dropRewards = null)
    {
        IsWin = isWin;
        ExpReward = expReward;
        MoneyReward = moneyReward;
        DropRewards = dropRewards ?? new();
    }
}

public readonly struct BattleResultViewEnterEvent : IEvent
{
    public readonly int ExpReward;
    public readonly int MoneyReward;
    public readonly List<BattleDropReward> DropRewards;

    public BattleResultViewEnterEvent(int expReward, int moneyReward, List<BattleDropReward> dropRewards)
    {
        ExpReward = expReward;
        MoneyReward = moneyReward;
        DropRewards = dropRewards ?? new();
    }
}

public readonly struct BattleDropReward
{
    public readonly ItemDefinition_SO ItemDefinition;
    public readonly int Quantity;

    public BattleDropReward(ItemDefinition_SO itemDefinition, int quantity)
    {
        ItemDefinition = itemDefinition;
        Quantity = quantity;
    }
}

public readonly struct BattleLoseViewEnterEvent : IEvent
{
    
}

/// <summary>
/// 玩家在结算面板点击 “确认”
/// </summary>
public readonly struct BattleResultConfirmedEvent : IEvent
{
}

#endregion

#region 音频驱动事件

/// <summary>
/// 伤害 / 治疗结算事件：每次命中结算后由 AttackSkillExecutionEngine 发布
/// 音频系统订阅它驱动 命中 / 弱点 / 未命中 / 治疗 音效
/// </summary>
public readonly struct DamageDealtEvent : IEvent
{
    public readonly BattleEntity Attacker;
    public readonly BattleEntity Target;
    /// <summary> 结算数值（伤害或治疗量，<= 0 视为未命中） </summary>
    public readonly int Amount;
    public readonly bool IsHeal;
    public readonly bool IsMagic;
    public readonly bool IsWeaknessHit;
    /// <summary> 技能专属命中音效（来自 SkillData_SO.hitSound，可为 null） </summary>
    public readonly AudioCue HitCueOverride;
    /// <summary> 本次命中是否来自技能行动（技能咏唱音效已在出手时播放，命中时跳过默认攻击音效） </summary>
    public readonly bool IsSkillAction;

    public DamageDealtEvent(BattleEntity attacker, BattleEntity target, int amount,
        bool isHeal, bool isMagic, bool isWeaknessHit, AudioCue hitCueOverride = null,
        bool isSkillAction = false)
    {
        Attacker = attacker;
        Target = target;
        Amount = amount;
        IsHeal = isHeal;
        IsMagic = isMagic;
        IsWeaknessHit = isWeaknessHit;
        HitCueOverride = hitCueOverride;
        IsSkillAction = isSkillAction;
    }
}

/// <summary>
/// 逃跑成功事件：由 EscapeCommandHandler 在撤退演出结束后发布
/// </summary>
public readonly struct BattleEscapedEvent : IEvent
{
}

/// <summary>
/// BOSS 阶段切换完成事件：由 BattleEntity.TryApplyPendingBossPhase 在阶段正式生效时发布
/// </summary>
public readonly struct BossPhaseTransitionedEvent : IEvent
{
    public readonly BattleEntity Target;
    /// <summary> 刚刚生效的阶段索引 </summary>
    public readonly int PhaseIndex;
    /// <summary> 该阶段配置的专属音效（来自 BossPhaseConfig.phaseSound，可为 null） </summary>
    public readonly AudioCue PhaseCueOverride;

    public BossPhaseTransitionedEvent(BattleEntity target, int phaseIndex, AudioCue cueOverride)
    {
        Target = target;
        PhaseIndex = phaseIndex;
        PhaseCueOverride = cueOverride;
    }
}

#endregion