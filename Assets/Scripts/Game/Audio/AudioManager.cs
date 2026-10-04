using UnityEngine.Audio;

/// <summary>
/// 音频系统入口单例（DontDestroyOnLoad，跨场景存活）
/// 组合三个子组件：MusicPlayer（BGM 交叉淡化）、SFXPlayer（音效池）、AudioEventBridge（事件映射）
/// </summary>
public class AudioManager : Singleton<AudioManager>
{
    [Tooltip("全局唯一的音频索引资产")]
    [SerializeField] private SoundLibrary_SO library;

    [Tooltip("AudioMixer 资产（可选）。创建后请暴露 MasterVolume/BgmVolume/SfxVolume/UiVolume 四个参数并建立 BGM/SFX/UI 分组")]
    [SerializeField] private AudioMixer mixer;

    public MusicPlayer Music { get; private set; }
    public SFXPlayer SFX { get; private set; }

    public SoundLibrary_SO Library => library;
    public AudioMixer Mixer => mixer;

    private AudioMixerGroup _bgmGroup;
    private AudioMixerGroup _sfxGroup;
    private AudioMixerGroup _uiGroup;

    protected override void Awake()
    {
        base.Awake();

        // 重复实例：单例基类已标记销毁，这里不再初始化
        if (Instance != this)
            return;

        DontDestroyOnLoad(gameObject);

        CacheMixerGroups();

        Music = gameObject.AddComponent<MusicPlayer>();
        Music.Init(library, _bgmGroup);

        SFX = gameObject.AddComponent<SFXPlayer>();
        SFX.Init(library, _sfxGroup, _uiGroup);

        // AudioEventBridge 会订阅 EventBus 并在 Start 时按当前场景播 BGM
        gameObject.AddComponent<AudioEventBridge>();

        // 恢复玩家上次的音量设置
        AudioSettings.ApplyAll();
    }

    /// <summary> 静态便捷入口：任意脚本可一行播音效（如 UIButtonSfx） </summary>
    public static void PlaySfx(E_SoundId id, float volumeScale = 1f, Vector3? worldPos = null)
    {
        var instance = Instance;
        if (instance == null || instance.SFX == null)
            return;
        instance.SFX.Play(id, volumeScale, worldPos);
    }

    /// <summary>
    /// 查询某条音效的最长实际播放时长（秒），供 “等音效播完再进入下一状态” 的流程使用
    /// 无音频系统或未配置时返回 0，调用方应把 0 视为 “无需等待”
    /// </summary>
    public static float GetSfxDuration(E_SoundId id)
    {
        var instance = Instance;
        if (instance == null || instance.library == null)
            return 0f;
        return instance.library.TryGetCue(id, out var cue) && cue != null
            ? cue.LongestPlayableDuration
            : 0f;
    }

    /// <summary> 应用某条总线的音量（Mixer 优先，无 Mixer 时走降级方案） </summary>
    public void ApplyBusVolume(E_AudioBus bus, float linear01)
    {
        linear01 = Mathf.Clamp01(linear01);

        if (mixer != null)
        {
            mixer.SetFloat(ExposedParamName(bus), LinearToDb(linear01));
            return;
        }

        // 降级方案：未配置 AudioMixer 时的近似音量控制
        switch (bus)
        {
            case E_AudioBus.Master:
                AudioListener.volume = linear01;
                break;
            case E_AudioBus.Bgm:
                Music?.SetBusVolume(linear01);
                break;
            case E_AudioBus.Sfx:
                SFX?.SetBusVolume(E_AudioBus.Sfx, linear01);
                break;
            case E_AudioBus.Ui:
                SFX?.SetBusVolume(E_AudioBus.Ui, linear01);
                break;
        }
    }

    /// <summary> 线性音量 [0,1] 转 dB（对数曲线，听感线性） </summary>
    public static float LinearToDb(float linear)
        => Mathf.Log10(Mathf.Max(linear, 0.0001f)) * 20f;

    /// <summary> Mixer 暴露参数命名约定：MasterVolume / BgmVolume / SfxVolume / UiVolume </summary>
    private static string ExposedParamName(E_AudioBus bus)
        => bus switch
        {
            E_AudioBus.Master => "MasterVolume",
            E_AudioBus.Bgm => "BgmVolume",
            E_AudioBus.Sfx => "SfxVolume",
            _ => "UiVolume",
        };

    private void CacheMixerGroups()
    {
        if (mixer == null)
            return;

        _bgmGroup = FindGroup("BGM");
        _sfxGroup = FindGroup("SFX");
        _uiGroup = FindGroup("UI");

        if (_bgmGroup == null || _sfxGroup == null || _uiGroup == null)
            Debug.LogWarning("[Audio] AudioMixer 中未找到 BGM / SFX / UI 分组，音频将输出到 Master 直通");
    }

    private AudioMixerGroup FindGroup(string groupName)
    {
        var matches = mixer.FindMatchingGroups(groupName);
        return (matches != null && matches.Length > 0) ? matches[0] : null;
    }
}
