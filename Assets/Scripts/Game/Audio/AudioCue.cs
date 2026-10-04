using Random = UnityEngine.Random;

/// <summary>
/// 单条音效的可播放描述（ScriptableObject 资产）
/// 支持随机变体、随机音高、并发上限、触发冷却，用于打散重复感
/// </summary>
[CreateAssetMenu(fileName = "NewAudioCue", menuName = "CodePath/Audio/AudioCue")]
public class AudioCue : ScriptableObject
{
    [System.Serializable]
    public class ClipVariant
    {
        public AudioClip Clip;
        [Range(0f, 1f)] public float Volume = 1f;
    }

    [Tooltip("同一条音效的可选变体，播放时随机抽取，避免重复感")]
    [SerializeField] private ClipVariant[] variants = { };

    [Tooltip("随机音高区间，进一步打散重复感（x == y 时不随机）")]
    [SerializeField] private Vector2 pitchRange = new(0.95f, 1.05f);

    [Tooltip("该音效最多同时存在的发声数，超出则丢弃新的触发")]
    [SerializeField, Range(1, 8)] private int maxVoices = 4;

    [Tooltip("两次触发之间的最小间隔（秒），防止事件风暴导致爆音")]
    [SerializeField, Min(0f)] private float cooldown = 0.04f;

    public int MaxVoices => maxVoices;
    public float Cooldown => cooldown;
    public bool IsEmpty => variants == null || variants.Length == 0;

    /// <summary> 最长变体时长，用于估算发声占用窗口 </summary>
    public float LongestLength
    {
        get
        {
            float len = 0f;
            if (variants != null)
                for (int i = 0; i < variants.Length; i++)
                {
                    var clip = variants[i].Clip;
                    if (clip != null)
                        len = Mathf.Max(len, clip.length);
                }
            return len;
        }
    }

    /// <summary>
    /// 最长实际播放时长：最长变体 ÷ 最低音调（与 SFXPlayer 的 clip.length / pitch 计算保持一致）
    /// 用于 “等音效播完再继续” 的流程等待
    /// </summary>
    public float LongestPlayableDuration
        => LongestLength / Mathf.Max(0.1f, pitchRange.x);

    /// <summary> 随机取一个变体（SFX 用） </summary>
    public bool TryGetNext(out AudioClip clip, out float volume, out float pitch)
    {
        clip = null;
        volume = 1f;
        pitch = 1f;

        if (IsEmpty)
            return false;

        var variant = variants[UnityEngine.Random.Range(0, variants.Length)];
        if (variant.Clip == null)
            return false;

        clip = variant.Clip;
        volume = variant.Volume;
        pitch = pitchRange.x >= pitchRange.y
            ? pitchRange.x
            : UnityEngine.Random.Range(pitchRange.x, pitchRange.y);
        return true;
    }

    /// <summary> 取首个变体（BGM 用，保证曲目确定性） </summary>
    public bool TryGetPrimary(out AudioClip clip, out float volume)
    {
        clip = null;
        volume = 1f;

        if (IsEmpty || variants[0].Clip == null)
            return false;

        clip = variants[0].Clip;
        volume = variants[0].Volume;
        return true;
    }
}
