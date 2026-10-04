/// <summary>
/// 一次战斗指令的数据载体(由 UI 组装，交给执行状态消费)
/// </summary>
public class BattleCommandRequest
{
    /// <summary> 指令类型：Attack / Skill / Item / Defend / Escape </summary>
    public E_BattleCommandType Type;
    public SkillData_SO Skill;
    public ItemDefinition_SO ItemDefinition;
    public string TargetEntityID;
    public int BPSpend;

    public static BattleCommandRequest CreateAttack(BattleEntity actor, int bpSpeed = 0)
    {
        return new BattleCommandRequest
        {
            Type = E_BattleCommandType.Attack,
            Skill = actor.Definition.BasicAttack,
            BPSpend = bpSpeed
        };
    }

    public static BattleCommandRequest CreateSkill(SkillData_SO skill, int bpSpeed = 0)
    {
        return new BattleCommandRequest
        {
            Type = E_BattleCommandType.Skill,
            Skill = skill,
            BPSpend = bpSpeed
        };
    }

    public static BattleCommandRequest CreateItem(ItemDefinition_SO item)
    {
        return new BattleCommandRequest
        {
            Type = E_BattleCommandType.Item,
            ItemDefinition = item,
            TargetEntityID = null
        };
    }
    
    public static BattleCommandRequest CreateDefend()
    {
        return new BattleCommandRequest
        {
            Type = E_BattleCommandType.Defend,
            TargetEntityID = null
        };
    }
    
    public static BattleCommandRequest CreateEscape()
    {
        return new BattleCommandRequest
        {
            Type = E_BattleCommandType.Escape,
            TargetEntityID = null
        };
    }
    
}
