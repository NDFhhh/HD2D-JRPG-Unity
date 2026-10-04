public class BattleEntity
{
    public CharacterRuntimeData RuntimeData { get; }
    public CharacterDefinition_SO Definition => RuntimeData.Definition;
    public BattleUnit Unit { get; }
    public string ID { get; }
    public bool IsPlayer { get; }
    public bool IsAlive => RuntimeData.CurrentHP > 0;
    public int CurrentHP => RuntimeData.CurrentHP;
    public int CurrentSP => RuntimeData.CurrentSP;
    public int CurrentBP => RuntimeData.CurrentBP;
    public StatBlock TotalStats => RuntimeData.GetTotalStats();

    private const int MaxBattleBP = 5;
    
    private bool _usedBPInThisTurn;

    /// <summary> 是否处于防御姿态(持续到当前 round 结束)</summary>
    public bool IsDefending { get; private set; }

    #region 掉落偷取

    public List<InventoryItem> BattleDrops { get; } = new();
    public bool HasBeenRobbed { get; private set; }

    #endregion

    #region 蓄力技能临时状态字段

    // --- 蓄力大招机制 (Telegraphed Attacks) ---
    /// <summary> 当前正在蓄力的技能 (下一回合强制释放) 若遇破盾则应该被打断 (清空) </summary>
    public SkillData_SO PreparedSkill { get; set; }

    #endregion

    #region BOSS 阶段临时状态字段

    // BOSS 阶段机制 (Phase Transition MVP)
    /// <summary> 当前已进入的 BOSS 阶段索引 (-1 表示还未进入任何阶段) </summary>
    private int CurrentBossPhaseIndex { get; set; }
    /// <summary> 已满足阈值但尚未播报 / 应用的阶段索引 (-1 表示无挂起)</summary>
    private int _pendingBoosPhaseIndex;

    /// <summary> 是否有已挂起、等待落地演出的 BOSS 阶段 </summary>
    public bool HasPendingBossPhase => _pendingBoosPhaseIndex >= 0;

    #endregion
    
    #region Buff / Debuff 临时状态

    private readonly List<BuffInstance> _activeBuffs = new();
    /// <summary> 当前生效中的 Buff / Debuff 列表 (给 Buff 栏 UI 读取用) </summary>
    public IReadOnlyList<BuffInstance> ActiveBuffs => _activeBuffs;

    // 同一属性上所有 Buff / Debuff 叠加后的总百分比上下限
    private const float MaxTotalBuffPercent = 2f;
    private const float MinTotalBuffPercent = -0.75f;

    #endregion

    #region 破盾与护盾临时状态

    public int CurrentShield { get; private set; }
    public bool IsBroken { get; private set; }
    private int MaxShield { get; set; }
    
    /// <summary> 剩余需跳过的行动次数 (由时间轴或行动点消费)</summary>
    private int BrokenTurnsRemaining { get; set; }
    /// <summary>
    /// 回合内一次性 “跳过行动” 挂起标记：
    /// 新回合由 TriggerBreakSkipForRound 置 true，消耗一次后由 ConsumeBrokenTurn 置 false
    /// </summary>
    private bool BreakSkipPending { get; set; }
    
    private readonly HashSet<E_DamageType> _weaknesses = new();
    private readonly List<E_DamageType> _orderedWeaknesses = new();

    #endregion

    public BattleEntity(CharacterRuntimeData runtimeData, BattleUnit unit, bool isPlayer, string stableID)
    {
        RuntimeData = runtimeData;
        Unit = unit;
        IsPlayer = isPlayer;
        ID = stableID;

        InitializeBattleStats();
    }

    private void InitializeBattleStats()
    {
        if (Definition is EnemyDefinition_SO enemyDefinition)
        {
            InitializeBattleDrops(enemyDefinition);
            MaxShield = Mathf.Max(1, enemyDefinition.MaxShields);
            ApplyWeaknesses(enemyDefinition.Weaknesses, false);
        }
        
        ResetShieldAndBreakState();
        _usedBPInThisTurn = false;
        _pendingBoosPhaseIndex = -1;
        CurrentBossPhaseIndex = -1;
    }

    private void InitializeBattleDrops(EnemyDefinition_SO enemyDefinition)
    {
        BattleDrops.Clear();

        if (enemyDefinition.Drops.Count >= 0)
        {
            for (int i = 0; i < enemyDefinition.Drops.Count; i++)
            {
                InventoryItem drop = enemyDefinition.Drops[i];
                if (drop.Quantity <= 0)
                    continue;
                BattleDrops.Add(new(drop.ItemDefinition, drop.Quantity));
            }
            RefreshRobbedState();
        }
        
    }

    public void RefreshRobbedState()
    {
        // 只要背包里还存在一条有效且数量大于 0 的掉落，就说明这个敌人还没被偷空
        HasBeenRobbed = true;

        for (int i = 0; i < BattleDrops.Count; i++)
        {
            if (BattleDrops[i].Quantity > 0)
            {
                HasBeenRobbed = false;
                return;
            }
        }
    }

    #region 行动相关

    public int GetCurrentSpeed()
    {
        // 速度吃 Buff / Debuff 修正 (加速 / 减速会直接影响 CTB 排序)
        return GetEffectiveStats().Speed;
    }

    public void SpendSP(int cost)
    {
        RuntimeData.ModifySP(-cost);
        
        // 广播更新 SP
        EventBus.Publish(new EntityStatChangedEvent(this, E_StatType.CurrentSP, CurrentSP, TotalStats.MaxSp));
    }

    public void SpendBP(int amount)
    {
        RuntimeData.ModifyBP(-amount);
        
        // 广播更新 BP
        EventBus.Publish(new EntityStatChangedEvent(this, E_StatType.CurrentBP, CurrentBP, MaxBattleBP));
    }

    public void RecoverBP()
    {
        // 1. 先判断上一回合有没有使用过 BP
        bool shouldRecoverBP = !_usedBPInThisTurn;
        
        // 2. 无论恢不恢复，这里都要先把 “本回合是否使用过 BP” 的标记清掉
        _usedBPInThisTurn = false;
        
        // 3. 不满足恢复条件，或者已经满 BP，就直接结束
        if (!shouldRecoverBP || CurrentBP >= MaxBattleBP)
            return;
        
        // 4. 真正恢复 1 点 BP，并广播给 UI
        RuntimeData.ModifyBP(1);
        EventBus.Publish(new EntityStatChangedEvent(this, E_StatType.CurrentBP, CurrentBP, MaxBattleBP));
    }

    public void RestoreSP(int amount)
    {
        RuntimeData.ModifySP(amount);
        EventBus.Publish(new EntityStatChangedEvent(this, E_StatType.CurrentSP, CurrentSP, TotalStats.MaxSp));
    }

    public void MarkBPUsed() => _usedBPInThisTurn = true;

    #endregion
    
    #region 伤害 / 治疗 / 恢复 与数值计算

    public int CalculateDamageFrom(BattleEntity attacker, SkillData_SO skill, float powerMultiplier)
    {
        bool isMagical = skill != null && skill.damageKind == E_DamageKind.Magical;
        StatBlock atkStats = attacker.GetEffectiveStats();
        StatBlock defStats = GetEffectiveStats();

        int atk = isMagical ? atkStats.MAtk : atkStats.PAtk;
        int def = isMagical ? defStats.MDef : defStats.PDef;

        if (IsDefending)
            def = Mathf.RoundToInt(def * 1.5f);
        
        int basePower = skill.basePower;
        int rawDamage = Mathf.Max(1, atk - def + basePower);
        return Mathf.RoundToInt(rawDamage * powerMultiplier);

    }

    public void TakeDamage(int amount)
    {
        if (!IsAlive) return;
        
        // 1. 先把伤害真正扣到 RuntimeData 上
        RuntimeData.ModifyHP(-amount);

        // 1.5 死亡时清空所有 Buff / Debuff
        if (!IsAlive)
            ClearAllBuffs();

        // 2. 受伤后顺手检查是否满足 Boss 阶段切换阈值，并先挂起阶段
        TryQueueBossPhaseTransitionByHp();
        
        // 3. 广播伤害事件
        EventBus.Publish(new EntityStatChangedEvent(this, E_StatType.CurrentHP, CurrentHP, TotalStats.MaxHp));
        
        // 4. 最后刷新单位表现，比如死亡，残血动画，破盾表现等
        Unit.UpdateVisuals();
    }

    public int CalculateHealAmountFromSkill(SkillData_SO skill, float powerMultiplier)
    {
        int baseHeal = Mathf.Max(0, skill.healAmount);
        return Mathf.RoundToInt(baseHeal * powerMultiplier);
    }

    public void Heal(int amount)
    {
        if (!IsAlive) return;
        
        RuntimeData.ModifyHP(amount);
        EventBus.Publish(new EntityStatChangedEvent(this, E_StatType.CurrentHP, CurrentHP, TotalStats.MaxHp));
        Unit.UpdateVisuals();
    }

    #endregion

    #region 防御姿态接口

    /// <summary>
    /// 进入防御姿态，持续到当前 round 结束
    /// </summary>
    public void EnterDefendStance()
    {
        if (!IsAlive) return;
        
        IsDefending = true;
    }

    /// <summary>
    /// 退出防御状态 (通常在新 round 开始时统一清除)
    /// </summary>
    public void ClearDefendStance() => IsDefending = false;

    #endregion

    #region 弱点

    private void ApplyWeaknesses(List<E_DamageType> weaknesses, bool publishEvent)
    {
        // 1. 先清掉旧的弱点缓存
        _weaknesses.Clear();
        _orderedWeaknesses.Clear();
        
        // 2. 再把新弱点按 “判定集合 + UI 顺序列表” 双写进缓存
        foreach (E_DamageType type in weaknesses)
        {
            if (type is E_DamageType.None or E_DamageType.Untyped)
                continue;
            
            // 双写结构：HashSet 用于命中判定，List 用于 UI 顺序展示
            if (_weaknesses.Add(type))
                _orderedWeaknesses.Add(type);
        }
        
        // 3. 如有需要，最后广播弱点变化事件
        if (publishEvent)
            EventBus.Publish(new EntityWeaknessChangedEvent(this));
    }
    
    public bool IsWeakTo(E_DamageType type) => !IsPlayer && _weaknesses.Contains(type);
    public List<E_DamageType> GetWeaknesses() => _orderedWeaknesses;

    #endregion

    #region 破盾

    public bool TryReduceShield(int amount)
    {
        // 1. 只要没破防且还有护盾时，才需要真正扣层数
        if (IsBroken || CurrentShield <= 0)
            return false;
        
        // 2. 先扣掉当前护盾值
        CurrentShield = Mathf.Max(0, CurrentShield - amount);
        
        // 3. 立即广播给 UI 刷新护盾数字
        EventBus.Publish(new EntityShieldChangedEvent(this, CurrentShield));
        
        // 4. 如果护盾归零，就正式进入 Break
        if (CurrentShield == 0)
        {
            OnBreak();
            return true;
        }
        
        return false;
    }

    private void OnBreak()
    {
        // 1. 先把内部 Break 标记和跳过次数写好
        IsBroken = true;
        BrokenTurnsRemaining = 1;
        BreakSkipPending = false;
        
        // 2. 破盾会打断技能，所以这里把准备中的技能和特效清掉
        PreparedSkill = null;
        Unit.StopTelegraphVfx();
        
        // 3. 再刷新单位头上的 Break 眩晕表现
        Unit.SetBreakStunVisual(true);
    }

    /// <summary>
    /// 从破防状态恢复 (通常在 Break 后的下一回合结束时调用)
    /// </summary>
    public void RecoverFromBreak()
    {
        if (!IsBroken)
            return;
        
        // 1. 先把内部 Break 状态复位
        ResetShieldAndBreakState();
        // 2. 再关掉场景里的 Break 表现
        Unit.SetBreakStunVisual(false);
        
        // 3. 最后按 “恢复事件 -> 护盾事件” 的顺序广播，让外部读到最新状态
        EventBus.Publish(new EntityRecoverFromBreakEvent(this));
        EventBus.Publish(new EntityShieldChangedEvent(this, CurrentShield));
    }

    public void ResolveBreakRecoveryAtRoundStart()
    {
        if (IsBroken && BrokenTurnsRemaining <= 0)
            RecoverFromBreak();
    }

    private void ResetShieldAndBreakState()
    {
        IsBroken = false;
        BrokenTurnsRemaining = 0;
        BreakSkipPending = false;
        CurrentShield = MaxShield;
    }

    public void AddBrokenSkipTurns(int amount)
    {
        if (!IsBroken)
            return;
        
        BrokenTurnsRemaining += amount;
    }

    /// <summary>
    /// 根据时间轴消耗破碎回合数
    /// </summary>
    /// <param name="amount">要消耗的回合数</param>
    public void ConsumeBrokenTurnsByTimeLine(int amount)
    {
        // 检查角色是否处于破碎状态，以及要消耗的回合数是否有效
        if (!IsBroken || BrokenTurnsRemaining <= 0)
            return;
        
        // 减少剩余的破碎回合数，确保不会小于 0
        BrokenTurnsRemaining = Mathf.Min(0, BrokenTurnsRemaining - amount);
        
        // 下一回合若仍需跳过，会由 TriggerBreakSkipForRound 重新置位
        BreakSkipPending = false;
    }

    public void TriggerBreakSkipForRound()
    {
        if (IsBroken && BrokenTurnsRemaining > 0)
            BreakSkipPending = true;
    }

    #endregion

    #region BOSS 阶段逻辑 (Phase Transition MVP)

    /// <summary>
    /// 获取当前已生效的 BOSS 阶段配置 (若不存在则返回 null)
    /// 用于 AI 根据阶段调整出招权重
    /// </summary>
    public BossPhaseConfig GetActivePhaseConfig()
    {
        if (Definition is not EnemyDefinition_SO enemyDef
            || CurrentBossPhaseIndex < 0
            || CurrentBossPhaseIndex >= enemyDef.BossPhases.Count)
            return null;
        return enemyDef.BossPhases[CurrentBossPhaseIndex];
    }
    
    /// <summary>
    /// 按当前 HP 比例检查 BOSS 阶段切换，并挂起目标阶段
    /// 规则：
    /// 1) 仅敌方可触发
    /// 2) 按配置列表顺序检查 (建议阈值从高到低)
    /// 3) 支持一次大伤害跨多个阶段 (直接挂起到最终应进入阶段)
    /// 4) 挂起后由外部流程在 “可行行动窗口” 调用 TryApplyPendingBossPhase 正式应用
    /// </summary>
    private void TryQueueBossPhaseTransitionByHp()
    {
        if (IsPlayer || !IsAlive || Definition is not EnemyDefinition_SO enemyDef)
            return;

        float hpRatio = CurrentHP / (float)Mathf.Max(1, TotalStats.MaxHp);
        int targetPhaseIndex = CurrentBossPhaseIndex;
        for (int i = CurrentBossPhaseIndex + 1; i < enemyDef.BossPhases.Count; i++)
        {
            var phase = enemyDef.BossPhases[i];
            if (hpRatio <= phase.triggerHpRatio)
            {
                targetPhaseIndex = i;
            }
            else
            {
                // 如果当前阶段都不满足条件，由于是从高血量到低血量检查，说明后面阈值更低肯定也不满足
                break;
            }

            if (targetPhaseIndex > CurrentBossPhaseIndex && targetPhaseIndex > _pendingBoosPhaseIndex)    // 防止重复挂起
            {
                _pendingBoosPhaseIndex = targetPhaseIndex;
                Debug.Log($"【BossPhase】 挂起阶段 {_pendingBoosPhaseIndex + 1}，等待可行行动窗口应用");
            }
        }
    }

    public bool TryApplyPendingBossPhase(out BossPhaseConfig appliedPhase)
    {
        appliedPhase = null;
        
        // 规则：处于破盾眩晕时，不应用阶段切换
        if (IsBroken)
            return false;

        if (Definition is not EnemyDefinition_SO enemyDef || _pendingBoosPhaseIndex < 0)
            return false;
        
        var phase = enemyDef.BossPhases[_pendingBoosPhaseIndex];
        CurrentBossPhaseIndex = _pendingBoosPhaseIndex;
        _pendingBoosPhaseIndex = -1;
        appliedPhase = phase;
        ApplyBossPhaseConfig(phase);

        // 音频：广播阶段切换完成（附带该阶段专属音效配置）
        EventBus.Publish(new BossPhaseTransitionedEvent(this, CurrentBossPhaseIndex, phase.phaseSound));
        return true;

    }

    /// <summary>
    /// 套用单个 BOSS 阶段配置
    /// 仅处理 MVP 需求：护盾切换，弱点切换，战斗动画切换
    /// </summary>
    private void ApplyBossPhaseConfig(BossPhaseConfig phase)
    {
        // 1. 先处理护盾切换
        if (phase.overrideMaxShield)
        {
            bool wasBroken = IsBroken;

            MaxShield = phase.maxShield;
            ResetShieldAndBreakState();
            Unit.SetBreakStunVisual(false);

            if (wasBroken)
                EventBus.Publish(new EntityRecoverFromBreakEvent(this));
            EventBus.Publish(new EntityShieldChangedEvent(this, CurrentShield));
        }

        // 2. 再处理弱点切换
        if (phase.overrideWeaknesses)
            ApplyWeaknesses(phase.weaknesses, true);

        // 3. 最后处理战斗动画切换（配了才换，状态机结构不变只换 clips）
        if (phase.phaseBattleAnimator != null)
            Unit.SetBattleAnimator(phase.phaseBattleAnimator);
    }

    public string ResolveBossPhasePrompt(BossPhaseConfig phase)
        => !string.IsNullOrWhiteSpace(phase.promptText)
            ? phase.promptText
            : "进入了新的阶段！";

    #endregion

    #region Buff / Debuff 逻辑

    /// <summary>
    /// 从技能施加 Buff / Debuff
    /// 规则：
    /// 1) 同属性同类型 (增益 / 减益) 重复上时，取更强的数值并刷新持续回合
    /// 2) 不同属性、或同属性但一正一负的效果可以共存，结算时求和
    /// </summary>
    /// <param name="skill">来源技能 (读取其 buffEffects / buffDuration)</param>
    /// <param name="bonusDuration">额外持续回合 (来自 Boost 的 durationBonus)</param>
    public void ApplyBuffsFromSkill(SkillData_SO skill, int bonusDuration)
    {
        if (skill == null || skill.buffEffects.Count == 0 || !IsAlive)
            return;

        int duration = Mathf.Max(1, skill.buffDuration + bonusDuration);
        bool changed = false;

        foreach (BuffEffectConfig effect in skill.buffEffects)
        {
            if (!BuffStatUtility.IsBuffableStat(effect.stat) || Mathf.Approximately(effect.percentBonus, 0f))
                continue;

            BuffInstance existing = FindBuff(effect.stat, effect.percentBonus < 0f);
            if (existing != null)
            {
                // 重复上同一类 Buff：数值取更强者，持续回合刷新为较长者
                if (Mathf.Abs(effect.percentBonus) > Mathf.Abs(existing.PercentBonus))
                    existing.PercentBonus = effect.percentBonus;
                existing.RemainingTurns = Mathf.Max(existing.RemainingTurns, duration);
            }
            else
            {
                _activeBuffs.Add(new BuffInstance(skill.skillName, skill.icon, effect.stat, effect.percentBonus, duration));
            }

            changed = true;

            // 战斗提示
            string direction = effect.percentBonus >= 0f ? "提升" : "下降";
            EventBus.Publish(new BattleNotificationEvent($"{Definition.Name}的{BuffStatUtility.GetDisplayName(effect.stat)}{direction}了！"));
        }

        if (changed)
            EventBus.Publish(new EntityBuffChangedEvent(this));
    }

    private BuffInstance FindBuff(E_StatType stat, bool isDebuff)
    {
        for (int i = 0; i < _activeBuffs.Count; i++)
        {
            if (_activeBuffs[i].Stat == stat && _activeBuffs[i].IsDebuff == isDebuff)
                return _activeBuffs[i];
        }
        return null;
    }

    /// <summary> 某属性当前的 Buff 百分比总和 (含 Buff 与 Debuff，已做上下限钳制) </summary>
    public float GetBuffPercent(E_StatType stat)
    {
        float total = 0f;
        for (int i = 0; i < _activeBuffs.Count; i++)
        {
            if (_activeBuffs[i].Stat == stat)
                total += _activeBuffs[i].PercentBonus;
        }
        return Mathf.Clamp(total, MinTotalBuffPercent, MaxTotalBuffPercent);
    }

    /// <summary>
    /// 含 Buff 修正后的战斗属性
    /// 只影响 物攻/物防/属攻/属防/速度/命中/回避，不影响 MaxHP / MaxSP
    /// </summary>
    public StatBlock GetEffectiveStats()
    {
        StatBlock stats = TotalStats;
        if (_activeBuffs.Count == 0)
            return stats;

        stats.PAtk = ApplyBuffPercent(stats.PAtk, E_StatType.PAtk);
        stats.PDef = ApplyBuffPercent(stats.PDef, E_StatType.PDef);
        stats.MAtk = ApplyBuffPercent(stats.MAtk, E_StatType.MAtk);
        stats.MDef = ApplyBuffPercent(stats.MDef, E_StatType.MDef);
        stats.Speed = ApplyBuffPercent(stats.Speed, E_StatType.Speed);
        stats.Accuracy = ApplyBuffPercent(stats.Accuracy, E_StatType.Accuracy);
        stats.Evasion = ApplyBuffPercent(stats.Evasion, E_StatType.Evasion);
        return stats;
    }

    private int ApplyBuffPercent(int baseValue, E_StatType stat)
    {
        float percent = GetBuffPercent(stat);
        if (Mathf.Approximately(percent, 0f))
            return baseValue;
        return Mathf.Max(1, Mathf.RoundToInt(baseValue * (1f + percent)));
    }

    /// <summary>
    /// 回合开始时推进 Buff 持续时间，到期的自动移除 (由 BattleRoundScheduler 统一调用)
    /// </summary>
    public void TickBuffsAtRoundStart()
    {
        bool changed = false;
        for (int i = _activeBuffs.Count - 1; i >= 0; i--)
        {
            BuffInstance buff = _activeBuffs[i];
            buff.RemainingTurns--;
            if (buff.RemainingTurns > 0)
                continue;

            Debug.Log($"【Buff】{Definition.Name}的{BuffStatUtility.GetDisplayName(buff.Stat)}变化效果消失了");
            _activeBuffs.RemoveAt(i);
            changed = true;
        }

        if (changed)
            EventBus.Publish(new EntityBuffChangedEvent(this));
    }

    /// <summary>
    /// 身上是否已挂有某个技能的全部 Buff 效果 (给敌方 AI 防止重复释放用)
    /// </summary>
    public bool HasAllBuffEffectsOf(SkillData_SO skill)
    {
        if (skill == null || skill.buffEffects.Count == 0)
            return false;

        foreach (BuffEffectConfig effect in skill.buffEffects)
        {
            if (FindBuff(effect.stat, effect.percentBonus < 0f) == null)
                return false;
        }
        return true;
    }

    /// <summary> 清空所有 Buff / Debuff (死亡时调用) </summary>
    public void ClearAllBuffs()
    {
        if (_activeBuffs.Count == 0)
            return;

        _activeBuffs.Clear();
        EventBus.Publish(new EntityBuffChangedEvent(this));
    }

    #endregion

}
