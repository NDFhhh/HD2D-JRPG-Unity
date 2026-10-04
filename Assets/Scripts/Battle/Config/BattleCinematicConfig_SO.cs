[CreateAssetMenu(menuName = "Battle/Cinematic Config")]
public class BattleCinematicConfig_SO : ScriptableObject
{
    [Header("Cinematic Toggle")] 
    public bool EnableKillCinematic = true;
    public bool EnableBreakCinematic = true;
    public float KillDissolveStagger = 0.08f;    // 多个敌人被击杀时，特效的错峰间隔

    #region 击杀与破盾参数

    [Header("Kill Impact")]
    public BattleImpactCinematicSettings Kill = BattleImpactCinematicSettings.CreateLegacyDefault();

    [Header("Break Impact")]
    public BattleImpactCinematicSettings Break = BattleImpactCinematicSettings.CreateLegacyDefault();
    
    #endregion
}

[System.Serializable]
// 单次冲击演出的参数集合
// 这里只保留镜头和时间缩放。破盾闪屏由 Break Volume 自己管理
public class BattleImpactCinematicSettings
{
    [Header("Time Scale")] 
    public float HitStopDuration = 0.03f;    // 命中瞬间的停顿时长

    [Range(0.05f, 1f)] 
    public float SlowMoScale = 0.15f;    // 慢动作时间缩放倍率
    public float SlowMoInDuration = 0.06f;    // 进入慢动作的过渡时长
    public float SlowMoOutDuration = 0.18f;    // 退出慢动作的过渡时长
    public float HoldDuration = 0.12f;    // 没有镜头特写时，慢动作的额外停留时间
    
    [Header("Camera")]
    public float CameraTurnDuration = 0.08f;    // 镜头转向目标的时长
    public float CameraHoldDuration = 0.05f;    // 镜头转到位后的停留时长
    public float CameraReturnDuration = 0.12f;    // 镜头回到默认朝向的时长
    public Vector3 CameraEulerOffset = Vector3.zero;    // 镜头旋转偏移
    public Vector3 CameraPositionOffset = Vector3.zero;    // 镜头位置偏移

    public static BattleImpactCinematicSettings CreateLegacyDefault()
    {
        // 默认值直接沿用旧 BattleConfig_SO 的演出参数，方便无缝回到之前的视觉手感
        return new BattleImpactCinematicSettings();
    }
}
