
public class InventoryManager : Singleton<InventoryManager>
{
    [Header("Icon Set")]
    public ItemIconSet_SO IconSet;
    
    [Header("Inventory")]
    public List<InventoryItem> CurrentInventory = new();

    [Header("Currency")]
    public int Currency;
    private int _initialCurrency;

    #region 对外调用接口

    public void AddItem(ItemDefinition_SO itemDefinition, int quantity)
    {
        foreach (var item in CurrentInventory)
        {
            if (item.ItemDefinition != itemDefinition)
                continue;
            
            item.Quantity += quantity;
            return;
        }
        
        CurrentInventory.Add(new InventoryItem(itemDefinition, quantity));
    }

    public void RemoveItem(ItemDefinition_SO itemDefinition, int quantity)
    {
        for (int i = 0; i < CurrentInventory.Count; i++)
        {
            if (CurrentInventory[i].ItemDefinition != itemDefinition)
                continue;
            
            CurrentInventory[i].Quantity -= quantity;
            
            if (CurrentInventory[i].Quantity <= 0)
                CurrentInventory.RemoveAt(i);
            
            return;
        }
    }

    public int GetItemQuantity(ItemDefinition_SO itemDefinition)
    {
        if (itemDefinition is null)
            return 0;

        foreach (var item in CurrentInventory)
        {
            if (itemDefinition == item.ItemDefinition)
                return item.Quantity;
        }
        return 0;
    }

    public bool TrySpednCurrency(int amount)
    {
        if (Currency < amount)
            return false;
        Currency -= amount;
        return true;
    }

    internal void AddCurrency(int SellPrice)
    {
        Currency += SellPrice;
    }

    #endregion
    
}

[System.Serializable]
public class InventoryItem
{
    public ItemDefinition_SO ItemDefinition;
    public int Quantity;
    
    public InventoryItem(ItemDefinition_SO itemDefinition, int quantity)
    {
        ItemDefinition = itemDefinition;
        Quantity = quantity;
    }

    public bool IsEquipment => ItemDefinition is not null && ItemDefinition.ItemType == E_ItemType.Equipment;
    public bool IsConsumable => ItemDefinition is not null && ItemDefinition.ItemType == E_ItemType.Consumable;
}
