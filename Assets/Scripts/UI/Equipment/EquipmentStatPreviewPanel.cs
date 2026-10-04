
public class EquipmentStatPreviewPanel : MonoBehaviour
{
    [Serializable]
    public struct StatRowBinding
    {
        public E_StatType StatType;
        public EquipmentStatCompareRow row;
    }
    
    [SerializeField] private StatRowBinding[] statRows;

    public void Refresh(StatBlock currentStats, StatBlock previewStats, bool isInPreviewMode = true)
    {
        for (int i = 0; i < statRows.Length; i++)
        {
            var binding = statRows[i];
            
            int current = ReadStat(currentStats, binding.StatType);
            int preview = ReadStat(previewStats, binding.StatType); 

            binding.row.SetRow(current, preview, isInPreviewMode);
        }
    }

    private int ReadStat(StatBlock statBlock, E_StatType statType)
    {
        switch (statType)
        {
            case E_StatType.MaxHP: return statBlock.MaxHp;
            case E_StatType.MaxSP: return statBlock.MaxSp;
            case E_StatType.PAtk: return statBlock.PAtk;
            case E_StatType.PDef: return statBlock.PDef;
            case E_StatType.MAtk: return statBlock.MAtk;
            case E_StatType.MDef: return statBlock.MDef;
            case E_StatType.Speed: return statBlock.Speed;
            case E_StatType.Accuracy: return statBlock.Accuracy;
            case E_StatType.Evasion: return statBlock.Evasion;
            default: return 0;
        }
    }
}
