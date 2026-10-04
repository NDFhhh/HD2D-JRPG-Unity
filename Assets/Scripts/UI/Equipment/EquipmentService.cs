
public static class EquipmentService
{
    public static List<EquipmentItem_SO> BuildCandidates(InventoryManager inventory, E_EquipSlot slot)
    {
        List<EquipmentItem_SO> result = new() { null };

        for (int i = 0; i < inventory.CurrentInventory.Count; i++)
        {
            var item = inventory.CurrentInventory[i];
            if (item == null || item.Quantity <= 0)
                continue;

            if (item.ItemDefinition is not EquipmentItem_SO equipmentItem)
                continue;

            if (!IsSlotCompatible(equipmentItem, slot))
                continue;
            
            result.Add(equipmentItem);
        }
        
        return result;
    }

    private static bool IsSlotCompatible(EquipmentItem_SO item, E_EquipSlot targetSlot)
    {
        if (item == null)
            return false;

        return item.category switch
        {
            E_EquipmentCategory.Weapon => targetSlot == (E_EquipSlot)((int)item.weaponType - 1),
            E_EquipmentCategory.Shield => targetSlot == E_EquipSlot.Shield,
            E_EquipmentCategory.Head => targetSlot == E_EquipSlot.Head,
            E_EquipmentCategory.Body => targetSlot == E_EquipSlot.Body,
            E_EquipmentCategory.Accessory => targetSlot is E_EquipSlot.Accessory1 or E_EquipSlot.Accessory2,
            _ => false
        };
    }

    /// <summary>
    /// 获取物品的可用数量(总持有数量 - 已装备数量)
    /// 如果是非装备物品，直接返回总持有数量
    /// </summary>
    /// <returns></returns>
    public static int GetAvailableItemCount(InventoryManager inventory, PartyManager partyManager,
        ItemDefinition_SO item)
    {
        if (item is null)
            return 0;
        
        int totalQuantity = inventory.GetItemQuantity(item);
        if (totalQuantity < 0)
            return 0;

        int equippedCount = 0;
        for (int i = 0; i < partyManager.PartyMembers.Count; i++)
        {
            equippedCount += partyManager.PartyMembers[i].GetEquippedItemCount(item);
        }
        
        return totalQuantity - equippedCount;
        
    }

    public static StatBlock BuildPreviewTotalStats(CharacterRuntimeData member, E_EquipSlot slot, EquipmentItem_SO previewItem)
    {
        if (member is null)
            return StatBlock.Zero;

        StatBlock previewTotal = member.GetTotalStats();    // 获取当前角色总属性
        EquipmentItem_SO currentItem = member.GetEquippedItem(slot);    // 获取当前装备的物品

        if (currentItem is not null)
            previewTotal += currentItem.statBonus * -1f;    // 减去当前装备的物品属性

        if (previewItem is not null)
            previewTotal += previewItem.statBonus;
        
        return previewTotal;
    }

}
