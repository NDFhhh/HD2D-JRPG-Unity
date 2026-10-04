[CreateAssetMenu(menuName = "Character/Ally")]
public class AllyDefinition_SO : CharacterDefinition_SO
{
    [Header("Ally Specific")] 
    public E_PlayerJob Job;
    
    [Header("Growth Settings")]
    public GlobalGrowthConfig_SO globalGrowthConfig_SO;
    public GrowthProfile growthProfile;
    
    [Header("Equipment Capability")] 
    public List<E_WeaponType> EquipableWeaponTypes = new();
    
    [Header("Initial Equipment")]
    public List<InitialEquipmentEntry> InitialEquipment = new();
    
    #region 经验成长参数

    [Header("Progression")] 
    [Min(1)] public int ExpToNextLevelAtLv1 = 200;
    [Min(1f)] public float ExpGrowthPerLevel = 1.15f;
    [Min(2f)] public int MaxLevel = 99;
    
    #endregion
    
    /// <summary>
    /// 计算升级到下一级所需的经验值
    /// </summary>
    /// <param name="currentLevel">当前等级</param>
    /// <returns>升级到下一级所需的经验值，如果已经到最大等级则返回 0</returns>
    public int GetExpRequiredToNextLevel(int currentLevel)
    {
        // 检查是否已经达到最大值
        if (currentLevel >= MaxLevel)
            return 0;
        
        // 确保等级不小于 1
        int clampedLevel = Mathf.Max(1, currentLevel);
        
        // 使用指数增长公式计算所需经验值
        float scaled = ExpToNextLevelAtLv1 * Mathf.Pow(ExpGrowthPerLevel, clampedLevel - 1);
        // 返回四舍五入后的经验值，并确保至少为 1
        return Mathf.Max(1, Mathf.RoundToInt(scaled));
    }

    public bool CanEquipWeaponType(E_WeaponType weaponType)
    {
        if (weaponType == E_WeaponType.None) return false;
        
        return EquipableWeaponTypes.Contains(weaponType);
    }

    [System.Serializable]
    public struct InitialEquipmentEntry
    {
        public E_EquipSlot slot;
        public EquipmentItem_SO item;
    }

    #region 属性成长

    public StatBlock GetStatForLevel(int level)
    {
        float hpMult = globalGrowthConfig_SO.GetCurveByRank(growthProfile.HP).Evaluate(level);
        float spMult = globalGrowthConfig_SO.GetCurveByRank(growthProfile.SP).Evaluate(level);
        float pAtkMult = globalGrowthConfig_SO.GetCurveByRank(growthProfile.PAtk).Evaluate(level);
        float pDefMult = globalGrowthConfig_SO.GetCurveByRank(growthProfile.PDef).Evaluate(level);
        float mAtkmult = globalGrowthConfig_SO.GetCurveByRank(growthProfile.MAtk).Evaluate(level);
        float mDefMult = globalGrowthConfig_SO.GetCurveByRank(growthProfile.MDef).Evaluate(level);
        float speedMult = globalGrowthConfig_SO.GetCurveByRank(growthProfile.Speed).Evaluate(level);

        return new StatBlock
        {
            MaxHp = Mathf.RoundToInt(BaseStats.MaxHp * hpMult),
            MaxSp = Mathf.RoundToInt(BaseStats.MaxSp * spMult),
            PAtk = Mathf.RoundToInt(BaseStats.PAtk * pAtkMult),
            PDef = Mathf.RoundToInt(BaseStats.PDef * pDefMult),
            MAtk = Mathf.RoundToInt(BaseStats.MAtk * mAtkmult),
            MDef = Mathf.RoundToInt(BaseStats.MDef * mDefMult),
            Speed = Mathf.RoundToInt(BaseStats.Speed * speedMult),
            
            Accuracy =  BaseStats.Accuracy,
            Evasion =  BaseStats.Evasion
        };
    }

    #endregion
}

[System.Serializable]
public struct GrowthProfile
{
    public E_GrowthRank HP;
    public E_GrowthRank SP;
    public E_GrowthRank PAtk;
    public E_GrowthRank PDef;
    public E_GrowthRank MAtk;
    public E_GrowthRank MDef;
    public E_GrowthRank Speed;
}
