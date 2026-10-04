/// <summary>
/// 回合收尾阶段
///
/// 这个状态主要负责：
/// 1) 让当前行动者从行动点回到原本站位
/// 2) 清理本回合临时状态，比如当前命令和 Boost 表现
/// 3) 再做一次战斗结束判断
/// 4) 最后把流程切回 “选择下一位行动者” 状态
///
/// 可以把它理解成：
/// “这一回合打完之后的统一收尾状态”
/// </summary>
public class TurnEndState : BattleState
{
    public TurnEndState(BattleController controller) : base(controller) { }

    public override IEnumerator Execute()
    {
        // 1. 先取出这一回合刚行动完的单位
        BattleEntity currentEntity = _controller.CurrentEntity;
        
        // 2. 把场景里的 Boost 表现先归零，避免残留到下一位行动者
        _controller.FieldManager.SetBoostVfxLevel(0);
        
        // 3. 若已经满足胜利条件，就先播一个最小胜利停顿，再进入结算
        if (currentEntity.IsPlayer &&
            BattleOutcomeResolver.TryGetBattleEndedEvent(_controller.AllEntities, out BattleEndedEvent endedEvent) &&
            endedEvent.IsWin)
        {
            foreach (var entity in _controller.AllEntities)
            {
                if (entity.IsPlayer)
                    entity.Unit.PlayVictoryAnimation();
            }

            if (_controller.Config.VictoryResultDelay > 0f)
                yield return new WaitForSeconds(_controller.Config.VictoryResultDelay);
            
            _controller.EndBattle(endedEvent);
            yield break;
        }

        // 4. 若当前单位还站在行动点，就让它回到自己的原本站位
        Vector3 homePos = _controller.FieldManager.GetHomePos(currentEntity.Unit);
         if (Vector3.Distance(currentEntity.Unit.transform.position, homePos) > 0.1f)
            yield return _controller.StartCoroutine(currentEntity.Unit.MoveToPosition(homePos, 0.35f));
        
        // 5. 清理这一回合暂存的当前命令
        _controller.CurrentCommand = null;
        
        // 6. 如配置了回合结束停顿，就在这里给一个节奏缓冲
        if (_controller.Config.TurnEndDelay > 0f)
            yield return new WaitForSeconds(_controller.Config.TurnEndDelay);
        
        // 7. 最后清理掉当前行动者，并切回 “选择下一位行动者” 状态
        _controller.CurrentEntity = null;
        _controller.SetState(new SelectNextEntityState(_controller));
    }
}
