
using TMPro;

public class StealItemButton : ItemButton
{
    [Header("Steal Item Button")] 
    [SerializeField] private TMP_Text rateText;

    public override void SetupButton(InventoryItem inventoryItem, Action<ItemDefinition_SO> onItemClick)
    {
        base.SetupButton(inventoryItem, onItemClick);
        rateText.text = $"{CurrentItemDefinition.RarityWeight}%";
    }
}
