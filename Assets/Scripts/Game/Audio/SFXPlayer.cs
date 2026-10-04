using UnityEngine.Audio;

/// <summary>
/// 音效播放器：AudioSource 池 + PlayOneShot
/// 支持冷却、并发上限（防"一刀砍中 8 人"的音效轰炸）、随机变体与变调（由 AudioCue 提供）
/// </summary>
public class SFXPlayer : MonoBehaviour
{
    private const int PoolInitialSize = 8;
    private const int PoolMaxSize = 24;

    private SoundLibrary_SO _library;
    private AudioMixerGroup _sfxGroup;
    private AudioMixerGroup _uiGroup;

    private readonly List<AudioSource> _pool = new();
    private int _roundRobin;

    private readonly Dictionary<E_SoundId, float> _lastPlayTime = new();
    private readonly Dictionary<E_SoundId, int> _activeVoices = new();
    private readonly Dictionary<AudioCue, float> _cueLastPlay = new();
    private readonly Dictionary<AudioCue, int> _cueActiveVoices = new();
    private readonly Dictionary<E_AudioBus, float> _fallbackVolumes = new()
    {
        { E_AudioBus.Sfx, 1f },
        { E_AudioBus.Ui, 1f },
    };

    public void Init(SoundLibrary_SO library, AudioMixerGroup sfxGroup, AudioMixerGroup uiGroup)
    {
        _library = library;
        _sfxGroup = sfxGroup;
        _uiGroup = uiGroup;

        for (int i = 0; i < PoolInitialSize; i++)
            CreateSource();
    }

    /// <summary>
    /// 播放一条音效
    /// </summary>
    /// <param name="id">音效枚举</param>
    /// <param name="volumeScale">额外音量缩放（0~1）</param>
    /// <param name="worldPos">可选：传入则按 3D 空间声定位（如场景内交互物），默认 2D</param>
    public void Play(E_SoundId id, float volumeScale = 1f, Vector3? worldPos = null)
    {
        if (id == E_SoundId.None)
            return;

        if (_library == null || !_library.TryGetCue(id, out var cue) || cue.IsEmpty)
            return;

        // 1. 冷却检查（unscaled 时间，暂停菜单里 UI 音效仍正常触发）
        float now = Time.unscaledTime;
        if (_lastPlayTime.TryGetValue(id, out float lastTime) && now - lastTime < cue.Cooldown)
            return;
        _lastPlayTime[id] = now;

        // 2. 并发上限检查
        _activeVoices.TryGetValue(id, out int voices);
        if (voices >= cue.MaxVoices)
            return;

        if (!cue.TryGetNext(out var clip, out float clipVolume, out float pitch))
            return;

        // 3. 取池中声源并播放（UI 音效走 UI 通道）
        bool isUi = IsUiSound(id);
        var source = NextSource(isUi ? _uiGroup : _sfxGroup);

        if (worldPos.HasValue)
        {
            source.transform.position = worldPos.Value;
            source.spatialBlend = 1f;
        }
        else
        {
            source.spatialBlend = 0f;
        }

        source.pitch = pitch;
        float busVolume = _fallbackVolumes[isUi ? E_AudioBus.Ui : E_AudioBus.Sfx];
        source.PlayOneShot(clip, clipVolume * volumeScale * busVolume);

        StartCoroutine(TrackVoice(id, clip.length / Mathf.Max(0.1f, pitch)));
    }

    /// <summary>
    /// 直接播放一个外部 AudioCue（技能专属音效等场景，绕过 SoundLibrary）
    /// </summary>
    /// <param name="cue">要播放的音效资产</param>
    /// <param name="volumeScale">额外音量缩放（0~1）</param>
    public void PlayCue(AudioCue cue, float volumeScale = 1f)
    {
        if (cue == null || cue.IsEmpty)
            return;

        // 复用 Play 的冷却 / 并发逻辑：以 cue 资产实例为 key
        if (!TryAcquireSlot(cue, out float now))
            return;

        if (!cue.TryGetNext(out var clip, out float clipVolume, out float pitch))
            return;

        var source = NextSource(_sfxGroup);
        source.spatialBlend = 0f;
        source.pitch = pitch;
        source.PlayOneShot(clip, clipVolume * volumeScale * _fallbackVolumes[E_AudioBus.Sfx]);

        StartCoroutine(TrackVoiceCue(cue, clip.length / Mathf.Max(0.1f, pitch)));
    }

    /// <summary> 冷却 + 并发上限检查（供 Play 与 PlayCue 共用） </summary>
    private bool TryAcquireSlot(AudioCue cue, out float now)
    {
        now = Time.unscaledTime;
        if (_cueLastPlay.TryGetValue(cue, out float lastTime) && now - lastTime < cue.Cooldown)
            return false;
        _cueLastPlay[cue] = now;

        _cueActiveVoices.TryGetValue(cue, out int voices);
        if (voices >= cue.MaxVoices)
            return false;

        return true;
    }

    private IEnumerator TrackVoiceCue(AudioCue cue, float duration)
    {
        _cueActiveVoices.TryGetValue(cue, out int count);
        _cueActiveVoices[cue] = count + 1;

        yield return new WaitForSeconds(duration);

        _cueActiveVoices.TryGetValue(cue, out count);
        _cueActiveVoices[cue] = Mathf.Max(0, count - 1);
    }

    /// <summary> 无 Mixer 降级方案：SFX / UI 通道音量 </summary>
    public void SetBusVolume(E_AudioBus bus, float linear01)
    {
        if (bus == E_AudioBus.Sfx || bus == E_AudioBus.Ui)
            _fallbackVolumes[bus] = linear01;
    }

    /// <summary> UI 音效判定（E_SoundId 中 UI 区块的范围检查） </summary>
    public static bool IsUiSound(E_SoundId id)
        => id >= E_SoundId.UI_Click && id <= E_SoundId.UI_Reward;

    private AudioSource CreateSource()
    {
        var go = new GameObject($"SFX_{_pool.Count}");
        go.transform.SetParent(transform, false);

        var source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 2f;
        source.maxDistance = 25f;

        _pool.Add(source);
        return source;
    }

    private AudioSource NextSource(AudioMixerGroup mixerGroup)
    {
        if (_pool.Count == 0)
            return CreateSource();

        AudioSource source = _pool[_roundRobin];
        _roundRobin = (_roundRobin + 1) % _pool.Count;

        // 简单扩容策略：轮到的声源正忙且未达上限时新建一个
        if (source.isPlaying && _pool.Count < PoolMaxSize)
            source = CreateSource();

        if (mixerGroup != null)
            source.outputAudioMixerGroup = mixerGroup;

        return source;
    }

    private IEnumerator TrackVoice(E_SoundId id, float duration)
    {
        _activeVoices.TryGetValue(id, out int count);
        _activeVoices[id] = count + 1;

        yield return new WaitForSeconds(duration);

        _activeVoices.TryGetValue(id, out count);
        _activeVoices[id] = Mathf.Max(0, count - 1);
    }
}
