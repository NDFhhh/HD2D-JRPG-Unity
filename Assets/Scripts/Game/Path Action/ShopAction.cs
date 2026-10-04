
public class ShopAction : ActionBase
{
    [Header("Shop Action")]
    public List<ItemDefinition_SO> itemsBag;

    public override void TriggerAction(AllyDefinition_SO interactor)
    {
        EventBus.Publish(new PanelRequestEvent(this));
    }

    public bool TryExcuteTransaction(E_PanelType panelType, ItemDefinition_SO itemDefinition)
    {
        var inventory = InventoryManager.Instance;

        if (panelType is E_PanelType.Buy)
        {
            if (!inventory.TrySpednCurrency(itemDefinition.BuyPrice))
                return false;

            inventory.AddItem(itemDefinition, 1);
            return true;
        }
        else
        {
            if (inventory.GetItemQuantity(itemDefinition) <= 0)
                return false;
            
            inventory.RemoveItem(itemDefinition, 1);
            inventory.AddCurrency(itemDefinition.SellPrice);
            return true;
        }
    }
}
