using MFramework.Event;
using UnityEngine.SceneManagement;

/// <summary>
/// 音频事件桥：订阅 EventBus 中已有的游戏事件，映射为 BGM 切换与音效播放
/// 这是全项目唯一"知道哪个事件配哪个声音"的地方，玩法代码保持零侵入
/// </summary>
public class AudioEventBridge : MonoBehaviour,
    IEventReceiver<GameModeChangedEvent>,
    IEventReceiver<SceneLoadCompleteEvent>,
    IEventReceiver<BattleStartedEvent>,
    IEventReceiver<BattleEndedEvent>,
    IEventReceiver<BattleEscapedEvent>,
    IEventReceiver<BattleResultViewEnterEvent>,
    IEventReceiver<BattleResultConfirmedEvent>,
    IEventReceiver<DamageDealtEvent>,
    IEventReceiver<SkillNameDisplayEvent>,
    IEventReceiver<ActiveEntityChangedEvent>,
    IEventReceiver<EntityShieldChangedEvent>,
    IEventReceiver<EntityWeaknessChangedEvent>,
    IEventReceiver<BreakCinematicRequestedEvent>,
    IEventReceiver<KillCinematicRequestedEvent>,
    IEventReceiver<BossPhaseTransitionedEvent>,
    IEventReceiver<BattleNotificationEvent>,
    IEventReceiver<PanelRequestEvent>,
    IEventReceiver<FootstepEvent>
{
    [Tooltip("启动时按当前场景自动播放对应 BGM（AudioManager 恰好不在场景切换时机创建时使用）")]
    [SerializeField] private bool playBgmOnStart = true;

    /// <summary> 战斗 BGM 快切时长，制造紧张感 </summary>
    private const float BattleFadeDuration = 0.6f;
    /// <summary> 场景 BGM 慢切时长 </summary>
    private const float SceneFadeDuration = 1.8f;

    private MusicPlayer Music
        => AudioManager.Instance != null ? AudioManager.Instance.Music : null;

    private SFXPlayer Sfx
        => AudioManager.Instance != null ? AudioManager.Instance.SFX : null;

    private void Start()
    {
        if (!playBgmOnStart)
            return;

        var bgm = ResolveSceneBgm(SceneManager.GetActiveScene().name);
        if (bgm != E_SoundId.None)
            Music?.Play(bgm, SceneFadeDuration);
    }

    #region 订阅管理

    private void OnEnable()
    {
        EventBus.Subscribe<GameModeChangedEvent>(this);
        EventBus.Subscribe<SceneLoadCompleteEvent>(this);
        EventBus.Subscribe<BattleStartedEvent>(this);
        EventBus.Subscribe<BattleEndedEvent>(this);
        EventBus.Subscribe<BattleEscapedEvent>(this);
        EventBus.Subscribe<BattleResultViewEnterEvent>(this);
        EventBus.Subscribe<BattleResultConfirmedEvent>(this);
        EventBus.Subscribe<DamageDealtEvent>(this);
        EventBus.Subscribe<SkillNameDisplayEvent>(this);
        EventBus.Subscribe<ActiveEntityChangedEvent>(this);
        EventBus.Subscribe<EntityShieldChangedEvent>(this);
        EventBus.Subscribe<EntityWeaknessChangedEvent>(this);
        EventBus.Subscribe<BreakCinematicRequestedEvent>(this);
        EventBus.Subscribe<KillCinematicRequestedEvent>(this);
        EventBus.Subscribe<BossPhaseTransitionedEvent>(this);
        EventBus.Subscribe<BattleNotificationEvent>(this);
        EventBus.Subscribe<PanelRequestEvent>(this);
        EventBus.Subscribe<FootstepEvent>(this);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<GameModeChangedEvent>(this);
        EventBus.Unsubscribe<SceneLoadCompleteEvent>(this);
        EventBus.Unsubscribe<BattleStartedEvent>(this);
        EventBus.Unsubscribe<BattleEndedEvent>(this);
        EventBus.Unsubscribe<BattleEscapedEvent>(this);
        EventBus.Unsubscribe<BattleResultViewEnterEvent>(this);
        EventBus.Unsubscribe<BattleResultConfirmedEvent>(this);
        EventBus.Unsubscribe<DamageDealtEvent>(this);
        EventBus.Unsubscribe<SkillNameDisplayEvent>(this);
        EventBus.Unsubscribe<ActiveEntityChangedEvent>(this);
        EventBus.Unsubscribe<EntityShieldChangedEvent>(this);
        EventBus.Unsubscribe<EntityWeaknessChangedEvent>(this);
        EventBus.Unsubscribe<BreakCinematicRequestedEvent>(this);
        EventBus.Unsubscribe<KillCinematicRequestedEvent>(this);
        EventBus.Unsubscribe<BossPhaseTransitionedEvent>(this);
        EventBus.Unsubscribe<BattleNotificationEvent>(this);
        EventBus.Unsubscribe<PanelRequestEvent>(this);
        EventBus.Unsubscribe<FootstepEvent>(this);
    }

    #endregion

    #region BGM 切换

    public void OnEvent(GameModeChangedEvent evt)
    {
        switch (evt.newMode)
        {
            case E_GameMode.Battle:
                Music?.Play(E_SoundId.BGM_Battle, BattleFadeDuration);
                break;

            case E_GameMode.Pause:
                Music?.SetPaused(true);
                break;

            // 漫游 / 交互菜单继续播场景 BGM，只解除暂停
            default:
                Music?.SetPaused(false);
                break;
        }
    }

    public void OnEvent(SceneLoadCompleteEvent evt)
    {
        Music?.SetPaused(false);
        var bgm = ResolveSceneBgm(evt.LoadedScene.name);
        if (bgm != E_SoundId.None)
            Music?.Play(bgm, SceneFadeDuration);
    }

    public void OnEvent(BattleStartedEvent evt)
    {
        Sfx?.Play(E_SoundId.Battle_Start);
        // 与 GameModeChangedEvent 二选一触发即可，同曲 Play 会被忽略
        Music?.Play(E_SoundId.BGM_Battle, BattleFadeDuration);
    }

    public void OnEvent(BattleEndedEvent evt)
    {
        Music?.Stop(0.8f);
        Sfx?.Play(evt.IsWin ? E_SoundId.Battle_Victory : E_SoundId.Battle_Defeat);
    }

    public void OnEvent(BattleEscapedEvent evt)
    {
        Sfx?.Play(E_SoundId.Battle_Escape);
    }

    /// <summary>
    /// 场景名 -> BGM 映射（新增场景时在这里补一条即可）
    /// </summary>
    private E_SoundId ResolveSceneBgm(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return E_SoundId.None;

        string name = sceneName.ToLowerInvariant();

        if (name.Contains("town01_day"))
            return E_SoundId.BGM_Town_Day;
        if (name.Contains("town01_night"))
            return E_SoundId.BGM_Town_Night;
        if (name.Contains("menu"))
            return E_SoundId.BGM_Menu;

        return E_SoundId.None;
    }

    #endregion

    #region 战斗音效

    public void OnEvent(DamageDealtEvent evt)
    {
        // 1. 技能配置了专属命中音效时优先播放
        if (evt.HitCueOverride != null)
        {
            Sfx?.PlayCue(evt.HitCueOverride);
            return;
        }

        // 2. 否则按结算类型播放全局默认音效
        if (evt.IsHeal)
        {
            Sfx?.Play(E_SoundId.Player_Heal);
        }
        else if (evt.Amount <= 0)
        {
            Sfx?.Play(E_SoundId.Player_Miss);
        }
        else if (evt.IsWeaknessHit)
        {
            Sfx?.Play(E_SoundId.Battle_Weakness_Hit);
        }
        // 技能行动在出手时已播放咏唱音效，命中时跳过默认攻击音效，避免两个音效叠在一起
        else if (evt.IsSkillAction)
        {
            // 技能命中：不播放全局攻击音效
        }
        else if (evt.IsMagic)
        {
            Sfx?.Play(E_SoundId.Player_Attack_Magic);
        }
        else
        {
            Sfx?.Play(E_SoundId.Player_Attack_Slash);
        }
    }

    public void OnEvent(SkillNameDisplayEvent evt)
    {
        // 蓄力预告只显示技能名，不播音效；咏唱音效等技能真正打出时再播
        if (!evt.PlayCastSound)
            return;

        // 技能配置了专属咏唱音效时优先播放，否则用全局默认
        if (evt.CastCueOverride != null)
            Sfx?.PlayCue(evt.CastCueOverride);
        else
            Sfx?.Play(E_SoundId.Skill_Cast);
    }

    public void OnEvent(ActiveEntityChangedEvent evt)
    {
        Sfx?.Play(E_SoundId.Battle_Turn_Tick);
    }

    public void OnEvent(EntityShieldChangedEvent evt)
    {
        Sfx?.Play(E_SoundId.Battle_Shield_Hit);
    }

    public void OnEvent(EntityWeaknessChangedEvent evt)
    {
        Sfx?.Play(E_SoundId.Battle_Weakness_Hit);
    }

    public void OnEvent(BreakCinematicRequestedEvent evt)
    {
        Sfx?.Play(E_SoundId.Battle_Break);
    }

    public void OnEvent(KillCinematicRequestedEvent evt)
    {
        Sfx?.Play(E_SoundId.Battle_Enemy_Die);
    }

    public void OnEvent(BossPhaseTransitionedEvent evt)
    {
        // 阶段配置了专属音效时优先播放，否则用全局默认
        if (evt.PhaseCueOverride != null)
            Sfx?.PlayCue(evt.PhaseCueOverride);
        else
            Sfx?.Play(E_SoundId.Battle_Phase_Change);
    }

    public void OnEvent(BattleResultViewEnterEvent evt)
    {
        Sfx?.Play(E_SoundId.UI_Reward);
    }

    public void OnEvent(BattleResultConfirmedEvent evt)
    {
        Sfx?.Play(E_SoundId.UI_Confirm);
    }

    public void OnEvent(BattleNotificationEvent evt)
    {
        if (!evt.IsSuccess)
            Sfx?.Play(E_SoundId.UI_Error);
    }

    #endregion

    #region 漫游与 UI 音效

    public void OnEvent(FootstepEvent evt)
    {
        Sfx?.Play(E_SoundId.Player_Footstep, 1f, evt.Position);
    }

    public void OnEvent(PanelRequestEvent evt)
    {
        Sfx?.Play(E_SoundId.UI_PanelOpen);
    }

    #endregion
}
