/// <summary>
/// 时间轴预测节点
///
/// 这个结构体的目的很简单：
/// 把调度器调好的结果，整理成 UI 可以直接消费的一份最小数据
/// 这样 BattleTimelineUI 不需要直接依赖 BattleRoundScheduler 的内部列表
/// </summary>
public readonly struct BattleTimelinePredictionNode
{
    // UniqueID 用来保证同一角色跨回合预测时，图标也能稳定复用
    public readonly string UniqueID;
    // 这条预测对应的是谁
    public readonly BattleEntity Entity;
    // Round = 0 表示当前回合，Round = 1 表示下一回合预测
    public readonly int Round;

    public BattleTimelinePredictionNode(string uniqueID, BattleEntity entity, int round)
    {
        UniqueID = uniqueID;
        Entity = entity;
        Round = round;
    }
}
