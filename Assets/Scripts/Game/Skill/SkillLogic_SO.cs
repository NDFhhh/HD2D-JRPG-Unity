/// <summary>
/// 技能逻辑策略基类
/// 作用：将特殊技能逻辑 (如召唤，BP传送，变身) 从 Handler 中剥离出来
/// </summary>
public abstract class SkillLogic_SO : ScriptableObject
{
    /// <summary>
    /// 执行特殊逻辑
    /// </summary>
    /// <param name="context">战斗上下文(包含施法者，控制器)</param>
    /// <param name="targets">已选定的目标列表</param>
    /// <param name="skillData">技能本身的静态数据(用于读取威力等参数)</param>
    /// <returns></returns>
    public abstract IEnumerator ExecuteLogic(
        BattleController controller, 
        BattleEntity actor,
        BattleCommandRequest Command,
        List<BattleEntity> targets);
}
