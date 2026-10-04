[CreateAssetMenu(menuName = "Battle/Skill Logic/TransferBPLogic")]
public class TransferBPLogic_SO : SkillLogic_SO
{
    public override IEnumerator ExecuteLogic(BattleController controller, BattleEntity actor, BattleCommandRequest command, List<BattleEntity> targets)
    {
        SkillData_SO skill = command.Skill;
        var currentTier = skill.GetBoostTier(command.BPSpend);
        int amountToGive = skill.basePower + currentTier.genericValueBonus;
        
        actor.Unit.PlayUseItemAnimation();
        
        yield return new WaitForSeconds(controller.Config.AttackWindupTime);

        for (int i = 0; i < targets.Count; i++)
        {
            var target = targets[i];

            int beforeBP = target.CurrentBP;
            int finalAmount = Mathf.Min(amountToGive, 5 - beforeBP);
            if (finalAmount > 0)
            {
                target.RuntimeData.ModifyBP(finalAmount);

                EventBus.Publish(new EntityStatChangedEvent(target, E_StatType.CurrentBP, target.RuntimeData.CurrentBP,
                    5));
            }

            controller.SpawnDamagePopup(target, finalAmount, E_DamagePopupType.Gold);
        }
    }
}
