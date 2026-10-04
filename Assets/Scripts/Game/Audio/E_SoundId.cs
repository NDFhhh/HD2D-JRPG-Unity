/// <summary>
/// 全局音频资源索引枚举
/// 代码中只通过该枚举引用音效，具体 AudioClip 由 SoundLibrary_SO 资产配置
/// 注意：UI 区块的位置被 SFXPlayer.IsUiSound 用于区分路由（UI 音效走独立通道）
/// </summary>
public enum E_SoundId
{
    None = 0,

    // ===== UI（此区块走 UI 通道） =====
    UI_Click,           // 按钮点击
    UI_Confirm,         // 确认
    UI_Cancel,          // 取消
    UI_PanelOpen,       // 面板打开
    UI_PanelClose,      // 面板关闭
    UI_Hover,           // 悬停（可选）
    UI_Error,           // 操作无效 / 失败提示
    UI_Reward,          // 获得奖励

    // ===== 玩家 / 漫游 =====
    Player_Footstep,    // 脚步（建议配置多个变体）
    Player_Hurt,        // 受伤
    Player_Heal,        // 治疗
    Player_Attack_Slash,// 物理命中
    Player_Attack_Magic,// 魔法命中
    Player_Miss,        // 未命中（挥空）

    // ===== 技能 =====
    Skill_Cast,         // 技能咏唱起手

    // ===== 物品与交易 =====
    Item_Pickup,        // 拾取
    Item_Use,           // 使用道具
    Gold_Gain,          // 金币变动

    // ===== 战斗 =====
    Battle_Start,       // 战斗开始
    Battle_Turn_Tick,   // 行动者切换（轻提示，可不配置）
    Battle_Shield_Hit,  // 护盾削减
    Battle_Weakness_Hit,// 弱点命中
    Battle_Break,       // 破防
    Battle_Enemy_Die,   // 敌人死亡
    Battle_Victory,     // 战斗胜利 jingle
    Battle_Defeat,      // 战斗失败
    Battle_Escape,      // 逃跑成功
    Battle_Phase_Change,// 敌人（BOSS）阶段切换（全局默认，可被 BossPhaseConfig.phaseSound 覆盖）

    // ===== BGM（供 MusicPlayer 播放） =====
    BGM_Menu,
    BGM_Town_Day,
    BGM_Town_Night,
    BGM_Battle,
    BGM_Boss,
}
