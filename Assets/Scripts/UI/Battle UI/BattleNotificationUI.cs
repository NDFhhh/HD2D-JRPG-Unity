using DG.Tweening;
using MFramework.Event;
using TMPro;

public class BattleNotificationUI : MonoBehaviour,
    IEventReceiver<SkillNameDisplayEvent>,
    IEventReceiver<BattleNotificationEvent>
{
    #region 通知条配置

    [Header("UI 引用")]
    [SerializeField] private GameObject notificationRoot;
    [SerializeField] private TMP_Text notificationText;
    [SerializeField] private CanvasGroup notificationCanvasGroup;

    [Header("动画参数")] 
    [SerializeField, Min(0f)] private float fadeInDuration = 0.15f;
    [SerializeField, Min(0f)] private float displayDuration = 1.5f;
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.3f;
    
    [Header("颜色配置")]
    [SerializeField] private Color successColor = new Color(1f,0.8f,0f);
    [SerializeField] private Color failureColor = new Color(0.7f,0.7f,0.7f);

    #endregion

    #region 技能名提示配置

    [Header("技能名提示")] 
    [SerializeField] private GameObject skillNameRoot;
    [SerializeField] private TMP_Text skillNameText;
    [SerializeField] private CanvasGroup skillNameCanvasGroup;
    [SerializeField,Min(0f)] private float skillNameFadeInDuration = 0.12f;
    [SerializeField, Min(0f)] private float skillNameDisplayDuration = 1.2f;
    [SerializeField, Min(0f)] private float skillNameFadeOutDuration = 0.18f;

    #endregion

    #region 运行时状态

    private Tween _notificationTween;
    private Tween _skillNameTween;

    #endregion

    private void Awake()
    {
        notificationRoot.SetActive(false);
        skillNameRoot.SetActive(false);
        notificationCanvasGroup.alpha = 0f;
        skillNameCanvasGroup.alpha = 0f;
    }

    private void OnEnable()
    {
        EventBus.Subscribe<SkillNameDisplayEvent>(this);
        EventBus.Subscribe<BattleNotificationEvent>(this);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<SkillNameDisplayEvent>(this);
        EventBus.Unsubscribe<BattleNotificationEvent>(this);

        _notificationTween?.Kill();
        _skillNameTween?.Kill();
        
        notificationRoot.SetActive(false);
        skillNameRoot.SetActive(false);
        notificationCanvasGroup.alpha = 0f;
        skillNameCanvasGroup.alpha = 0f;
    }

    #region 实现接口

    public void OnEvent(BattleNotificationEvent evt)
    {
        notificationText.text = evt.Message;
        notificationText.color = evt.IsSuccess ? successColor : failureColor;
        notificationText.alpha = 1f;

        _notificationTween?.Kill();

        // 事件可指定显示总时长（如 BOSS 阶段提示与阶段音效时长一致）
        // 总时长 = 淡入 + 停留 + 淡出，这里反推出停留时长；<= 0 时使用默认停留时长
        float holdDuration = displayDuration;
        if (evt.DisplayDuration > 0f)
            holdDuration = Mathf.Max(0f, evt.DisplayDuration - fadeInDuration - fadeOutDuration);

        _notificationTween = PlayFadeSequence(
            notificationRoot,
            notificationCanvasGroup,
            fadeInDuration,
            holdDuration,
            fadeOutDuration,
            () =>
            {
                notificationRoot.SetActive(false);
                _notificationTween = null;
            });
    }
    
    public void OnEvent(SkillNameDisplayEvent evt)
    {
        skillNameText.text = evt.SkillName;
        skillNameCanvasGroup.alpha = 1f;
        
        _skillNameTween?.Kill();

        _skillNameTween = PlayFadeSequence(
            skillNameRoot,
            skillNameCanvasGroup,
            skillNameFadeInDuration,
            skillNameDisplayDuration,
            skillNameFadeOutDuration,
            () =>
            {
                skillNameRoot.SetActive(false);
                _skillNameTween = null;
            });

    }

    private Tween PlayFadeSequence(GameObject root, CanvasGroup targetCanvasGroup, float fadeInDuration,
        float displayDuration, float fadeOutDuration, TweenCallback onFinished)
    {
        root.SetActive(true);
        targetCanvasGroup.alpha = 0f;
        
        // DOTween.Sequence()：创建一个时间轴容器，把后面的多个 Tween 串成一段完整演出。
        // SetUpdate(true)：让这段 UI 动画不受 Time.timeScale 影响，暂停或慢动作时也能正常播放。
        // Append(...)：把一个 Tween 接到时间轴尾部，这里先接“从 0 淡到 1”的淡入动画。
        // AppendInterval(...)：在时间轴尾部插入一段纯等待时间，用来让文字在屏幕上停留一会。
        // Append(...)：继续把淡出动画接到等待后面，形成“淡入 -> 停留 -> 淡出”的完整顺序。
        // OnComplete(...)：整条时间轴播完后执行收尾逻辑，比如隐藏物体、清空 Tween 引用。
        return DOTween.Sequence()
            .SetUpdate(true)
            .Append(targetCanvasGroup.DOFade(1f, fadeInDuration))
            .AppendInterval(displayDuration)
            .Append(targetCanvasGroup.DOFade(0f, fadeOutDuration))
            .OnComplete(onFinished);
    }

    #endregion
    
}
