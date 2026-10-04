using Random = UnityEngine.Random;

[CreateAssetMenu(menuName = "Battle/Skill Logic/StealSkillLogic")]
public class StealSkillLogic_SO : SkillLogic_SO
{
    public string EmptyPocketMessage = "敌人兜里空空如也";
    public float lowHPBonusMax = 0.2f;

    private float BreakBonus = 0.15f;
    
    public override IEnumerator ExecuteLogic(BattleController controller, BattleEntity actor, BattleCommandRequest command, List<BattleEntity> targets)
    {
        actor.Unit.PlayAttackAnimation();
        yield return new WaitForSeconds(controller.Config.AttackWindupTime);
        
        SkillData_SO skill = command.Skill;
        var currentTier = skill.GetBoostTier(command.BPSpend);

        BattleEntity target = targets[0];

        if (target.HasBeenRobbed)
        {
            EventBus.Publish(new BattleNotificationEvent(EmptyPocketMessage));
            yield break;
        }
        
        // 1. 先统计当前掉落袋里还剩多少件物品，并把 rarityWeight 汇总起来
        int totalQuantity = 0;
        int totalWeight = 0;
        for (int i = 0; i < target.BattleDrops.Count; i++)
        {
            InventoryItem drop = target.BattleDrops[i];
            if (drop.Quantity <= 0)
                continue;
            
            totalQuantity += drop.Quantity;
            totalWeight += drop.ItemDefinition.RarityWeight * drop.Quantity;
        }

        if (totalQuantity == 0)
        {
            target.RefreshRobbedState();
            EventBus.Publish(new BattleNotificationEvent(EmptyPocketMessage));
            yield break;
        }

        if (totalWeight <= 0)
        {
            EventBus.Publish(new BattleNotificationEvent("偷窃失败"));
            yield break;
        }

        // 概率计算：
        // 1) 先用当前掉落袋里剩余物品的平均 rarityWeight 作为基础成功率
        // 2) 低血量和破盾提供补偿
        // 3) BP Tier 的 chanceBonus 做追加
        // 4) 成功后再按各自 rarityWeight 加权抽出具体偷到哪一件
        
        float baseChance01 = totalWeight / (float)(totalQuantity * 100);
        float chance = CalculateStealChance01(baseChance01, target, currentTier);

        if (Random.value > chance)
        {
            EventBus.Publish(new BattleNotificationEvent("偷取失败"));
            yield break;
        }
        
        // 2. 偷取成功后，再按 rarityWeight 从整包物品里加权抽出本次实际偷到的那一件
        int roll = Random.Range(0, totalWeight);
        int currentWeight = 0;
        InventoryItem targetItemDrop = null;

        for (int i = 0; i < target.BattleDrops.Count; i++)
        {
            InventoryItem drop = target.BattleDrops[i];
            if (drop.Quantity <= 0)
                continue;
            
            currentWeight += drop.ItemDefinition.RarityWeight * drop.Quantity;
            if (roll >= currentWeight)
                continue;
            
            targetItemDrop = drop;
            break;
        }
        
        InventoryManager.Instance.AddItem(targetItemDrop.ItemDefinition,1);
        targetItemDrop.Quantity--;
        target.RefreshRobbedState();

        EventBus.Publish(new BattleNotificationEvent($"偷得：{targetItemDrop.ItemDefinition.ItemName}", true));

    }

    private float CalculateStealChance01(
        float baseChance01,
        BattleEntity target,
        BoostTierConfig tier)
    {
        float hpBonus = 0f;
        
        var stats = target.RuntimeData.GetTotalStats();
        if (stats.MaxHp > 0)
        {
            var hpPercent = Mathf.Clamp01(target.RuntimeData.CurrentHP/stats.MaxHp);
            hpBonus = (1 - hpPercent) * lowHPBonusMax;
        }
        
        float breakBonus = target.IsBroken ? BreakBonus : 0;
        float tierBonus = tier.chanceBonus; 
        
        float finalChance = baseChance01 + hpBonus + tierBonus + breakBonus;
        
        return Mathf.Clamp01(finalChance);
    }

}
