public class DefendCommandHandler : BattleCommandHandlerBase
{
    protected override IEnumerator ExecutionPhase()
    {
        BattleEntity entity = Actor;
        
        // 1. 进入减伤姿态 (直到下一次轮到他行动前生效)
        entity.EnterDefendStance();
        
        // 2. 普通防御才触发 “下一回合抢先”
        yield break;
    }
}
