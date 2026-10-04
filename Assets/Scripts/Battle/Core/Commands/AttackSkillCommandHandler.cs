/// <summary>
/// Attack / Skill 共用处理器
///
/// 设计目标：
/// 1) Attack 与 Skill 保留两种命令类型，但统一走同一条处理器
/// 2) 资源扣除，日志，目标解析等前置流程集中管理，避免代码重复
/// </summary>
public class AttackSkillCommandHandler : BattleCommandHandlerBase
{
    private List<BattleEntity> _targets = new();
    private AttackSkillExecutionEngine _executionEngine = new();

    protected override bool PreparePhase()
    {
        // 1. 每次进来先清空运行时状态，避免上一条命令的残留目标目标污染本次执行
        _targets.Clear();

        // 2. BP 这里直接做真实消耗，并标记“本回合确实用过 BP”
        if (Command.BPSpend > 0)
        {
            Actor.SpendBP(Command.BPSpend);
            Actor.MarkBPUsed();
        }

        // 3. 如果是技能就要扣 SP
        if (Command.Type == E_BattleCommandType.Skill)
        {
            Actor.SpendSP(Command.Skill.spCost);
        }
        
        Debug.Log($"【AttackDebug】 Name={Actor.Definition.Name} Skill={Command.Skill.skillName} TargetID={Command.TargetEntityID}");
        
        // 4. 在真正执行前，统一构建出这条命令的最终目标列表
        _targets.AddRange(BattleTargeting.BuildExecutionTargets(Controller));
        
        // 5. 显示技能名字（附带技能专属咏唱音效）
        EventBus.Publish(new SkillNameDisplayEvent(Actor, Command.Skill.skillName, Command.Skill.castSound));
        
        return true;
    }

    /// <summary>
    /// 核心结算阶段交给执行引擎
    /// </summary>
    protected override IEnumerator ExecutionPhase()
    {
        return _executionEngine.Execute(Controller, _targets);
    }

    /// <summary>
    /// 收尾阶段统一等待恢复时间
    /// </summary>
    protected override IEnumerator ResolvePhase()
    {
        float recovery = Controller.Config.AttackRecoveryTime;
        yield return new WaitForSeconds(recovery);
    }
}