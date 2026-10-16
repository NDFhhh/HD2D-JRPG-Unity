using MFramework.Event;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// 对话系统总入口（自动生成的常驻单例，无需手动挂场景）。
///
/// 用法：<see cref="Play"/> 一行代码播放一段对话。
/// 职责：
/// - 持有 <see cref="DialogueRunner"/>（逻辑）与 <see cref="DialoguePanelController"/>（表现）；
/// - 播放期间把游戏模式切到 InteractionMenu（激活 UI 输入图、冻结玩家移动），结束后恢复；
/// - 广播 <see cref="DialogueStartedEvent"/> / <see cref="DialogueEndedEvent"/>。
/// </summary>
public class DialogueManager : Singleton<DialogueManager>, IEventReceiver<GameModeChangedEvent>
{
    /// <summary>
    /// 全局指定对话字体。不设置时自动复用项目中已加载的 SourceHanSerifSC 字体。
    /// 若自动解析失败，在启动流程里赋值一个中文字体资产即可。
    /// </summary>
    public static TMP_FontAsset OverrideFont;

    /// <summary>
    /// 全局指定对话面板预制体（用你自己的美术资产在编辑器搭好的 prefab，
    /// 根物体挂 DialoguePanelController 并拖好引用）。
    /// 不设置时自动查找场景中已摆放的面板；都没有才生成占位 UI。
    /// 在任意启动脚本的 Awake/Start 里赋值即可，例如：
    /// DialogueManager.PanelPrefab = myPanelPrefab;
    /// </summary>
    public static DialoguePanelController PanelPrefab;

    /// <summary>当前是否有对话正在播放</summary>
    public static bool IsPlaying => Instance is not null && Instance._runner is { IsRunning: true };

    private DialogueRunner _runner;
    private DialoguePanelController _panel;
    private DialogueAsset_SO _currentAsset;

    /// <summary>
    /// 本次对话是否由我们切换了游戏模式。
    /// 为 true 期间对话独占模式：任何系统（如 ESC 触发的面板关闭）把模式切回漫游都会被立即夺回。
    /// </summary>
    private bool _ownsModeSwitch;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoSpawn() => EnsureInstance();

    public static DialogueManager EnsureInstance()
    {
        if (Instance is not null)
            return Instance;

        GameFlagManager.EnsureInstance();

        var go = new GameObject($"[{nameof(DialogueManager)}]");
        DontDestroyOnLoad(go);
        return go.AddComponent<DialogueManager>();
    }

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this)
            return;

        DontDestroyOnLoad(gameObject);

        _runner = new DialogueRunner();
        _runner.DialogueEnded += HandleDialogueEnded;

        BuildUI();
        if (_panel is not null)
            _panel.Bind(_runner);

        EventBus.Subscribe<GameModeChangedEvent>(this);
    }

    private void OnDestroy()
    {
        EventBus.Unsubscribe<GameModeChangedEvent>(this);
    }

    private void BuildUI()
    {
        var canvasGo = new GameObject("DialogueCanvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // 场景里已有 EventSystem 就不重复创建（新项目模板一般都有）
        if (EventSystem.current is null)
        {
            var eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystemGo.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        _panel = ResolvePanel(canvasGo.transform);
        if (_panel is null)
        {
            Debug.LogError("[DialogueManager] 对话面板初始化失败：未找到可用面板");
            return;
        }
    }

    /// <summary>
    /// 解析对话面板，优先级：
    /// 1) 静态指定的 PanelPrefab（你自己的美术资产 prefab）；
    /// 2) 场景中已摆放的面板（如常驻场景里的 DialoguePanel）。
    /// 都不存在时返回 null，由调用方报错——系统不生成任何占位 UI。
    /// </summary>
    private static DialoguePanelController ResolvePanel(Transform canvasRoot)
    {
        if (PanelPrefab is not null)
        {
            var instance = Instantiate(PanelPrefab, canvasRoot);
            instance.gameObject.SetActive(true);
            return instance;
        }

        var scenePanel = FindObjectOfType<DialoguePanelController>(true);
        if (scenePanel is not null)
            return scenePanel;

        Debug.LogWarning("[DialogueManager] 未找到对话面板。请二选一配置：\n" +
                         "① 在任意启动脚本里赋值 DialogueManager.PanelPrefab = 你的面板预制体；\n" +
                         "② 把挂有 DialoguePanelController 的面板 prefab 摆进常驻场景（如 Persistent.scene）。");
        return null;
    }

    #region 播放控制

    /// <summary>播放一段对话。已有对话进行中时会被忽略。</summary>
    public static void Play(DialogueAsset_SO asset)
    {
        if (asset is null)
        {
            Debug.LogWarning("[DialogueManager] 播放失败：对话资产为空");
            return;
        }

        DialogueManager manager = EnsureInstance();
        if (manager._runner.IsRunning)
        {
            Debug.LogWarning($"[DialogueManager] 已有对话进行中，忽略本次播放：{asset.name}");
            return;
        }

        manager.BeginDialogue(asset);
    }

    private void BeginDialogue(DialogueAsset_SO asset)
    {
        _currentAsset = asset;

        // 切到 UI 输入图：冻结玩家移动、激活 Submit/Cancel。
        // 战斗与暂停模式下不切换（战斗内对话不能打断战斗模式）。
        _ownsModeSwitch = false;
        if (GameModeManager.Instance is not null)
        {
            E_GameMode current = GameModeManager.Instance.CurrentGameMode;
            if (current != E_GameMode.Battle && current != E_GameMode.Pause)
            {
                GameModeManager.Instance.RequestChangeMode(E_GameMode.InteractionMenu);
                _ownsModeSwitch = true;
            }
        }

        EventBus.Publish(new DialogueStartedEvent(asset));
        _panel.Open();
        StartCoroutine(BeginTypingAfterPanelReady(asset));
    }

    /// <summary>
    /// 等面板激活并完成一帧渲染后再启动状态机。
    /// 若在 Open()（SetActive 触发 TMP 首帧布局）同一帧就同步 Start，
    /// 面板的 TextMeshPro 尚未完成布局，打字机读到错误字符数会一次性全显。
    /// </summary>
    private IEnumerator BeginTypingAfterPanelReady(DialogueAsset_SO asset)
    {
        yield return null;
        _runner.Start(asset);
    }

    private void HandleDialogueEnded()
    {
        _panel.Close();
        EventBus.Publish(new DialogueEndedEvent(_currentAsset));
        _currentAsset = null;

        // 对话期间是我们切到 InteractionMenu 的，结束后恢复漫游
        if (_ownsModeSwitch &&
            GameModeManager.Instance is not null &&
            GameModeManager.Instance.CurrentGameMode == E_GameMode.InteractionMenu)
        {
            GameModeManager.Instance.RequestChangeMode(E_GameMode.Explore);
        }
        _ownsModeSwitch = false;
    }

    #endregion

    #region 模式独占

    /// <summary>
    /// 对话是模态的：播放期间若其他系统（如 ESC 触发的面板/菜单逻辑）把模式切回漫游，
    /// 立即夺回 InteractionMenu，防止"对话未结束但人物恢复移动、可再次交互"的错乱状态。
    /// </summary>
    public void OnEvent(GameModeChangedEvent evt)
    {
        if (!_ownsModeSwitch || _runner is not { IsRunning: true })
            return;
        if (GameModeManager.Instance is null)
            return;

        if (evt.newMode == E_GameMode.Explore)
            GameModeManager.Instance.RequestChangeMode(E_GameMode.InteractionMenu);
    }

    #endregion

    private void Update()
    {
        if (_runner is null || !_runner.IsRunning)
            return;

        // 键盘/手柄 Submit（鼠标点击由面板的 OnPointerClick 处理）
        if (InputSystemController.Instance is not null &&
            InputSystemController.Instance.GetUISubmitPressed())
        {
            _panel.HandleAdvanceInput();
        }
    }
}
