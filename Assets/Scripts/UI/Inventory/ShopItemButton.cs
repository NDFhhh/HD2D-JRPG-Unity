
using TMPro;

public class ShopItemButton : ItemButton
{
    [Header("Shop Item Button")] 
    [SerializeField] private TMP_Text priceText;

    public void SetupButton(InventoryItem inventoryItem, E_PanelType panelType, Action<ItemDefinition_SO> onItemClick)
    {
        base.SetupButton(inventoryItem, onItemClick);
        priceText.text = panelType == E_PanelType.Buy
            ?$"{inventoryItem.ItemDefinition.BuyPrice}"
            :$"{inventoryItem.ItemDefinition.SellPrice}";
    }
}

