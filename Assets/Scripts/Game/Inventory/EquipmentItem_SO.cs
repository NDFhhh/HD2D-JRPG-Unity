using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "Inventory/Equipment Item")]
public class EquipmentItem_SO : ItemDefinition_SO
{
    [Header("Equipment Config")] 
    public E_EquipmentCategory category = E_EquipmentCategory.Weapon;
    public E_WeaponType weaponType = E_WeaponType.Sword;
    
    public StatBlock statBonus = StatBlock.Zero;

    private void OnValidate()
    {
        if (category != E_EquipmentCategory.Weapon)
            weaponType = E_WeaponType.None;
    }
}
