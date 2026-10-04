public class ChooseCharacterController : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private List<ChooseCharacterButton> buttons;

    // 场景还在加载 / 黑场过渡时被点中的角色，先记下来，等加载结束后再补发
    private CharacterIdentity _pendingIdentity;

    private void OnEnable()
    {
        foreach (ChooseCharacterButton button in buttons)
        {
            button.Button.onClick.AddListener(() => OnChooseCharacterButtonClicked(button));
        }
    }

    private void OnDisable()
    {
        foreach (ChooseCharacterButton button in buttons)
        {
            if (button == null) continue;
            button.Button.onClick.RemoveAllListeners();
        }
    }
    
    private void Start()
    {
        // Start 时所有 Awake 都已执行，Button 缓存一定就绪
        if (buttons != null && buttons.Count > 0 && buttons[0] != null)
        {
            buttons[0].Button.Select();
        }
    }

    /// <summary>
    /// 补发进场瞬间被吞掉的选角点击。
    /// 刚进 Map 场景就点角色时，SceneLoadManager 还在加载 + 黑场淡入，这时直接发事件
    /// 会出现"队伍数据已经换成新角色、切场景请求却被丢掉"的错位：数据停在第一个选择，
    /// 场景里表现出的却是第二个选择。所以这里先记下选择，等加载结束后再发。
    /// </summary>
    private void Update()
    {
        if (_pendingIdentity == null)
            return;

        SceneLoadManager sceneLoadManager = SceneLoadManager.Instance;
        if (sceneLoadManager == null || sceneLoadManager.IsLoading)
            return;

        CharacterIdentity identity = _pendingIdentity;
        _pendingIdentity = null;
        PublishChooseCharacter(identity);
    }

    private void OnChooseCharacterButtonClicked(ChooseCharacterButton clickedButton)
    {
        CharacterIdentity identity = clickedButton.CharacterIdentity;
        if (identity == null)
        {
            return;
        }

        SceneLoadManager sceneLoadManager = SceneLoadManager.Instance;

        // 场景正在切换时，请求一定会被 SceneLoadManager 丢掉，
        // 所以这次点击不做任何事，只登记下来等加载结束补发。
        if (sceneLoadManager != null && sceneLoadManager.IsLoading)
        {
            _pendingIdentity = identity;
            return;
        }

        PublishChooseCharacter(identity);
    }

    private void PublishChooseCharacter(CharacterIdentity identity)
    {
        EventBus.Publish(new ChooseCharacterButtonOnClickEvent(identity));
    }
}