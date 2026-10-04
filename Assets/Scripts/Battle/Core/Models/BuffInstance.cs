/// <summary>
/// Buff / Debuff 运行时实例
/// 挂在 BattleEntity 上，记录 “哪项属性、加减多少、还剩几回合”
/// </summary>
[Serializable]
public class BuffInstance
{
    /// <summary> 来源技能名 (用于提示文本与调试) </summary>
    public string SourceSkillName;
    /// <summary> 来源技能图标 (留给 Buff 栏 UI 使用) </summary>
    public Sprite Icon;
    /// <summary> 作用的属性 </summary>
    public E_StatType Stat;
    /// <summary> 百分比加成 (0.25 = +25%，-0.25 = -25%) </summary>
    public float PercentBonus;
    /// <summary> 剩余生效回合数 (每回合开始 -1，归零到期) </summary>
    public int RemainingTurns;

    /// <summary> 是否为 Debuff (数值为负即为 Debuff) </summary>
    public bool IsDebuff => PercentBonus < 0f;

    public BuffInstance(string sourceSkillName, Sprite icon, E_StatType stat, float percentBonus, int remainingTurns)
    {
        SourceSkillName = sourceSkillName;
        Icon = icon;
        Stat = stat;
        PercentBonus = percentBonus;
        RemainingTurns = remainingTurns;
    }
}

/// <summary>
/// Buff 相关小工具：可 Buff 属性判断 / 属性中文显示名
/// </summary>
public static class BuffStatUtility
{
    /// <summary>
    /// 目前只支持对战斗属性做 Buff
    /// MaxHP / MaxSP 会牵扯当前血量同步问题，暂不支持
    /// </summary>
    public static bool IsBuffableStat(E_StatType stat)
        => stat is E_StatType.PAtk or E_StatType.PDef
            or E_StatType.MAtk or E_StatType.MDef
            or E_StatType.Speed or E_StatType.Accuracy or E_StatType.Evasion;

    public static string GetDisplayName(E_StatType stat) => stat switch
    {
        E_StatType.PAtk => "物攻",
        E_StatType.PDef => "物防",
        E_StatType.MAtk => "属攻",
        E_StatType.MDef => "属防",
        E_StatType.Speed => "速度",
        E_StatType.Accuracy => "命中",
        E_StatType.Evasion => "回避",
        _ => stat.ToString()
    };
}
