/// <summary>
/// 执行行动状态
///
/// 这个状态主要负责：
/// 1) 读取当前行动者和当前指令
/// 2) 直接把命令分发给对应 Handler
/// 3) 执行完后刷新时间轴，并进入回合收尾状态
///
/// 可以把它理解成：
/// “真正把这一回合命令打出去的状态”
/// </summary>
public class PerformActionState : BattleState
{
    private AttackSkillCommandHandler AttackSkillHandler = new();
    private DefendCommandHandler DefendHandler = new();
    private EscapeCommandHandler EscapeHandler = new();
    private ItemCommandHandler ItemHandler = new();
    
    public PerformActionState(BattleController controller) : base(controller) { }

    public override IEnumerator Execute()
    {
        BattleEntity entity = _controller.CurrentEntity;
        BattleCommandRequest command = _controller.CurrentCommand;

        switch (command.Type)
        {
            case E_BattleCommandType.Attack:
            case E_BattleCommandType.Skill:
                yield return AttackSkillHandler.Execute(_controller);
                break;
            case E_BattleCommandType.Item:
                yield return ItemHandler.Execute(_controller);
                break;
            case E_BattleCommandType.Defend:
                yield return DefendHandler.Execute(_controller);
                break;
            case E_BattleCommandType.Escape:
                yield return EscapeHandler.Execute(_controller);
                break;
        }
        
        _controller.UpdateTimelinePrediction();
        _controller.SetState(new TurnEndState(_controller));
    }
}
