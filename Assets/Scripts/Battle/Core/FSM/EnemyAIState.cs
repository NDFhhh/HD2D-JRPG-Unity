using Random = UnityEngine.Random;

public class EnemyAIState : BattleState
{
    private class ActionDecision
    {
        public SkillData_SO SelectedSkill;
        public bool IsTelegraph;
        public float Weight;
    }

    public EnemyAIState(BattleController controller) : base(controller) { }

    public override IEnumerator Execute()
    {
        // 1. 先处理当前敌人是否有挂起的 BOSS 阶段切换提示
        yield return PlayPendingBossPhaseIntro();
        // 2. 再正式让 AI 给自己组出一条 BattleCommandRequest
        _controller.CurrentCommand = buildAICommand();
        // 3. 稍微等待一下，模拟敌人思考的节奏感停顿
        yield return new WaitForSeconds(_controller.Config.AIThinkDuration);
        // 4. 最后切到执行状态，真正把命令打出去
        _controller.SetState(new PerformActionState(_controller));
    }

    private IEnumerator PlayPendingBossPhaseIntro()
    {
        BattleEntity actor = _controller.CurrentEntity;

        // 1. 先尝试把挂起的 Boss 阶段正式应用掉
        if (!actor.TryApplyPendingBossPhase(out BossPhaseConfig appliedPhase))
            yield break;

        // 2. 应用成功后，先发战斗提示文案（显示总时长与阶段音效时长保持一致）
        float soundDuration = appliedPhase.ResolveSoundDuration();
        EventBus.Publish(new BattleNotificationEvent(appliedPhase.promptText, displayDuration: soundDuration));

        // 3. 等待阶段演出结束再行动：提示延迟与阶段音效时长取较大者
        //    保证音效播完才进入下一个战斗状态
        float wait = Mathf.Max(appliedPhase.introDelay, soundDuration);
        if (wait > 0)
            yield return new WaitForSeconds(wait);
    }

    private BattleCommandRequest buildAICommand()
    {
        BattleEntity actor = _controller.CurrentEntity;

        // 1. 如果这个敌人上一回合已经进入了蓄力状态
        //    这一回合就直接释放那招准备中的技能
        if (actor.PreparedSkill is not null)
            return BuildPreparedSkillCommand();

        // 2. 否则先做一次常规行动候选评估
        ActionDecision decision = EvaluateActions();
        
        // 3. 若这次选中的是 “蓄力型技能”，这一回合先进入蓄力准备
        if (decision.IsTelegraph)
            return BuildTelegraphCommand(decision.SelectedSkill);

        // 4. 最后退回普通技能 / 普通命令
        return BuildCommand(decision.SelectedSkill);
    }

    private BattleCommandRequest BuildCommand(SkillData_SO selectedSkill)
    {
        BattleCommandRequest command = selectedSkill == _controller.CurrentEntity.Definition.BasicAttack
            ? global::BattleCommandRequest.CreateAttack(_controller.CurrentEntity)
            : global::BattleCommandRequest.CreateSkill(selectedSkill);
        
        AutoTargetSelection(command);
        return command;
    }

    #region 蓄力技能

    private BattleCommandRequest BuildTelegraphCommand(SkillData_SO skill)
    {
        var actor = _controller.CurrentEntity;
        
        // 1. 先把这招记录为 “下回合要释放的准备技能”
        string skillName = skill.skillName;
        actor.PreparedSkill = skill;
        
        // 2. 再立刻播放头顶蓄力特效
        actor.Unit.PlayTelegraphVfx(skill);
        
        // 3. 然后用技能提示和战斗通知告诉玩家 “敌人正在蓄力”
        //    蓄力回合只显示技能名，不播咏唱音效，音效等下回合技能真正打出时再播
        EventBus.Publish(new BattleNotificationEvent($"敌人正在蓄力"));
        EventBus.Publish(new SkillNameDisplayEvent(actor, skillName, skill.castSound, playCastSound: false));

        // 4. 最后返回一个 “防御” 命令，让敌人这一回合先不行动
        return BattleCommandRequest.CreateDefend();
    }

    private BattleCommandRequest BuildPreparedSkillCommand()
    {
        var actor = _controller.CurrentEntity;
        
        // 1. 先取出这次真正要释放的蓄力攻击
        var skillToCast = actor.PreparedSkill;
        var skillName = skillToCast.skillName;
        
        // 2. 释放前先清掉 “准备中” 状态和头顶蓄力特效
        actor.PreparedSkill = null;
        actor.Unit.StopTelegraphVfx();
        
        // 3. 再补一条战斗通知，告诉玩家这招现在正式打出来了
        //    技能名提示与咏唱音效由 AttackSkillCommandHandler 在出手瞬间统一发布，这里不再重复
        EventBus.Publish(new BattleNotificationEvent($"{actor.Definition.Name} 释放了 {skillName}"));
        
        // 4. 最后退后普通攻击 / 普通命令 
        return BuildCommand(skillToCast);
    }

    #endregion

    private SkillData_SO ChooseFirstAvailableSkill(BattleEntity actor)
    {
        if (actor.Definition.InitialSkills != null)
        {
            for (int i = 0; i < actor.Definition.InitialSkills.Count; i++)
            {
                var skill = actor.Definition.InitialSkills[i];
                if (skill.spCost <= actor.CurrentSP)
                    return skill;
            }
        }

        return actor.Definition.BasicAttack;
    }

    private void AutoTargetSelection(BattleCommandRequest command)
    {
        var skill = command.Skill;

        // 1. 群体 和 self 目标不需要再选具体目标，清掉 TargetEntityID 即可
        if (skill.targetType is not (E_TargetType.SingleAlly or E_TargetType.SingleEnemy))
        {
            command.TargetEntityID = null;
            return;
        }
        
        // 2. 单体目标才需要真的挑一个合法候选
        var candidates = BattleTargeting.GetAliveTargetByType(
            _controller.CurrentEntity,
            skill.targetType,
            _controller.AllEntities);
        
        if (candidates.Count == 0) return;
        
        // 3. 单体治疗 优先挑血量最低的队友，其它情况先随机一个合法目标
        bool isSingleAllyHeal = skill.targetType == E_TargetType.SingleAlly && skill.skillType == E_SkillType.Heal;
        
        BattleEntity target = isSingleAllyHeal 
            ? GetLowestHpTarget(candidates) 
            : candidates[Random.Range(0, candidates.Count)];
        
        command.TargetEntityID = target.ID;
    }

    private BattleEntity GetLowestHpTarget(List<BattleEntity> candidates)
    {
        var bestTarget = candidates[0];
        float bestRatio = bestTarget.CurrentHP / bestTarget.TotalStats.MaxHp;

        for (int i = 1; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            float ratio = candidate.CurrentHP / candidate.TotalStats.MaxHp;
            
            if (ratio > bestRatio)
                continue;
            
            bestTarget = candidate;
            bestRatio = ratio;
        }
        
        return bestTarget;
    }

    /// <summary>
    /// 评估敌方当前可用的所有行动，并打分
    /// </summary>
    private ActionDecision EvaluateActions()
    {
        var actor = _controller.CurrentEntity;
        CharacterDefinition_SO def = actor.Definition;
        BossPhaseConfig phase = actor.GetActivePhaseConfig();
        EnemyAITuningConfig tuning = ((EnemyDefinition_SO)def).AiTurning;

        List<ActionDecision> candidates = new()
        {
            new ActionDecision
            {
                SelectedSkill = def.BasicAttack,
                IsTelegraph = false,
                Weight = ApplyPhaseWeight(tuning.basicAttackWeight,
                    phase is not null ? phase.basicAttackWeightMultiplier : 1)
            }
        };

        int currentSP = actor.CurrentSP;
        for (int i = 0; i < def.InitialSkills.Count; i++)
        {
            SkillData_SO skill = def.InitialSkills[i];
            if (skill.spCost >= currentSP)
                continue;

            float weight = 0f;
            bool isTelegraph = false;

            switch (skill.skillType)
            {
                case E_SkillType.Damage:
                    if (IsTelegraphSkill(skill, tuning))
                    {
                        isTelegraph = true;
                        weight = ShouldPreferTelegraphSkill(tuning)
                            ? ApplyPhaseWeight(tuning.telegraphWeight,
                                phase != null ? phase.telegraphWeightMultiplier : 1)
                            : ApplyPhaseWeight(tuning.damageSkillWeight,
                                phase != null ? phase.damageWeightMultiplier : 1);
                    }
                    break;
                case E_SkillType.Heal:
                    weight = EvaluateHealWeight(skill, tuning);
                    weight = ApplyPhaseWeight(weight, phase != null ? phase.healWeightMultiplier : 1);
                    break;
                case E_SkillType.Buff:
                    // 自己身上已经挂着这套 Buff 时不再重复释放，避免 AI 原地空转
                    // (对队友释放的群体 Buff 也用施法者自身做近似判断)
                    weight = actor.HasAllBuffEffectsOf(skill) ? 0f : tuning.defaultSkillWeight;
                    break;
                case E_SkillType.Debuff:
                    weight = tuning.defaultSkillWeight;
                    break;
            }

            if (weight > 0)
            {
                candidates.Add(new ActionDecision
                {
                    SelectedSkill = skill,
                    IsTelegraph = isTelegraph,
                    Weight = weight
                });
            } 
        }
        
        return PickByWeight(candidates);
    }

    private float ApplyPhaseWeight(float baseWeight, float multiplier)
    {
        return baseWeight * multiplier;
    }

    private static bool IsTelegraphSkill(SkillData_SO skill, EnemyAITuningConfig tuning)
        => skill.basePower >= tuning.telegraphMinBasePower
           && skill.spCost >= tuning.telegraphMinSpCost;

    /// <summary>
    /// 是否进入 “优先蓄力大招” 阶段
    /// 说明：这里只决定权重提升，不决定技能是否需要先蓄力
    /// 只要技能被认定为蓄力技，被选中后都会先进入蓄力回合
    /// </summary>
    private bool ShouldPreferTelegraphSkill(EnemyAITuningConfig tuning)
    {
        BattleEntity actor = _controller.CurrentEntity;
        return actor.CurrentHP / (float)actor.TotalStats.MaxHp <= tuning.telegraphHpRatioThreshold;
    }

    /// <summary>
    /// 精准评估治疗技能的权重
    /// </summary>
    private float EvaluateHealWeight(SkillData_SO skill, EnemyAITuningConfig tuning)
    {
        int lowHpCount = 0;
        foreach (var ally in _controller.AllEntities)
        {
            if (!ally.IsPlayer && ally.IsAlive
                               && ally.CurrentHP / (float)ally.TotalStats.MaxHp <= tuning.healLowHpRatioThreshold)
                lowHpCount++;
        }
        
        if (lowHpCount <= 0)
                return 0;

        float weight = tuning.healBaseWeight + lowHpCount + tuning.healPerLowHpBonus;
        return skill.targetType == E_TargetType.SingleAlly && lowHpCount > 1
            ? weight * tuning.singleHealMultiLowHpPenalty
            : weight;
        
    }

    private ActionDecision PickByWeight(List<ActionDecision> candidates)
    {
        float totalWeight = 0f;
        foreach (var candidate in candidates)
            totalWeight += candidate.Weight;

        float randomWeight = Random.Range(0f, totalWeight);

        foreach (var candidate in candidates)
        {
            randomWeight -= candidate.Weight;
            if (randomWeight <= 0f)
                return candidate;
        }

        return candidates[^1];
    }
}
