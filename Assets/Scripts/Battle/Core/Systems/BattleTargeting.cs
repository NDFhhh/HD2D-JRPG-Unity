/// <summary>
/// 战斗目标规则工具
///
/// 这个工具类的目的，是把“目标相关规则”集中到一个地方统一管理
/// 1. 某种目标类型要不要进入目标选择状态
/// 2. 某种目标类型应该筛选出哪些候选目标
/// 3. 玩家或 AI 选中的实体，最后要怎么转换回命令里的目标请求
///
/// 这样 PlayerInputState、TargetSelectionState、EnemyAIState 和执行层
/// 就不用各写一套“谁是敌人、谁是队友、单体和群体怎么分”的重复代码
/// 
/// 可以把它理解成：
/// “战斗里所有目标规则的统一工具箱”
/// </summary>
public static class BattleTargeting
{
    public static List<BattleEntity> GetAliveTargetByType(BattleEntity self, E_TargetType targetType,
        List<BattleEntity> allEntities)
    {
        return targetType switch
        {
            E_TargetType.SingleAlly or E_TargetType.AllAllies
                => allEntities.FindAll(entity => entity.IsAlive && entity.IsPlayer == self.IsPlayer),
            _ => allEntities.FindAll(entity => entity.IsAlive && entity.IsPlayer != self.IsPlayer)
        };
    }

    public static List<BattleEntity> BuildExecutionTargets(BattleController controller)
    {
        BattleEntity actor = controller.CurrentEntity;
        BattleCommandRequest command = controller.CurrentCommand;
        List<BattleEntity> allEntities = controller.AllEntities;

        switch (command.Type)
        {
            case E_BattleCommandType.Attack:
            case E_BattleCommandType.Skill:
                var targetType = command.Skill.targetType;
                if (targetType is E_TargetType.SingleAlly or E_TargetType.SingleEnemy)
                {
                    var target = allEntities.Find(entity => entity.IsAlive && entity.ID == command.TargetEntityID);
                    return target != null ? new List<BattleEntity>() { target } : new List<BattleEntity>();
                }
                return GetAliveTargetByType(actor, targetType, allEntities);
            
            case E_BattleCommandType.Item:
                var itemTarget = allEntities.Find(entity => entity.IsAlive && entity.ID == command.TargetEntityID);
                return itemTarget != null ? new List<BattleEntity>() { itemTarget } : new List<BattleEntity>();
            
            case E_BattleCommandType.Defend:
            case E_BattleCommandType.Escape:
                return new List<BattleEntity>() { actor };
        }

        return new List<BattleEntity>();
    }
}