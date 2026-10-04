[CreateAssetMenu(menuName = "Battle/BattleConfig")]
public class BattleConfig_SO : ScriptableObject
{
    #region 攻击节奏参数

    [Header("Flow Timing")]
    public float StartBattleDelay = 0.5f;    // 战斗开始后到正式进入流程前的缓冲时间，避免切场景过突兀
    public float TurnStartDelay = 0.3f;    // 每个回合开始前的等待时间，用于 UI 和角色状态切换
    public float TurnEndDelay = 0.2f;    // 每个回合结束后的停顿时间，让节奏更清晰
    public float VictoryResultDelay = 1.0f;    // 胜利后到结果面板出现前的延迟，给演出留时间
    public float AIThinkDuration = 1.0f;    // AI 行动前的思考时长，用于模拟决策与控制节奏
    
    [Header("Attack Timing")] 
    public float GroupTargetHitInterval = 0.05f;    // 群体目标依次受击的时间间隔，增强连锁打击感
    public float MultiHitInterval = 0.08f;    // 多段攻击每一段命中的间隔时间，控制连击节奏

    [Header("Animation Timing")] 
    public float AttackWindupTime = 0.4f;    // 攻击前摇时长，控制角色出手前蓄力感
    public float AttackRecoveryTime = 0.8f;    // 攻击后摇时长，决定出手后的僵直与节奏
    public float DefendPoseDuration = 0.5f;    // 防御姿态保持时间，用于表现防御动作
    
    public float EscapeRunDuration = 1.2f;    // 角色逃跑动作时长，决定离场过程快慢
    public float EscapeExitDelay = 0.35f;    // 逃跑动作结束到真正退出战场前的额外延迟

    #endregion
}
