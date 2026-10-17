using UnityEngine.Audio;

/// <summary>
/// BGM 播放器：双 AudioSource 交叉淡化
/// 同曲重复 Play 会被忽略（防止事件风暴导致 BGM 重头播）
/// </summary>
public class MusicPlayer : MonoBehaviour
{
    private SoundLibrary_SO _library;
    private AudioSource _sourceA;
    private AudioSource _sourceB;
    private AudioSource _active;

    private E_SoundId _currentId = E_SoundId.None;
    private Coroutine _fadeJob;

    // 无 Mixer 时的降级音量控制
    private float _busVolume = 1f;
    private float _clipVolume = 1f;

    public E_SoundId CurrentId => _currentId;
    public bool IsPlaying => _active != null && _active.isPlaying;

    public void Init(SoundLibrary_SO library, AudioMixerGroup mixerGroup)
    {
        _library = library;
        _sourceA = CreateSource(mixerGroup);
        _sourceB = CreateSource(mixerGroup);
        _active = _sourceA;
    }

    /// <summary>
    /// 播放指定 BGM（交叉淡化）
    /// </summary>
    /// <param name="bgmId">曲目枚举</param>
    /// <param name="fadeDuration">淡化时长（秒）。战斗切换建议 0.5 左右，场景切换 1.5~2</param>
    /// <param name="restartIfSame">同曲时是否强制重播（默认忽略）</param>
    public void Play(E_SoundId bgmId, float fadeDuration = 1.5f, bool restartIfSame = false)
    {
        if (_library == null || !_library.TryGetCue(bgmId, out var cue))
        {
            return;
        }

        if (bgmId == _currentId && !restartIfSame && IsPlaying)
            return;

        if (!cue.TryGetPrimary(out var clip, out var clipVolume))
        {
            Debug.LogWarning($"[Audio] BGM {bgmId} 的 AudioCue 中没有配置 AudioClip");
            return;
        }

        // 轮换声源：新曲进、旧曲淡出
        var next = (_active == _sourceA) ? _sourceB : _sourceA;
        next.clip = clip;
        next.volume = 0f;
        next.Play();

        var old = _active;
        _active = next;
        _currentId = bgmId;
        _clipVolume = clipVolume;

        if (_fadeJob != null)
            StopCoroutine(_fadeJob);
        _fadeJob = StartCoroutine(Crossfade(next, clipVolume * _busVolume, old, fadeDuration));
    }

    /// <summary> 停止 BGM（淡化） </summary>
    public void Stop(float fadeDuration = 1f)
    {
        _currentId = E_SoundId.None;

        if (!IsPlaying)
            return;

        if (_fadeJob != null)
            StopCoroutine(_fadeJob);
        _fadeJob = StartCoroutine(Crossfade(null, 0f, _active, fadeDuration));
    }

    /// <summary> 暂停 / 恢复（暂停菜单用） </summary>
    public void SetPaused(bool paused)
    {
        SetSourcePaused(_sourceA, paused);
        SetSourcePaused(_sourceB, paused);
    }

    /// <summary> 无 Mixer 降级方案：BGM 通道音量 </summary>
    public void SetBusVolume(float linear01)
    {
        _busVolume = linear01;
        if (_fadeJob == null && IsPlaying)
            _active.volume = _clipVolume * _busVolume;
    }

    private AudioSource CreateSource(AudioMixerGroup mixerGroup)
    {
        var source = gameObject.AddComponent<AudioSource>();
        source.loop = true;
        source.playOnAwake = false;
        source.volume = 0f;
        if (mixerGroup != null)
            source.outputAudioMixerGroup = mixerGroup;
        return source;
    }

    private void SetSourcePaused(AudioSource source, bool paused)
    {
        if (source == null)
            return;

        if (paused)
        {
            if (source.isPlaying)
                source.Pause();
        }
        else
        {
            if (source.clip != null && !source.isPlaying)
                source.UnPause();
        }
    }

    private IEnumerator Crossfade(AudioSource fadeIn, float targetVolume, AudioSource fadeOut, float duration)
    {
        duration = Mathf.Max(0.01f, duration);
        float inStart = fadeIn != null ? fadeIn.volume : 0f;
        float outStart = fadeOut != null ? fadeOut.volume : 0f;

        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = t / duration;
            if (fadeIn != null)
                fadeIn.volume = Mathf.Lerp(inStart, targetVolume, k);
            if (fadeOut != null)
                fadeOut.volume = Mathf.Lerp(outStart, 0f, k);
            yield return null;
        }

        if (fadeIn != null)
            fadeIn.volume = targetVolume;

        if (fadeOut != null)
        {
            fadeOut.volume = 0f;
            fadeOut.Stop();
            fadeOut.clip = null;
        }

        _fadeJob = null;
    }
}
