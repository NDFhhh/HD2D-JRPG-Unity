using MFramework.Event;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Yarn.Unity;

/// <summary>
/// 对话系统总入口（自动生成的常驻单例，无需手动挂场景）。
/// 底层换成 **Yarn Spinner** 驱动，对外接口保持一行代码播放一段对话。
///
/// 用法：<see cref="Play"/> 传入 Yarn 脚本里的**节点名**即可。
/// 职责：
/// - 装配并持有 Yarn <c>DialogueRunner</c>，接上 <see cref="GameFlagVariableStorage"/>
///   （变量桥接到既有 GameFlagManager）与 <see cref="DialoguePanelController"/>（表现层）；
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
    /// 不设置时自动查找场景中已摆放的面板；都没有则报错。
    /// 在任意启动脚本的 Awake/Start 里赋值即可，例如：
    /// DialogueManager.PanelPrefab = myPanelPrefab;
    /// </summary>
    public static DialoguePanelController PanelPrefab;

    /// <summary>
    /// Yarn 对话项目资产（.yarnproject）。在任意启动脚本里赋值即可，例如：
    /// DialogueManager.Project = myYarnProject;
    /// 不设置时会尝试 Resources.Load 兜底。
    /// </summary>
    public static YarnProject Project;

    /// <summary>Resources 兜底路径：把 .yarnproject 放进 Assets/Resources/ 即可零配置加载</summary>
    public const string ProjectResourcePath = "Dialogue";

    /// <summary>当前是否有对话正在播放</summary>
    public static bool IsPlaying
        => Instance is not null && Instance._runner is not null && Instance._runner.IsDialogueRunning;

    private Yarn.Unity.DialogueRunner _runner;
    private DialoguePanelController _panel;
    private GameFlagVariableStorage _storage;
    private string _currentNode;
    private bool _dialogueCompleteHooked;

    /// <summary>
    /// 本次对话的收尾是否已经跑过。收尾有两条通道（Yarn 的 onDialogueComplete 事件、
    /// 以及 StartDialogueAsync 里 await 的 DialogueTask），必须只处理一次，
    /// 否则会把「切回 Explore」执行两遍甚至和模式独占守卫互相抢。
    /// </summary>
    private bool _completionHandled;

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

        BuildUI();
        BuildRunner();

        EventBus.Subscribe<GameModeChangedEvent>(this);
    }

    private void OnDestroy()
    {
        EventBus.Unsubscribe<GameModeChangedEvent>(this);
    }

    #region 装配

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
            Debug.LogError("[DialogueManager] 对话面板初始化失败：未找到可用面板");
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

    /// <summary>装配 Yarn 的 DialogueRunner、变量存储与表现器</summary>
    private void BuildRunner()
    {
        _runner = gameObject.GetComponent<Yarn.Unity.DialogueRunner>();
        if (_runner is null)
            _runner = gameObject.AddComponent<Yarn.Unity.DialogueRunner>();

        // 变量存储桥接：Yarn 的 $var ↔ GameFlagManager
        _storage = gameObject.GetComponent<GameFlagVariableStorage>();
        if (_storage is null)
            _storage = gameObject.AddComponent<GameFlagVariableStorage>();
        _runner.VariableStorage = _storage;

        // 表现器：我们的 UI 面板
        if (_panel is not null)
            _runner.DialoguePresenters = new DialoguePresenterBase[] { _panel };

        if (!_dialogueCompleteHooked)
        {
            // ⚠️ onDialogueComplete 是 UnityEvent 字段且**没有默认实例**（DialogueRunner.Awake
            // 也不创建它）。DialogueRunner 在本工程是运行时 AddComponent 出来的，这个字段可能
            // 直接是 null —— 一旦 null 就永远挂不上监听，「对话结束」收尾不会执行。
            // 这里既补建实例，又在 StartDialogueAsync 里另走 DialogueTask 兜底，双通道 + 幂等。
            _runner.onDialogueComplete ??= new UnityEvent();
            _runner.onDialogueComplete.AddListener(HandleDialogueComplete);
            _dialogueCompleteHooked = true;
        }

        // Yarn 命令桥接：脚本里写 <<evt quest_started>> 即广播 DialogueCustomEvent。
        // 与旧 DialogueNode.OnEnterEventKey 是同一个挂接点，任务/剧情系统零改动。
        _runner.AddCommandHandler("evt", (System.Action<string>)HandleDialogueEventCommand);
    }

    /// <summary>Yarn 的 <c>&lt;&lt;evt key&gt;&gt;</c> 命令 → EventBus 广播</summary>
    private static void HandleDialogueEventCommand(string key)
    {
        if (string.IsNullOrEmpty(key))
            return;
        EventBus.Publish(new DialogueCustomEvent(key));
    }

    /// <summary>确保 YarnProject 已就位（可由启动脚本晚于 Awake 赋值，故每次播放前兜底）</summary>
    private bool EnsureProject()
    {
        if (_runner.YarnProject is not null)
            return true;

        YarnProject project = Project;
        if (project is null)
        {
            project = Resources.Load<YarnProject>(ProjectResourcePath);
            Project = project;
        }

        if (project is null)
        {
            Debug.LogError("[DialogueManager] 未找到 YarnProject。请在本机 Unity 里创建 .yarnproject，" +
                           "然后二选一：① 在启动脚本里赋值 DialogueManager.Project；" +
                           $"② 放到 Assets/Resources/{ProjectResourcePath}.yarnproject");
            return false;
        }

        _runner.SetProject(project);
        return true;
    }

    #endregion

    #region 播放控制

    /// <summary>播放一段对话。参数是 Yarn 脚本里的节点名（如 "ElderGreeting"）。</summary>
    public static void Play(string nodeName)
    {
        if (string.IsNullOrEmpty(nodeName))
        {
            Debug.LogWarning("[DialogueManager] 播放失败：节点名为空");
            return;
        }

        DialogueManager manager = EnsureInstance();
        if (IsPlaying)
        {
            Debug.LogWarning($"[DialogueManager] 已有对话进行中，忽略本次播放：{nodeName}");
            return;
        }

        manager.BeginDialogue(nodeName);
    }

    private void BeginDialogue(string nodeName)
    {
        if (!EnsureProject())
            return;

        _currentNode = nodeName;
        _completionHandled = false;

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

        EventBus.Publish(new DialogueStartedEvent(nodeName));
        StartDialogueAsync(nodeName);
    }

    /// <summary>
    /// Yarn 的 StartDialogue 是异步的（YarnTask），这里 fire-and-forget。
    /// 用 async void + try/catch 保证异常不会静默吞掉，失败时也走一遍收尾。
    /// </summary>
    /// <remarks>
    /// <para><b>关键：</b><c>StartDialogue</c> 在「对话刚开始」时就会返回（内部只是
    /// SetNode + 一发 Continue），并不会等玩家看完。所以必须再
    /// <c>await DialogueTask</c> —— 它在 Yarn 收尾阶段
    /// （<c>dialogueCompletionSource.TrySetResult()</c>，此时所有 Presenter 的
    /// <c>OnDialogueCompleteAsync</c> 都已执行完）才完成。</para>
    /// <para>为什么不能只靠 <c>onDialogueComplete</c> 事件：见 BuildRunner 里的注释，
    /// 那个 UnityEvent 在运行时 AddComponent 的场景下可能是 null。</para>
    /// </remarks>
    private async void StartDialogueAsync(string nodeName)
    {
        try
        {
            await _runner.StartDialogue(nodeName);
            await _runner.DialogueTask;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[DialogueManager] 播放对话 '{nodeName}' 失败：{e}");
        }
        finally
        {
            // 幂等：onDialogueComplete 那条通道若已经收过尾，这里会直接返回
            HandleDialogueComplete();
        }
    }

    /// <summary>把玩家的推进输入翻译成 Yarn 语义：打字中 → 加速补全全文；否则 → 下一句</summary>
    public static void RequestAdvance(bool isTyping)
    {
        DialogueManager manager = Instance;
        if (manager is null || manager._runner is null)
            return;
        if (!manager._runner.IsDialogueRunning)
            return;

        if (isTyping)
            manager._runner.RequestHurryUpLine();
        else
            manager._runner.RequestNextLine();
    }

    private void HandleDialogueComplete()
    {
        if (_completionHandled)
            return;
        _completionHandled = true;

        EventBus.Publish(new DialogueEndedEvent(_currentNode));
        _currentNode = null;

        // ⚠️ 顺序不能反：先把「本次对话独占模式」的标志清掉，再请求切回 Explore。
        //   RequestChangeMode 是**同步**的（内部 EventBus.Publish 同步派发），
        //   OnEvent(GameModeChangedEvent) 会在本次调用栈里立刻执行到。若此时
        //   _ownsModeSwitch 仍为 true、而 Yarn 还没把 IsDialogueRunning 置 false，
        //   守卫就会把刚切好的 Explore 又抢回 InteractionMenu
        //   → 输入图停在 UI、玩家角色永久不能动。
        bool ownedModeSwitch = _ownsModeSwitch;
        _ownsModeSwitch = false;

        if (ownedModeSwitch &&
            GameModeManager.Instance is not null &&
            GameModeManager.Instance.CurrentGameMode == E_GameMode.InteractionMenu)
        {
            GameModeManager.Instance.RequestChangeMode(E_GameMode.Explore);
        }
    }

    #endregion

    #region 模式独占

    /// <summary>
    /// 对话是模态的：播放期间若其他系统（如 ESC 触发的面板/菜单逻辑）把模式切回漫游，
    /// 立即夺回 InteractionMenu，防止"对话未结束但人物恢复移动、可再次交互"的错乱状态。
    /// </summary>
    public void OnEvent(GameModeChangedEvent evt)
    {
        if (!_ownsModeSwitch || _runner is null || !_runner.IsDialogueRunning)
            return;
        if (GameModeManager.Instance is null)
            return;

        if (evt.newMode == E_GameMode.Explore)
            GameModeManager.Instance.RequestChangeMode(E_GameMode.InteractionMenu);
    }

    #endregion

    private void Update()
    {
        if (_runner is null || !_runner.IsDialogueRunning || _panel is null)
            return;

        // 键盘/手柄 Submit（鼠标点击由面板的 OnPointerClick 处理）
        if (InputSystemController.Instance is not null &&
            InputSystemController.Instance.GetUISubmitPressed())
        {
            _panel.HandleAdvanceInput();
        }
    }
}
