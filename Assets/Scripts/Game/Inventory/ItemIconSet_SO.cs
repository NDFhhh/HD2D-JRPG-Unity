
[CreateAssetMenu( menuName = "Inventory/ItemIconSet")]
public class ItemIconSet_SO : ScriptableObject
{
    public ItemIconEntry[] itemIconEntries;

    public Sprite GetIcon(E_ItemIconKey itemIconKey)
    {
        foreach (var entry in itemIconEntries)
        {
            if (itemIconKey == entry.itemIconKey)
                return entry.icon;
        }
        
        return null;
    }
}

[System.Serializable]
public class ItemIconEntry
{
    public E_ItemIconKey itemIconKey;
    public Sprite icon;
}
