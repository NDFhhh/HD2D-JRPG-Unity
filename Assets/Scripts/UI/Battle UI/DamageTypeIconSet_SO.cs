[CreateAssetMenu(menuName = "Battle/Damage Type Icon Set")]
public class DamageTypeIconSet_SO : ScriptableObject
{
    [SerializeField] private DamageTypeIconEntry[] entries;
    
    private readonly Dictionary<E_DamageType, Sprite> _iconCache = new();

    public Sprite GetIcon(E_DamageType damageType)
    {
        return _iconCache[damageType];
    }

    private void OnValidate()
    {
        _iconCache.Clear();
        for (int i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            _iconCache[entry.damageType] = entry.icon;
        }
    }
}

[Serializable]
public struct DamageTypeIconEntry
{
    #region 图标条目结构

    public E_DamageType damageType;
    public Sprite icon;

    #endregion
}