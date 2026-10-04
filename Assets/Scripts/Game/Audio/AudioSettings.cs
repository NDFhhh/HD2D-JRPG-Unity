/// <summary>
/// 音频总线，对应 AudioMixer 的四条通道
/// </summary>
public enum E_AudioBus
{
    Master, // 总音量
    Bgm,    // 背景音乐
    Sfx,    // 游戏音效
    Ui,     // UI 音效（独立通道，游戏音效静音时设置界面仍有反馈）
}

/// <summary>
/// 音量设置的持久化与应用
/// 数据存 PlayerPrefs；应用优先走 AudioMixer（对数曲线），
/// 未配置 Mixer 时由 AudioManager 走 AudioSource 降级方案
/// </summary>
public static class AudioSettings
{
    private const string PrefsPrefix = "Audio_";

    public static float GetBusVolume(E_AudioBus bus)
        => PlayerPrefs.GetFloat(Key(bus), 1f);

    public static void SetBusVolume(E_AudioBus bus, float linear01)
    {
        linear01 = Mathf.Clamp01(linear01);
        PlayerPrefs.SetFloat(Key(bus), linear01);
        PlayerPrefs.Save();
        Apply(bus, linear01);
    }

    /// <summary> 启动时恢复全部音量（由 AudioManager.Awake 调用） </summary>
    public static void ApplyAll()
    {
        var buses = Enum.GetValues(typeof(E_AudioBus));
        foreach (E_AudioBus bus in buses)
            Apply(bus, GetBusVolume(bus));
    }

    private static void Apply(E_AudioBus bus, float linear01)
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.ApplyBusVolume(bus, linear01);
    }

    private static string Key(E_AudioBus bus) => PrefsPrefix + bus;
}
