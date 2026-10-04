public class ItemCommandHandler : BattleCommandHandlerBase
{
    private ConsumableItem_SO _consumableItem;
    private BattleEntity _target;
    private bool _isHpItem;

    protected override bool PreparePhase()
    {
        _consumableItem = (ConsumableItem_SO)Command.ItemDefinition;
        _target = Controller.AllEntities.Find(e => e.ID == Command.TargetEntityID);
        _isHpItem = _consumableItem.ItemIconKey == E_ItemIconKey.Healing;
        return _target != null;
    }

    protected override IEnumerator AnimationPhase()
    {
        // TODO 动画
        Actor.Unit.PlayUseItemAnimation();
        
        float windup = Controller.Config.AttackWindupTime;
        if (windup > 0f)
            yield return new WaitForSeconds(windup);
    }

    protected override IEnumerator ExecutionPhase()
    {
        int restoreAmount = _consumableItem.restoreAmount;
        InventoryManager.Instance.RemoveItem(_consumableItem, 1);
        
        if (_isHpItem)
        {
            _target.Heal(restoreAmount);
            Controller.SpawnDamagePopup(_target, restoreAmount, E_DamagePopupType.Heal);
        }
        else
        {
            _target.RestoreSP(restoreAmount);
            Controller.SpawnDamagePopup(_target, restoreAmount, E_DamagePopupType.Magic);
        }
        yield break;
    }

    protected override IEnumerator ResolvePhase()
    {
        float recovery = Controller.Config.AttackRecoveryTime;
        if (recovery > 0f)
            yield return new WaitForSeconds(recovery);
    }
}
