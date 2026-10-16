using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Yarn.Markup;
using Yarn.Unity;

/// <summary>
/// 对话面板（表现层）：Yarn Spinner 的 Dialogue Presenter 实现。
/// UI 由你在编辑器里用美术资产搭建的预制体提供，此组件只负责运行时逻辑：
/// 打字机、选项生成、继续指示器动画、立绘/姓名牌、中文字体解析。
///
/// 接线要求：
/// 1. 在 prefab 根物体上挂本组件，把各 UI 元素拖入下方字段；
/// 2. 窗口背景图需勾选 Raycast Target（支持点击推进对话）；
/// 3. 选项模板是 ChoiceRoot 下的一个 Button（含 TMP 文本子物体），会被克隆复用；
/// 4. 所有文本 TMP 无需手动指定字体，运行时会自动解析项目中的思源宋体；
/// 5. 本组件需被加进 Yarn DialogueRunner 的 Dialogue Presenters 列表
///    （DialogueManager 会自动完成，无需手动挂场景）。
///
/// Yarn 侧约定：
/// - 说话人用 **Speaker Id** 充当 CharacterName，如 <c>traveller: 你好。</c>，
///   运行时经 <see cref="DialogueSpeakerRegistry"/> 查回 Speaker_SO 拿颜色/立绘/音效；
/// - 玩家输入走 <see cref="HandleAdvanceInput"/>（点击 / Submit），
///   由 DialogueManager 翻译成 Yarn 的「加速」或「下一句」。
/// </summary>
public class DialoguePanelController : DialoguePresenterBase, IPointerClickHandler
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

    private AudioSource _audioSource;
    private AudioClip _currentBlip;

    private Coroutine _typingRoutine;
    private bool _isTyping;
    private bool _optionsActive;
    private int _selectedOption = -1;
    private int _lastChoiceFrame = -1;
    private TMP_FontAsset _font;
    private Vector2 _indicatorBasePos;
    private bool _indicatorBaseCaptured;
    private bool _initialized;

    private readonly List<Button> _choiceButtons = new();

    /// <summary>当前是否正在逐字打字（DialogueManager 据此决定推进语义：跳过 or 下一句）</summary>
    public bool IsTyping => _isTyping;

    /// <summary>当前是否正在等待玩家做选项选择</summary>
    public bool IsAwaitingChoice => _optionsActive;

    #region Yarn Presenter 生命周期

    /// <summary>对话开始：激活面板并做一次性初始化</summary>
    public override YarnTask OnDialogueStartedAsync()
    {
        // 面板可能初始为 inactive（组件不跑 Awake），所以先激活再初始化
        gameObject.SetActive(true);
        EnsureInitialized();
        ResolveFont();
        return YarnTask.CompletedTask;
    }

    /// <summary>对话结束：收起面板、清理残留状态</summary>
    public override YarnTask OnDialogueCompleteAsync()
    {
        Close();
        return YarnTask.CompletedTask;
    }

    /// <summary>
    /// 呈现一行对话：解析说话人 → 打字机逐字显示 → 等玩家推进。
    /// 打字期间若收到 Yarn 的「加速」信号（HurryUpToken），立即补全全文。
    /// </summary>
    public override async YarnTask RunLineAsync(LocalizedLine line, LineCancellationToken token)
    {
        EnsureInitialized();

        Speaker_SO speaker = DialogueSpeakerRegistry.Get(line.CharacterName);
        ApplySpeaker(speaker);

        _currentBlip = speaker is not null ? speaker.VoiceBlip : null;

        // 新行开始：收起上一轮的选项与指示器
        HideChoices();
        HideIndicator();

        if (_typingRoutine is not null)
            StopCoroutine(_typingRoutine);

        // 必须用 TextWithoutCharacterName：Yarn 的 line.Text 里**含**说话人名字，
        // 直接用它会让名字既出现在姓名牌、又在正文里重复一遍。
        // 富文本标记交给 BuildRichText 翻译成 TMP 标签（打字机按可见字符数推进，标签不占位）。
        _typingRoutine = StartCoroutine(TypeRoutine(BuildRichText(line.TextWithoutCharacterName), token));
        await this.WaitForCoroutine(_typingRoutine);

        // 打字完成，等玩家点击/Submit 推进（Yarn 会取消 NextContentToken）
        ShowIndicator();
        await YarnTask.WaitUntilCanceled(token.NextContentToken).SuppressCancellationThrow();
        HideIndicator();
    }

    /// <summary>
    /// 呈现分支选项：生成按钮、等玩家选择、返回选中的 option。
    /// 若本 presenter 不处理选项则返回 null（Yarn 视为「未选择」）。
    /// </summary>
    /// <remarks>
    /// 返回类型刻意写作 <c>YarnTask&lt;DialogueOption&gt;</c>（不带 <c>?</c>）：
    /// 本工程编译参数未开启 nullable 上下文，写 <c>DialogueOption?</c> 只会得到
    /// CS8632 警告。可空注解不影响重写匹配，基类同名方法照常被重写。
    /// </remarks>
    public override async YarnTask<DialogueOption> RunOptionsAsync(DialogueOption[] dialogueOptions, LineCancellationToken token)
    {
        EnsureInitialized();

        HideIndicator();

        _selectedOption = -1;
        _optionsActive = true;
        BuildChoices(dialogueOptions);

        // 等某个选项被点中（对话被强制结束时会走 token 取消分支）
        await YarnTask.WaitUntil(() => _selectedOption >= 0, token.NextContentToken).SuppressCancellationThrow();

        _optionsActive = false;
        HideChoices();

        if (_selectedOption >= 0 && _selectedOption < dialogueOptions.Length)
            return dialogueOptions[_selectedOption];
        return null;
    }

    #endregion

    #region 生命周期与初始化

    private void Awake() => EnsureInitialized();

    /// <summary>幂等初始化：面板可能在 inactive 状态下被创建（Awake 不执行）</summary>
    private void EnsureInitialized()
    {
        if (_initialized)
            return;
        _initialized = true;

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
        _optionsActive = false;
        HideChoices();
        HideIndicator();
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

    private void ShowIndicator()
    {
        if (continueIndicator is not null)
            continueIndicator.gameObject.SetActive(true);
    }

    private void HideIndicator()
    {
        if (continueIndicator is not null)
            continueIndicator.gameObject.SetActive(false);
    }

    #endregion

    #region 说话人渲染

    /// <summary>按 Speaker_SO 刷新姓名牌 / 立绘 / 正文左边距</summary>
    private void ApplySpeaker(Speaker_SO speaker)
    {
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

    #region Yarn markup → TMP 富文本

    /// <summary>
    /// 把 Yarn 的方括号标记翻译成 TMP 富文本标签。
    /// </summary>
    /// <remarks>
    /// Yarn 解析时已把标记从文本里剥掉，只在 <see cref="MarkupParseResult.Attributes"/>
    /// 里留下「位置 + 长度 + 名字 + 属性」，所以这里按属性重建 TMP 标签。
    ///
    /// 只翻译能与 TMP 一一对应的少数标签（b / i / u / s / color / size）；
    /// 演出类标记（wave / shake / pause 等）会被静默忽略，不会作为乱码显示出来。
    ///
    /// 颜色受 Yarn 语法限制不能写 <c>#</c>（它是元数据分隔符，会把整行后半截吞掉），
    /// 所以脚本里写 <c>[color=E8C97A]</c>，由这里补回 <c>#</c> 再交给 TMP。
    /// </remarks>
    private static string BuildRichText(MarkupParseResult source)
    {
        // 注意：MarkupParseResult 是 **struct**，不能写 `source is null`（会报 CS0037）。
        // 结构体默认值的 Text / Attributes 为 null，靠下面的空判断兜住。
        string plain = source.Text;
        if (string.IsNullOrEmpty(plain))
            return string.Empty;

        var attributes = source.Attributes;
        if (attributes is null || attributes.Count == 0)
            return plain;

        var pending = new List<MarkupTagRange>();

        for (int i = 0; i < attributes.Count; i++)
        {
            MarkupAttribute attribute = attributes[i];
            if (attribute.Length <= 0)
                continue;

            string open;
            string close;
            if (!TryBuildTmpTags(attribute, out open, out close))
                continue;

            int start = Mathf.Clamp(attribute.Position, 0, plain.Length);
            int end = Mathf.Clamp(attribute.Position + attribute.Length, 0, plain.Length);
            if (end <= start)
                continue;

            var range = new MarkupTagRange();
            range.Start = start;
            range.End = end;
            range.Open = open;
            range.Close = close;
            pending.Add(range);
        }

        if (pending.Count == 0)
            return plain;

        // 位置靠后的先插入，避免前面的插入影响尚未处理的下标
        pending.Sort((a, b) => b.Start.CompareTo(a.Start));

        var builder = new StringBuilder(plain);
        for (int i = 0; i < pending.Count; i++)
        {
            builder.Insert(pending[i].End, pending[i].Close);
            builder.Insert(pending[i].Start, pending[i].Open);
        }

        return builder.ToString();
    }

    /// <summary>一条待插入的 TMP 标签区间（纯 C# 小结构，不依赖 Yarn 类型）</summary>
    private struct MarkupTagRange
    {
        public int Start;
        public int End;
        public string Open;
        public string Close;
    }

    /// <summary>把单个 Yarn 标记翻译成一对 TMP 标签；不认识的标记返回 false。</summary>
    private static bool TryBuildTmpTags(MarkupAttribute attribute, out string open, out string close)
    {
        open = null;
        close = null;

        switch (attribute.Name)
        {
            case "b":
                open = "<b>";
                close = "</b>";
                return true;

            case "i":
                open = "<i>";
                close = "</i>";
                return true;

            case "u":
                open = "<u>";
                close = "</u>";
                return true;

            case "s":
                open = "<s>";
                close = "</s>";
                return true;

            case "color":
            case "colour":
            {
                string color = GetShorthandValue(attribute);
                if (string.IsNullOrEmpty(color))
                    return false;

                open = "<color=" + NormalizeColor(color) + ">";
                close = "</color>";
                return true;
            }

            case "size":
            {
                string size = GetShorthandValue(attribute);
                if (string.IsNullOrEmpty(size))
                    return false;

                open = "<size=" + size + ">";
                close = "</size>";
                return true;
            }

            default:
                return false;
        }
    }

    /// <summary>
    /// 取标记的短写属性值：<c>[color=E8C97A]</c> 等价于 <c>[color color=E8C97A]</c>，
    /// 即属性名等于标记名；取不到再回落 <c>value</c>。
    /// </summary>
    private static string GetShorthandValue(MarkupAttribute attribute)
    {
        string value;
        if (attribute.TryGetProperty(attribute.Name, out value) && !string.IsNullOrEmpty(value))
            return value;

        if (attribute.TryGetProperty("value", out value) && !string.IsNullOrEmpty(value))
            return value;

        return null;
    }

    /// <summary>
    /// Yarn 标记里不能出现 <c>#</c>，颜色通常写成 6/8 位十六进制裸值，这里补回 <c>#</c>；
    /// 已是命名色（如 <c>red</c>）则原样交给 TMP 自己解析。
    /// </summary>
    private static string NormalizeColor(string value)
    {
        if (value[0] == '#')
            return value;

        int length = value.Length;
        bool hex = length == 3 || length == 6 || length == 8;

        for (int i = 0; hex && i < length; i++)
        {
            char c = value[i];
            hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
        }

        return hex ? "#" + value : value;
    }

    #endregion

    private IEnumerator TypeRoutine(string content, LineCancellationToken token)
    {
        _isTyping = true;

        if (bodyText is null)
        {
            Debug.LogError("[DialoguePanelController] 未配置正文文本（bodyText），对话无法显示");
            _isTyping = false;
            _typingRoutine = null;
            yield break;
        }

        bodyText.text = content ?? string.Empty;
        bodyText.maxVisibleCharacters = 0;

        // 面板刚激活时 TMP 尚未完成首帧布局，字符数会读到 0，导致打字机被整体跳过。
        // 先等一帧让 Canvas 完成布局重建，再强制重建网格取真实字符数。
        // 用 characterCount（排除富文本标签占位）比直接读字符串长度更可靠。
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
                _isTyping = false;
                _typingRoutine = null;
                yield break;
            }
        }

        float shown = 0f;
        float pauseTimer = 0f;
        int blipCounter = 0;

        while (bodyText.maxVisibleCharacters < total)
        {
            // 玩家请求「加速」时立即补全全文（Yarn 的 HurryUpToken）
            if (token.HurryUpToken.IsCancellationRequested)
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

        _isTyping = false;
        _typingRoutine = null;
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

    #endregion

    #region 选项

    private void BuildChoices(DialogueOption[] options)
    {
        if (choiceRoot is null)
        {
            Debug.LogWarning("[DialoguePanelController] 未配置选项容器（choiceRoot），分支选项将无法显示");
            return;
        }

        while (_choiceButtons.Count < options.Length)
            _choiceButtons.Add(CreateChoiceButton(_choiceButtons.Count));

        for (int i = 0; i < _choiceButtons.Count; i++)
        {
            bool used = i < options.Length;
            Button button = _choiceButtons[i];
            if (button is null)
                continue;
            button.gameObject.SetActive(used);
            if (!used)
                continue;

            var label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label is not null)
            {
                // 选项文本同样带 markup，翻译成 TMP 富文本（并去掉可能存在的说话人前缀）
                label.text = BuildRichText(options[i].Line.TextWithoutCharacterName);
                if (_font is not null)
                    label.font = _font;
            }

            int captured = i;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => SelectChoice(captured));
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(choiceRoot);

        // 手柄/键盘：默认选中第一个选项
        if (options.Length > 0 && EventSystem.current is not null && _choiceButtons[0] is not null)
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
        _selectedOption = index;
        HideChoices();
    }

    #endregion

    #region 输入

    /// <summary>
    /// 推进输入：打字中 → 请求加速补全；等待中 → 下一句；有选项时忽略（必须做选择）。
    /// 由点击（OnPointerClick）与 DialogueManager 的 Submit 输入共同调用。
    /// </summary>
    public void HandleAdvanceInput()
    {
        if (!isActiveAndEnabled)
            return;
        if (Time.frameCount == _lastChoiceFrame)
            return;
        if (_optionsActive)
            return;

        if (continueIndicator is not null)
            continueIndicator.gameObject.SetActive(false);

        // 交给 DialogueManager 翻译成 Yarn 的 HurryUp / NextLine
        DialogueManager.RequestAdvance(_isTyping);
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
