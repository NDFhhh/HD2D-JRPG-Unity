using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 对话面板（表现层）。UI 由你在编辑器里用美术资产搭建的预制体提供，
/// 此组件只负责运行时逻辑：打字机、选项生成、继续指示器动画、中文字体解析。
///
/// 接线要求：
/// 1. 在 prefab 根物体上挂本组件，把各 UI 元素拖入下方字段；
/// 2. 窗口背景图需勾选 Raycast Target（支持点击推进对话）；
/// 3. 选项模板是 ChoiceRoot 下的一个 Button（含 TMP 文本子物体），会被克隆复用；
/// 4. 所有文本 TMP 无需手动指定字体，运行时会自动解析项目中的思源宋体。
/// </summary>
public class DialoguePanelController : MonoBehaviour, IPointerClickHandler
{
    [Header("窗口（背景图需勾选 Raycast Target 以支持点击推进）")]
    [SerializeField] private Image windowBackground;

    [Header("姓名牌")]
    [SerializeField] private GameObject namePlate;
    [SerializeField] private TextMeshProUGUI nameText;

    [Header("正文")]
    [SerializeField] private TextMeshProUGUI bodyText;
    [SerializeField] private Image portraitImage;

    [Header("正文左边距（有立绘时正文右移）")]
    [SerializeField] private float bodyLeftNormal = 48f;
    [SerializeField] private float bodyLeftWithPortrait = 216f;

    [Header("继续指示器（可选，上下浮动）")]
    [SerializeField] private RectTransform continueIndicator;

    [Header("选项")]
    [SerializeField] private RectTransform choiceRoot;
    [Tooltip("ChoiceRoot 下的选项按钮模板，运行时会被克隆，模板本身自动隐藏")]
    [SerializeField] private Button choiceButtonTemplate;

    [Header("打字机")]
    [SerializeField] private float charsPerSecond = 28f;
    [SerializeField] private float punctuationPause = 0.18f;

    private DialogueRunner _runner;
    private AudioSource _audioSource;
    private AudioClip _currentBlip;

    private Coroutine _typingRoutine;
    private bool _isTyping;
    private bool _skipRequested;
    private int _lastChoiceFrame = -1;
    private TMP_FontAsset _font;
    private Vector2 _indicatorBasePos;
    private bool _indicatorBaseCaptured;

    private readonly List<Button> _choiceButtons = new();

    #region 生命周期

    private void Awake()
    {
        // 窗口背景图作为点击推进的接收区域
        if (windowBackground is not null)
            windowBackground.raycastTarget = true;

        // 选项模板默认隐藏，仅作为克隆源
        if (choiceButtonTemplate is not null)
            choiceButtonTemplate.gameObject.SetActive(false);

        _audioSource = GetComponent<AudioSource>();
        if (_audioSource is null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
        }

        CaptureIndicatorBase();
    }

    private void CaptureIndicatorBase()
    {
        if (_indicatorBaseCaptured || continueIndicator is null)
            return;
        _indicatorBasePos = continueIndicator.anchoredPosition;
        _indicatorBaseCaptured = true;
    }

    /// <summary>绑定对话状态机，面板从此只做事件的响应者</summary>
    public void Bind(DialogueRunner runner)
    {
        _runner = runner;
        _runner.NodeEntered += HandleNodeEntered;
    }

    public void Open()
    {
        ResolveFont();
        gameObject.SetActive(true);
    }

    public void Close()
    {
        if (_typingRoutine is not null)
        {
            StopCoroutine(_typingRoutine);
            _typingRoutine = null;
        }
        _isTyping = false;
        HideChoices();
        if (continueIndicator is not null)
            continueIndicator.gameObject.SetActive(false);
        gameObject.SetActive(false);
    }

    private void Update()
    {
        // 继续指示器上下浮动
        if (continueIndicator is not null && continueIndicator.gameObject.activeSelf)
        {
            float y = _indicatorBasePos.y + Mathf.Sin(Time.unscaledTime * 5f) * 5f;
            continueIndicator.anchoredPosition = new Vector2(_indicatorBasePos.x, y);
        }
    }

    #endregion

    #region 节点渲染

    private void HandleNodeEntered(DialogueNode node)
    {
        Speaker_SO speaker = node.Speaker;

        // 姓名牌：无说话人或无名时隐藏（旁白）
        if (namePlate is not null)
        {
            bool showName = speaker is not null && !string.IsNullOrEmpty(speaker.SpeakerName);
            namePlate.SetActive(showName);
            if (showName && nameText is not null)
            {
                nameText.text = speaker.SpeakerName;
                nameText.color = speaker.NameColor;
            }
        }

        // 立绘
        Sprite portrait = speaker is not null ? speaker.Portrait : null;
        bool showPortrait = portrait is not null;
        if (portraitImage is not null)
        {
            portraitImage.gameObject.SetActive(showPortrait);
            if (showPortrait)
                portraitImage.sprite = portrait;
        }
        SetBodyMargin(showPortrait);

        // 打字机音效
        _currentBlip = speaker is not null ? speaker.VoiceBlip : null;

        // 隐藏旧选项，开始打字
        HideChoices();
        if (continueIndicator is not null)
            continueIndicator.gameObject.SetActive(false);
        if (_typingRoutine is not null)
            StopCoroutine(_typingRoutine);
        _typingRoutine = StartCoroutine(TypeRoutine(node.Text));
    }

    private void SetBodyMargin(bool hasPortrait)
    {
        if (bodyText is null)
            return;
        float left = hasPortrait ? bodyLeftWithPortrait : bodyLeftNormal;
        bodyText.rectTransform.offsetMin = new Vector2(left, bodyText.rectTransform.offsetMin.y);
    }

    #endregion

    #region 打字机

    private IEnumerator TypeRoutine(string content)
    {
        _isTyping = true;
        _skipRequested = false;

        if (bodyText is null)
        {
            Debug.LogError("[DialoguePanelController] 未配置正文文本（bodyText），对话无法显示");
            FinishTyping();
            yield break;
        }

        bodyText.text = content ?? string.Empty;
        bodyText.maxVisibleCharacters = 0;

        // 面板刚激活时 TMP 尚未完成首帧布局，字符数会读到 0，导致打字机被整体跳过。
        // 先等一帧让 Canvas 完成布局重建，再强制重建网格取真实字符数。
        // 用 totalVisibleCharacters（排除富文本标签占位），比 characterCount 更可靠。
        yield return null;
        bodyText.ForceMeshUpdate();

        int total = bodyText.textInfo.characterCount;
        if (total <= 0)
        {
            // 极端时序下再等一帧；仍无字符说明文本为空，直接结束避免死循环
            yield return null;
            bodyText.ForceMeshUpdate();
            total = bodyText.textInfo.characterCount;
            if (total <= 0)
            {
                FinishTyping();
                yield break;
            }
        }

        float shown = 0f;
        float pauseTimer = 0f;
        int blipCounter = 0;

        while (bodyText.maxVisibleCharacters < total)
        {
            if (_skipRequested)
            {
                bodyText.maxVisibleCharacters = total;
                break;
            }

            // 标点停顿：只暂停推进，计数器保持只增不减（回退会导致尾部字符闪烁）
            if (pauseTimer > 0f)
            {
                pauseTimer -= Time.unscaledDeltaTime;
                yield return null;
                continue;
            }

            shown += Time.unscaledDeltaTime * charsPerSecond;
            int count = Mathf.Min(total, Mathf.FloorToInt(shown));
            if (count > bodyText.maxVisibleCharacters)
            {
                bodyText.maxVisibleCharacters = count;

                if (_currentBlip is not null && _audioSource is not null && blipCounter++ % 2 == 0)
                    _audioSource.PlayOneShot(_currentBlip, 0.3f);

                // 标点处稍作停顿，更有节奏感
                if (IsPunctuationChar(count - 1))
                    pauseTimer = punctuationPause;
            }
            yield return null;
        }

        FinishTyping();
    }

    private const string PunctuationChars = "，。！？；：、…—,.!?;:\"'";

    /// <summary>读取第 visibleIndex 个可见字符是否为标点（带数组越界防御）</summary>
    private bool IsPunctuationChar(int visibleIndex)
    {
        // 调用方已保证 bodyText 非空；此处仍防御富文本下 characterInfo 长度与可见字符数不一致导致的越界
        TMP_TextInfo info = bodyText.textInfo;
        if (info is null || info.characterInfo is null || info.characterInfo.Length == 0)
            return false;

        int index = Mathf.Clamp(visibleIndex, 0, info.characterInfo.Length - 1);
        return PunctuationChars.IndexOf(info.characterInfo[index].character) >= 0;
    }

    private void FinishTyping()
    {
        _isTyping = false;
        _typingRoutine = null;

        if (_runner.VisibleChoices.Count > 0)
            ShowChoices();
        else if (continueIndicator is not null)
            continueIndicator.gameObject.SetActive(true);
    }

    #endregion

    #region 选项

    private void ShowChoices()
    {
        if (choiceRoot is null)
        {
            Debug.LogWarning("[DialoguePanelController] 未配置选项容器（choiceRoot），分支选项将无法显示");
            return;
        }

        IReadOnlyList<DialogueChoice> choices = _runner.VisibleChoices;

        while (_choiceButtons.Count < choices.Count)
            _choiceButtons.Add(CreateChoiceButton(_choiceButtons.Count));

        for (int i = 0; i < _choiceButtons.Count; i++)
        {
            bool used = i < choices.Count;
            Button button = _choiceButtons[i];
            if (button is null)
                continue;
            button.gameObject.SetActive(used);
            if (!used)
                continue;

            var label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label is not null)
            {
                label.text = choices[i].Text;
                if (_font is not null)
                    label.font = _font;
            }

            int captured = i;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => SelectChoice(captured));
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(choiceRoot);

        // 手柄/键盘：默认选中第一个选项
        if (choices.Count > 0 && EventSystem.current is not null)
            EventSystem.current.SetSelectedGameObject(_choiceButtons[0].gameObject);
    }

    private Button CreateChoiceButton(int index)
    {
        if (choiceButtonTemplate is not null)
        {
            var clone = Instantiate(choiceButtonTemplate, choiceRoot);
            clone.name = $"Choice_{index}";
            clone.gameObject.SetActive(true);
            return clone;
        }

        Debug.LogWarning("[DialoguePanelController] 未配置选项按钮模板（choiceButtonTemplate），选项将无法显示");
        return null;
    }

    private void HideChoices()
    {
        for (int i = 0; i < _choiceButtons.Count; i++)
        {
            if (_choiceButtons[i] is not null)
                _choiceButtons[i].gameObject.SetActive(false);
        }
    }

    private void SelectChoice(int index)
    {
        _lastChoiceFrame = Time.frameCount;
        HideChoices();
        _runner.SelectChoice(index);
    }

    #endregion

    #region 输入

    /// <summary>
    /// 推进输入：打字中 → 补全全文；等待中 → 下一节点；有选项时忽略（必须做选择）。
    /// 由点击（OnPointerClick）与 DialogueManager 的 Submit 输入共同调用。
    /// </summary>
    public void HandleAdvanceInput()
    {
        if (!isActiveAndEnabled || _runner is null || !_runner.IsRunning)
            return;
        if (Time.frameCount == _lastChoiceFrame)
            return;

        if (_isTyping)
        {
            _skipRequested = true;
            return;
        }

        if (_runner.VisibleChoices.Count > 0)
            return;

        if (continueIndicator is not null)
            continueIndicator.gameObject.SetActive(false);
        _runner.Advance();
    }

    public void OnPointerClick(PointerEventData eventData) => HandleAdvanceInput();

    #endregion

    #region 字体

    /// <summary>
    /// 运行时解析中文字体：优先 OverrideFont，其次项目中已加载的 SourceHanSerifSC，
    /// 最后退回 TMP 默认字体（不含中文字形，仅兜底）。
    /// </summary>
    private void ResolveFont()
    {
        if (DialogueManager.OverrideFont is not null)
        {
            ApplyFont(DialogueManager.OverrideFont);
            return;
        }

        TMP_FontAsset[] loaded = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        TMP_FontAsset anyHan = null;
        for (int i = 0; i < loaded.Length; i++)
        {
            TMP_FontAsset font = loaded[i];
            if (font is null || !font.name.Contains("SourceHanSerifSC"))
                continue;
            if (font.name.Contains("Regular"))
            {
                ApplyFont(font);
                return;
            }
            anyHan ??= font;
        }

        if (anyHan is not null)
        {
            ApplyFont(anyHan);
            return;
        }

        if (_font is null && TMP_Settings.defaultFontAsset is not null)
            ApplyFont(TMP_Settings.defaultFontAsset);
    }

    private void ApplyFont(TMP_FontAsset font)
    {
        if (font is null || _font == font)
            return;
        _font = font;
        if (nameText is not null)
            nameText.font = font;
        if (bodyText is not null)
            bodyText.font = font;
        for (int i = 0; i < _choiceButtons.Count; i++)
        {
            if (_choiceButtons[i] is null)
                continue;
            var label = _choiceButtons[i].GetComponentInChildren<TextMeshProUGUI>(true);
            if (label is not null)
                label.font = font;
        }
    }

    #endregion
}
