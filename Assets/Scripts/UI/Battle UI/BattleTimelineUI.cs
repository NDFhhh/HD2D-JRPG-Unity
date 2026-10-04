using DG.Tweening;
using TMPro;
using UnityEngine.Pool;
using UnityEngine.UI;

public class BattleTimelineUI : MonoBehaviour
{
    [SerializeField] private TimelineIcon timelineIconPrefab;
    
    [Header("Containers")]
    [SerializeField] private RectTransform currentRoundContainer;
    [SerializeField] private RectTransform nextRoundContainer;
    
    [Header("Active Unit Display")]
    [SerializeField] private Image activeUnitPortrait;
    [SerializeField] private TMP_Text activeUnitName;
    
    [Header("Animation")]
    [SerializeField] private float animDuration = 0.5f;    // 图标淡入和滑动的基础时长
    [SerializeField] private Ease moveEase = Ease.OutQuart;    // 滑动时的缓动曲线
    [SerializeField] private float spawnStagger = 0.03f;    // 队列头像依次出现的时间
    [SerializeField] private float spawnOffsetX = 40f;    // 入场先从右侧偏一点，再滑回原位

    private IObjectPool<TimelineIcon> _pool;
    
    // key 使用预测节点的 uniqueID，避免同角色跨回合显示冲突
    private readonly Dictionary<string, TimelineIcon> _activeIconMap = new();

    private void Awake()
    {
        // 初始化 Unity 对象池
        _pool = new ObjectPool<TimelineIcon>(
            // 创建时：先不指定父物体，或指定为 transform (Root)
            createFunc: () => Instantiate(timelineIconPrefab, transform),
            actionOnGet: (icon) =>
            {
                icon.gameObject.SetActive(true);
            },
            actionOnRelease: (icon) =>
            {
                icon.gameObject.SetActive(false);
                icon.transform.SetParent(transform); // 回收时挂到根节点下藏起来

                // 回收时重置视觉偏移，避免复用后还停留在上次动画位置
                // icon.ResetForPool();
            },
            actionOnDestroy: (icon) => Destroy(icon.gameObject),
            defaultCapacity: 10,
            maxSize: 20
        );
    }

    #region 时间轴刷新主流程

    public void UpdateTimeline(List<BattleTimelinePredictionNode> predictions)
    {
        // 1. 记录这次预测中包含的 ID (用于稍后清理图标)
        HashSet<string> keptIDs = new();
        
        // 2. 核心循环
        // 列表展示 “当前可执行 + 下一回合预测” 的完整顺序
        for (int i = 0; i < predictions.Count; i++)
        {
            BattleTimelinePredictionNode node = predictions[i];
            keptIDs.Add(node.UniqueID);

            RectTransform targetParent = (node.Round == 0) ? currentRoundContainer : nextRoundContainer;

            TimelineIcon icon;

            if (_activeIconMap.TryGetValue(node.UniqueID, out icon))
            {
                // 【老图标】：直接更新父级与顺序(不做移动动画)
                icon.transform.SetParent(targetParent, false);
                icon.transform.SetAsLastSibling();
                icon.Setup(node.Entity);
            }
            else
            {
                icon = GetIcon(targetParent);
                _activeIconMap[node.UniqueID] = icon;
                icon.Setup(node.Entity);

                // 动画
                if (isActiveAndEnabled)
                {
                    float delay = spawnStagger * i;
                    StartCoroutine(AnimateSpawn(icon, targetParent, delay));
                }
            }
        }
        
        // 3. 清理旧图标
        List<string> toRemoveIDs = new();
        foreach (var pair in _activeIconMap)
        {
            if (!keptIDs.Contains(pair.Key))
            {
                toRemoveIDs.Add(pair.Key);
                var icon = pair.Value;
                
                // 动画
                if (isActiveAndEnabled)
                {
                    StartCoroutine(AnimateDespawn(icon));
                }
                else
                {
                    _pool.Release(icon);
                }
            }
        }

        foreach (var id in toRemoveIDs)
        {
            _activeIconMap.Remove(id);
        }
        
        LayoutRebuilder.ForceRebuildLayoutImmediate(currentRoundContainer);
        LayoutRebuilder.ForceRebuildLayoutImmediate(nextRoundContainer);
    }

    private TimelineIcon GetIcon(Transform parent)
    {
        var icon = _pool.Get();
        icon.transform.SetParent(parent, false);
        icon.transform.SetAsLastSibling();
        return icon;
    }

    #endregion

    #region 当前行动者焦点辅助

    public void SetActiveEntity(BattleEntity entity)
    {
        UpdateActiveUnitFrame(entity);
        
        // 动画
        PlayActionUnitFrameAnimation();
    }

    private void UpdateActiveUnitFrame(BattleEntity entity)
    {
        ClearActiveUnitFrame();
        
        var definition = entity.RuntimeData.Definition;
        activeUnitPortrait.sprite = definition.Portrait;
        activeUnitPortrait.enabled = true;
        
        activeUnitName.text = definition.Name;
    }

    private void ClearActiveUnitFrame()
    {
        activeUnitPortrait.enabled = false;
        activeUnitName.text = "";
    }

    #endregion

    #region 时间轴动画协程

    /// <summary>
    /// 新图标入场动画：轻位移 + 淡入
    /// </summary>
    private IEnumerator AnimateSpawn(TimelineIcon icon, RectTransform container, float delay)
    {
        if (delay > 0)
            yield return new WaitForSeconds(delay);

        icon.PlayEntranceAnimation(animDuration, spawnOffsetX, moveEase);

        yield return null;
        LayoutRebuilder.ForceRebuildLayoutImmediate(container);
    }

    /// <summary>
    /// 旧图标离场动画：轻位移 + 淡出后回收到对象池
    /// </summary>
    private IEnumerator AnimateDespawn(TimelineIcon icon)
    {
        // 退场保持和入场同一种滑动风格，只是时长稍微更短，看起来会更干脆
        float duration = animDuration * 0.6f;
        icon.PlayEntranceAnimation(duration, -spawnOffsetX, moveEase);
        yield return new WaitForSeconds(duration);
        
        _pool.Release(icon);
    }

    private void PlayActionUnitFrameAnimation()
    {
        activeUnitPortrait.transform.DOKill();
        activeUnitPortrait.transform.localScale = Vector3.one * 0.5f;
        activeUnitPortrait.transform.DOScale(2f,0.4f).SetEase(Ease.OutBack);
        // activeUnitPortrait.SetNativeSize();
    }

    #endregion
    
}
