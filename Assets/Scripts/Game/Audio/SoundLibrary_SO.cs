/// <summary>
/// 音频资源总索引（全局只建一份资产）
/// Inspector 中把 E_SoundId 枚举项映射到 AudioCue 资产，新增音效零代码改动
/// </summary>
[CreateAssetMenu(fileName = "SoundLibrary", menuName = "CodePath/Audio/SoundLibrary")]
public class SoundLibrary_SO : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        public E_SoundId Id;
        public AudioCue Cue;
    }

    [Tooltip("全部音效索引表：枚举项 -> AudioCue 资产")]
    [SerializeField] private List<Entry> entries = new();

    private readonly Dictionary<E_SoundId, AudioCue> _lookup = new();

    public IReadOnlyList<Entry> Entries => entries;

    private void OnEnable() => RebuildLookup();

    private void OnValidate() => RebuildLookup();

    private void RebuildLookup()
    {
        _lookup.Clear();
        if (entries == null)
            return;
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry == null || entry.Cue == null)
                continue;
            if (_lookup.ContainsKey(entry.Id))
                Debug.LogWarning($"[Audio] SoundLibrary 中 {entry.Id} 存在重复配置，已忽略后者");
            else
                _lookup.Add(entry.Id, entry.Cue);
        }
    }

    public bool TryGetCue(E_SoundId id, out AudioCue cue)
    {
        if (_lookup.Count == 0)
            RebuildLookup();
        return _lookup.TryGetValue(id, out cue) && cue != null;
    }
}
