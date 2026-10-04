public abstract class BattleCommandHandlerBase
{
    protected BattleController Controller { get; set; }
    protected BattleEntity Actor => Controller.CurrentEntity;
    protected BattleCommandRequest Command => Controller.CurrentCommand;

    #region 入口

    public virtual IEnumerator Execute(BattleController controller)
    {
        Controller = controller;
         
        if (!PreparePhase())
            yield break;
        
        yield return AnimationPhase();
        yield return ExecutionPhase();
        yield return ResolvePhase();
    }

    #endregion
    
    #region 四阶段扩展点

    /// <summary>
    /// 阶段1：参数校验，资源扣除，目标解析等前置准备
    /// </summary>
    protected virtual bool PreparePhase() => true;

    /// <summary>
    /// 阶段2：动作演出 (可选)
    /// </summary>
    protected virtual IEnumerator AnimationPhase() { yield break; }
    
    /// <summary>
    /// 阶段3：核心效果结算 (伤害，治疗，Buff 等)
    /// </summary>
    protected virtual IEnumerator ExecutionPhase() { yield break; }
    
    /// <summary>
    /// 阶段4：收尾 (等待恢复，清理临时状态等)
    /// </summary>
    protected virtual IEnumerator ResolvePhase() { yield break; }

    #endregion
}
