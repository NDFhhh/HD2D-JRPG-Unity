
public class ItemDefinition_SO : ScriptableObject
{
    public string ItemName;
    
    [TextArea] public string ItemDescription;
    
    public E_ItemType ItemType;
    public E_ItemIconKey ItemIconKey;
    
    public int BuyPrice;
    public int SellPrice => (int)(BuyPrice * 0.75f);
    
    public int MaxStack = 99;
    
    [Header("稀有度")]
    public int RarityWeight = 100;
    
}
