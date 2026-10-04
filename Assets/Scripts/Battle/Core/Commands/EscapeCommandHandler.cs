public class EscapeCommandHandler : BattleCommandHandlerBase
{
    /// <summary>
    /// 执行逃跑
    /// 1. 先清理当前目标选择，避免退场时还残留选中状态
    /// 2. 再让幸存者单位执行撤退演出
    /// 3. 演出结束后请求回到战斗前场景
    /// 4. 回切请求发出后，立刻停止当前战斗状态机
    /// </summary>
    public override IEnumerator Execute(BattleController controller)
    {
        // 1. 先把当前目标清掉，避免逃跑时还保留锁定状态
        controller.ClearTargetSelection();
        
        // 2. 让还活着的单位先按配置时间撤回出生点
        yield return RunEscape(controller);

        // 音频：逃跑成功音效
        EventBus.Publish(new BattleEscapedEvent());

        // 3. 请求回切到战斗前场景
        BattleService.Instance.ReturnToPreviousScene();
        
        // 4. 回切请求发出后，立刻停止当前战斗状态机
        controller.StopBattle();
    }
    
    private IEnumerator RunEscape(BattleController controller)
    {
        // 1. 读取逃跑移动时长，后面所有幸存单位都用同一段时间退场。
        float escapeDuration = controller.Config.EscapeRunDuration;

        // 2. 逐个检查战场单位，只处理还活着且确实需要移动的对象。
        bool hasMover = false;
        foreach (BattleEntity entity in controller.AllEntities)
        {
            if (!entity.IsAlive || !entity.IsPlayer)
                continue;

            var initPos = controller.FieldManager.GetInitPos();
            if (Vector3.Distance(entity.Unit.transform.position, initPos) <= 0.01f)
                continue;

            hasMover = true;
            // 3. 让单位各自朝出生点移动，演出时间统一由配置控制。
            controller.StartCoroutine(entity.Unit.MoveToPosition(initPos, escapeDuration));
        }

        // 4. 只有真的有人在移动时，才等待这段退场时间。
        if (hasMover)
            yield return new WaitForSeconds(escapeDuration);

        // 5. 再补一段退出延迟，让退场和回场之间的节奏更顺。
        float exitDelay = controller.Config.EscapeExitDelay;
        if (exitDelay > 0f)
            yield return new WaitForSeconds(exitDelay);
    }

}
