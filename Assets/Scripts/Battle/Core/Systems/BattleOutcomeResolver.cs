using Random = UnityEngine.Random;

public static class BattleOutcomeResolver
{
    /// <summary>
    /// 尝试生成战斗结束事件
    /// 这个函数只看 “当前还活着哪些单位”，不回头追历史回合数据
    /// 只要双方都有存货单位，战斗就继续；只要一方被清空，就直接产出结束事件
    /// </summary>
    /// <param name="entities">当前战场实体快照</param>
    /// <param name="endedEvent">输出的战斗结束事件</param>
    /// <returns>可结束时返回 true；战斗仍在进行返回 false</returns>
    public static bool TryGetBattleEndedEvent(List<BattleEntity> entities, out BattleEndedEvent endedEvent)
    {
        #region 胜负判定

        // 1. 先扫一遍当前存活单位
        //    这里只看战场快照里的 “活人”，不关心谁是上一帧刚被打倒的
        bool hasPlayers = false;
        bool hasEnemies = false;

        for (int i = 0; i < entities.Count; i++)
        {
            BattleEntity entity = entities[i];
            if (!entity.IsAlive)
                continue;

            // 2. 只要存活单位里出现玩家或敌人，就先记个标记
            if (entity.IsPlayer)
                hasPlayers = true;
            else
                hasEnemies = true;


            // 3. 双方都还有人活着，说明战斗还没有结束
            //    这里可以提前返回，避免继续扫描后面的实体
            if (hasPlayers && hasEnemies)
            {
                endedEvent = default;
                return false;
            }
        }

        // 4. 玩家侧已经没人了，直接判负
        //    失败分支不需要奖励，所以这里不继续算经验、金币和掉落
        if (!hasPlayers)
        {
            endedEvent = new BattleEndedEvent(false);
            return true;
        }

        #endregion

        #region 胜利奖励汇总

        // 5. 能走到这里，说明敌方已经被清空，玩家胜利
        //    接下来把本场胜利奖励一次性收成到结束事件里，
        //    后面的相机和 UI 只需要消费这一份数据就行
        int exp = 0;
        int money = 0;
        Dictionary<ItemDefinition_SO, int> dropQuantityMap = new();

        for (int i = 0; i < entities.Count; i++)
        {
            BattleEntity entity = entities[i];
            if (entity.IsPlayer)
                continue;

            // 6. 经验和金币直接读敌人的静态定义
            //    这部分是固定结算，不受战斗过程中的临时转状态影响
            EnemyDefinition_SO enemyDef = (EnemyDefinition_SO)entity.Definition;
            exp += enemyDef.ExpReward;
            money += enemyDef.MoneyReward;

            // 7. 掉落则读每场战斗独立生成的运行时掉落。
            // 这样偷窃、消耗或其他战斗内变化，都会保留到结算时。
            List<InventoryItem> drops = entity.BattleDrops;
            for (int j = 0; j < drops.Count; j++)
            {
                InventoryItem dropItem = drops[j];
                if (dropItem.Quantity <= 0)
                    continue;
                ItemDefinition_SO itemDefinition = dropItem.ItemDefinition;

                // 8. 稀有度权重折算成 0~1 的掉落概率。
                // 这里仍然由结算时随机决定是否保留。
                float chance01 = Mathf.Clamp01(itemDefinition.RarityWeight / 100f);
                if (Random.value > chance01)
                    continue;
                int addQuantity = dropItem.Quantity;

                // 9. 同一个物品只累加数量,不重复生成条目
                if (dropQuantityMap.TryGetValue(itemDefinition, out int quantity))
                    dropQuantityMap[itemDefinition] = quantity + addQuantity;
                else
                    dropQuantityMap.Add(itemDefinition, addQuantity);
            }

        }

        // 10. 最后把字典转成列表,方便事件携带和 UI 直接遍历。
        List<BattleDropReward> result = new(dropQuantityMap.Count);
        foreach (KeyValuePair<ItemDefinition_SO, int> pair in dropQuantityMap)
            result.Add(new BattleDropReward(pair.Key, pair.Value));
        endedEvent = new BattleEndedEvent(true, exp, money, result);
        return true;
        
        #endregion
    }

}
