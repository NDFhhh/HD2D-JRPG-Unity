public enum E_GameMode
{
    /// <summary>漫游</summary>
    Explore,
    /// <summary>交互</summary>
    InteractionMenu,
    /// <summary>战斗</summary>
    Battle,
    /// <summary>暂停</summary>
    Pause
}

public enum E_ActiveMap
{
    Player,
    UI,
    Battle,
    None
}

public enum E_CameraView
{
    Explore,
    Battle,
    BattleResult
}

public enum E_PlayerJob
{
    Any,
    
    /// <summary>剑士</summary>
    Warrior,
    /// <summary>药师</summary>
    Apothecary,
    /// <summary>神官</summary>
    Cleric,
    /// <summary>舞娘</summary>
    Dancer,
    /// <summary>猎人</summary>
    Hunter,
    /// <summary>商人</summary>
    Merchant,
    /// <summary>学者</summary>
    Scholar,
    /// <summary>盗贼</summary>
    Thief
}

public enum E_GrowthRank { S, A, B, C, D }

public enum E_ItemType
{
    /// <summary>装备</summary>
    Equipment = 0,
    /// <summary>消耗品</summary>
    Consumable = 1
}

public enum E_ItemIconKey
{
    /// <summary>武器类物品</summary>
    Weapon,
    /// <summary>防具类物品</summary>
    Armor,
    /// <summary>饰品类物品</summary>
    Accessory,
    /// <summary>治疗类物品</summary>
    Healing,
    /// <summary>SP恢复类物品</summary>
    SP,
    /// <summary>复活类物品</summary>
    Revive,
    /// <summary>解除异常状态类物品</summary>
    Cure,
    /// <summary>关键物品/任务物品</summary>
    KeyItem
}

public enum E_PanelType
{
    Item,
    Sell,
    Buy,
    Equipment
}

public enum E_EquipmentCategory
{
    /// <summary>武器</summary>
    Weapon,
    /// <summary>盾牌</summary>
    Shield,
    /// <summary>头盔</summary>
    Head,
    /// <summary>胸甲</summary>
    Body,
    /// <summary>饰品</summary>
    Accessory
}

public enum E_EquipSlot
{
    /// <summary>剑</summary>
    Sword,
    /// <summary>矛</summary>
    Spear,
    /// <summary>匕首</summary>
    Dagger,
    /// <summary>斧头</summary>
    Axe,
    /// <summary>弓</summary>
    Bow,
    /// <summary>法杖</summary>
    Staff,
    /// <summary>盾牌</summary>
    Shield,
    /// <summary>头盔</summary>
    Head,
    /// <summary>胸甲</summary>
    Body,
    /// <summary>饰品1</summary>
    Accessory1,
    /// <summary>饰品2</summary>
    Accessory2
}

public enum E_WeaponType
{
    None = 0,
    
    /// <summary>剑</summary>
    Sword,
    /// <summary>矛</summary>
    Spear,
    /// <summary>匕首</summary>
    Dagger,
    /// <summary>斧头</summary>
    Axe,
    /// <summary>弓</summary>
    Bow,
    /// <summary>法杖</summary>
    Staff,
}

public enum E_StatType
{
    MaxHP = 0,
    MaxSP = 1,
    PAtk = 2,
    PDef = 3,
    MAtk = 4,
    MDef = 5,
    Speed = 6,
    Accuracy = 7,
    Evasion = 8,
    
    CurrentHP = 100,
    CurrentSP = 101,
    CurrentBP = 102
}

public enum E_EnemyLayoutFormation
{
    Line = 0,
    BossTriangle = 1
}

public enum E_FadeStyle
{
    PanelFade,
    WipeMask
}

public enum E_BattleCommandType
{
    Attack,
    Skill,
    Item,
    Defend,
    Escape
}

public enum E_TargetType
{
    SingleEnemy,
    AllEnemies,
    SingleAlly,
    AllAllies,
    Self
}

public enum E_SkillType
{
    Damage,
    Heal,
    Buff,
    Debuff
}

public enum E_DamageKind
{
    Physical,
    Magical,
}

public enum E_ElementType
{
    None,
    Fire,
    Ice,
    Lightning,
    Wind,
    Light,
    Dark
}

public enum E_DamagePopupType
{
    Normal = 0,
    Heal = 2,
    Magic = 3,
    Gold = 6
}

// 2. 伤害 / 弱点类型 (合并了 E_WeaponType 和 E_ElementType，方便统一处理弱点)
public enum E_DamageType
{
    // 近战武器类型
    Sward, Spear, Dagger, Axe, Bow, Staff,
    // 属性
    Fire, Ice, Lightning, Wind, Light, Dark,
    // 特殊
    None, Untyped
}

public enum E_SkillVfxSpawnMode
{
    AntoByTargetType = 0,
    Target = 1,
    GroupCenter = 2
}