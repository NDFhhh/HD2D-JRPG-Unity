using UnityEngine.UI;

/// <summary>
/// 音频设置面板控制器：四条总线滑条（主音量 / BGM / 音效 / UI）+ 静音开关
/// 拖动即时生效（AudioMixer / 降级方案），值通过 AudioSettings 持久化到 PlayerPrefs
/// 滑条取值范围请设为 0 ~ 1
/// </summary>
public class AudioSettingsPanelController : MonoBehaviour
{
    [Header("滑条（0~1）")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Slider uiSlider;

    [Header("静音开关（可选）")]
    [SerializeField] private Toggle muteToggle;

    private float _masterBeforeMute = 1f;

    private void OnEnable()
    {
        BindSlider(masterSlider, E_AudioBus.Master);
        BindSlider(bgmSlider, E_AudioBus.Bgm);
        BindSlider(sfxSlider, E_AudioBus.Sfx);
        BindSlider(uiSlider, E_AudioBus.Ui);

        if (muteToggle != null)
        {
            muteToggle.SetIsOnWithoutNotify(AudioSettings.GetBusVolume(E_AudioBus.Master) <= 0.001f);
            muteToggle.onValueChanged.AddListener(OnMuteToggled);
        }
    }

    private void OnDisable()
    {
        UnbindSlider(masterSlider);
        UnbindSlider(bgmSlider);
        UnbindSlider(sfxSlider);
        UnbindSlider(uiSlider);

        if (muteToggle != null)
            muteToggle.onValueChanged.RemoveListener(OnMuteToggled);
    }

    private void BindSlider(Slider slider, E_AudioBus bus)
    {
        if (slider == null)
            return;

        slider.SetValueWithoutNotify(AudioSettings.GetBusVolume(bus));
        slider.onValueChanged.AddListener(value => AudioSettings.SetBusVolume(bus, value));
    }

    private void UnbindSlider(Slider slider)
    {
        if (slider != null)
            slider.onValueChanged.RemoveAllListeners();
    }

    private void OnMuteToggled(bool muted)
    {
        float master = AudioSettings.GetBusVolume(E_AudioBus.Master);

        if (muted)
        {
            if (master > 0.001f)
                _masterBeforeMute = master;
            AudioSettings.SetBusVolume(E_AudioBus.Master, 0f);
        }
        else
        {
            AudioSettings.SetBusVolume(E_AudioBus.Master, _masterBeforeMute > 0.001f ? _masterBeforeMute : 1f);
        }

        // 静音状态变化后同步滑条显示
        if (masterSlider != null)
            masterSlider.SetValueWithoutNotify(AudioSettings.GetBusVolume(E_AudioBus.Master));
    }
}
