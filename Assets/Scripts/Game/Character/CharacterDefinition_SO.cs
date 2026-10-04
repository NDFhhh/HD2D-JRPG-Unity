public abstract class CharacterDefinition_SO : ScriptableObject
{
    [Header("Identity")] 
    public string ID;
    public string Name;
    public Sprite Portrait;
    
    [Header("Stats")]
    public int BaseLevel = 1;
    public StatBlock BaseStats;

    [Header("Skills")] 
    public SkillData_SO BasicAttack;
    public List<SkillData_SO> InitialSkills;    // 初始技能
    
    [Header("Animator")] 
    public AnimatorOverrideController fieldAnimator;
    public AnimatorOverrideController battleAnimator;
}

[System.Serializable]
public struct StatBlock
{
    /// <summary>最大生命值 </summary>
    public int MaxHp;
    /// <summary>最大法力值 </summary>
    public int MaxSp;
    /// <summary>物理攻击力 </summary>
    public int PAtk;
    /// <summary>物理防御力 </summary>
    public int PDef;
    /// <summary>法术攻击力 </summary>
    public int MAtk;
    /// <summary>法术防御力 </summary>
    public int MDef;
    /// <summary>速度 </summary>
    public int Speed;
    /// <summary>命中率</summary>
    public int Accuracy;
    /// <summary>闪避率 </summary>
    public int Evasion;

    public static StatBlock Zero = new(); //零值

    public static StatBlock operator +(StatBlock a, StatBlock b)
    {
        return new StatBlock()
        {
            MaxHp = a.MaxHp + b.MaxHp,
            MaxSp = a.MaxSp + b.MaxSp,
            PAtk = a.PAtk + b.PAtk,
            PDef = a.PDef + b.PDef,
            MAtk = a.MAtk + b.MAtk,
            MDef = a.MDef + b.MDef,
            Speed = a.Speed + b.Speed,
            Accuracy = a.Accuracy + b.Accuracy,
            Evasion = a.Evasion + b.Evasion,
        };
    }
    
    public static StatBlock operator *(StatBlock a, float multiplier)
    {
        return new StatBlock()
        {
            MaxHp = Mathf.RoundToInt(a.MaxHp * multiplier),
            MaxSp = Mathf.RoundToInt(a.MaxSp * multiplier),
            PAtk = Mathf.RoundToInt(a.PAtk * multiplier),
            PDef = Mathf.RoundToInt(a.PDef * multiplier),
            MAtk = Mathf.RoundToInt(a.MAtk * multiplier),
            MDef = Mathf.RoundToInt(a.MDef * multiplier),
            Speed = Mathf.RoundToInt(a.Speed * multiplier),
            Accuracy = Mathf.RoundToInt(a.Accuracy * multiplier),
            Evasion = Mathf.RoundToInt(a.Evasion * multiplier)
        };
    }

}
