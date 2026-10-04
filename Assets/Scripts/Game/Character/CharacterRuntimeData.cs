[Serializable]
public class CharacterRuntimeData
{
     public CharacterDefinition_SO Definition;

     public int Level;
     public int CurrentHP;
     public int CurrentSP;
     public int CurrentBP;
     public int CurrentExp;
     
     public string Name => Definition.Name;
     
     public StatBlock EquipmentStats;
     
     private bool hasAppliedInitialEquipment;    //初始装备是否成功
     
     [Serializable]
     public class EquippedItemEntry
     {
         public E_EquipSlot slot;
         public EquipmentItem_SO item;
     }
     
     public List<EquippedItemEntry> EquippedItems = new();
     
     public CharacterRuntimeData(CharacterDefinition_SO definition)
     {
         Definition = definition;
         EquipmentStats = StatBlock.Zero;
         Level = definition.BaseLevel;
         
         ApplyInitialEquipment();
         
         StatBlock stats = GetBaseStats();
         CurrentHP = stats.MaxHp;
         CurrentSP = stats.MaxSp;
         CurrentBP = 0;
     }

     public StatBlock GetBaseStats()
     {
         if (Definition is AllyDefinition_SO allyDefinition)
             return allyDefinition.GetStatForLevel(Level);
         if (Definition is EnemyDefinition_SO enemyDefinition)
             return enemyDefinition.BaseStats;

         return Definition != null ? Definition.BaseStats : StatBlock.Zero;
     }
     
     public StatBlock GetTotalStats() => GetBaseStats() + EquipmentStats;

     #region 数据变化接口

     public void HealFull()
     {
         CurrentHP = GetTotalStats().MaxHp;
         CurrentSP = GetTotalStats().MaxSp;
     }

     public void ModifyHP(int amount)
     {
         CurrentHP += amount;
         CurrentHP = Mathf.Clamp(CurrentHP, 0, GetTotalStats().MaxHp);
     }
     
     public void ModifySP(int amount)
     {
         CurrentSP += amount;
         CurrentSP = Mathf.Clamp(CurrentSP, 0, GetTotalStats().MaxSp);
     }

     public void ModifyBP(int amount)
     {
         CurrentBP = Mathf.Clamp(CurrentBP + amount, 0, 5);
     }

     public void ResetBattleBP()
     {
         CurrentBP = 0;
     }

     #endregion

     #region 装备系统

     public void ApplyInitialEquipment()
     {
         if (hasAppliedInitialEquipment)
             return;
         
         AllyDefinition_SO allyDef = Definition as AllyDefinition_SO;
         if (allyDef is null || allyDef.InitialEquipment is null || allyDef.InitialEquipment.Count == 0)
         {
             hasAppliedInitialEquipment = true;
             return;
         }

         if (InventoryManager.Instance is null) return;
         
         for (int i = 0; i < allyDef.InitialEquipment.Count; i++)
         {
             var entry = allyDef.InitialEquipment[i];
             var item = entry.item;
             
             if (item is null) continue;
             
             // 装备物品
             SetEquippedItem(entry.slot, item);
             
             InventoryManager.Instance.AddItem(item, 1);
         }
         
         hasAppliedInitialEquipment = true;
     }

     public void SetEquippedItem(E_EquipSlot slot, EquipmentItem_SO item)
     {
         var entry = EquippedItems.Find(e => e.slot == slot);

         if (entry != null)
         {
             if (item == null)
                EquippedItems.Remove(entry);
             else
                 entry.item = item;
         }
         else
         {
             EquippedItems.Add(new EquippedItemEntry(){slot = slot, item = item});
         }
         
         RebuildEquipmentStats();
     }

     public EquipmentItem_SO GetEquippedItem(E_EquipSlot slot)
     {
         var entry = EquippedItems.Find(entry => entry != null && entry.slot == slot);
         
         return entry?.item;
     }

     public int GetEquippedItemCount(ItemDefinition_SO targetItem)
     {
         if (targetItem is null) return 0;
         
         int count = 0;
         foreach (var entry in EquippedItems)
         {
             if (entry != null && entry.item == targetItem)
                 count++;
         }
         return count;
     }

     public void RebuildEquipmentStats()
     {
         var merged = StatBlock.Zero;
         for (int i = EquippedItems.Count - 1; i >= 0; i--)
         {
             var entry = EquippedItems[i];
             if (entry.item is null)
             {
                 EquippedItems.RemoveAt(i);
                 continue;
             }
             merged += entry.item.statBonus;
         }
         
         EquipmentStats = merged;
         
         var total = GetTotalStats();
         CurrentHP = Math.Min(CurrentHP, total.MaxHp);
         CurrentSP = Math.Min(CurrentSP, total.MaxSp);
     }

     #endregion

     #region 挑战战力评估

     public static int EvaluatePowerFromStats(StatBlock stats)
     {
         float score =
             stats.MaxHp * 0.2f +
             stats.MaxSp * 0.1f +
             stats.PAtk * 1.5f +
             stats.MAtk * 1.5f +
             stats.PDef * 1.0f +
             stats.MDef * 1.0f +
             stats.Speed * 1.2f;

         return Mathf.Max(1, Mathf.RoundToInt(score));
     }

     #endregion

     #region 经验成长与升级

     public int GetExpRequiredToNextLevel()
     {
         // 队伍成员才会走成长经验，这里直接读取盟友成长配置
         return ((AllyDefinition_SO)Definition).GetExpRequiredToNextLevel(Level);
     }

     public float GetExpProgress01()
     {
         int targetExp = GetExpRequiredToNextLevel();
         if (targetExp == 0)
             return 1f;

         return CurrentExp / (float)targetExp;
     }

     /// <summary>
     /// 添加经验并处理升级，返回实际应用的经验值
     /// </summary>
     public int AddExp(int amount)
     {
         int remainingExp = amount;

         AllyDefinition_SO allyDef = (AllyDefinition_SO)Definition;

         int appliedExp = 0;    // 实际应用的经验值
         bool leveledUp = false;

         while (remainingExp > 0)
         {
             int targetExp = allyDef.GetExpRequiredToNextLevel(Level);    // 获得当前等级所需的经验值
             if (targetExp == 0)
             {
                 CurrentExp = 0;
                 break;
             }
             
             int need = targetExp - CurrentExp;    // 计算当前经验值与目标经验值之间的差值
             int gain = Mathf.Min(need, remainingExp);
             
             CurrentExp += gain;    // 增加经验
             remainingExp -= gain;    // 减少剩余经验值
             appliedExp += gain;    // 增加实际应用的经验值

             if (CurrentExp >= targetExp)
             {
                 Level++;
                 leveledUp = true;
                 CurrentExp = 0;    // 减少经验值，使其不超过当前等级所需的经验值
             }
         }

         if (leveledUp)
             HealFull();
         
         return appliedExp;  
     }

     #endregion
}
