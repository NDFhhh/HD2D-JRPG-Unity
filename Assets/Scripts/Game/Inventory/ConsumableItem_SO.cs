
[CreateAssetMenu(menuName = "Inventory/Consumable Item")]
public class ConsumableItem_SO : ItemDefinition_SO
{
    [Header("Consumable Effect")] 
    [Min(0)] public int restoreAmount;
}
