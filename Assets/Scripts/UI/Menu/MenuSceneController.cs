using UnityEngine.UI;

public class MenuSceneController : MonoBehaviour
{
    [Header("Start Menu")]
    [SerializeField] private GameObject gameStartMenuPanel;
    [SerializeField] private float panelFadeInDuration = 0.5f;

    [Header("Buttons")]
    [SerializeField] private Button newGameButton;

    [Header("Menu -> Map Scene Fade")]
    [SerializeField] private E_FadeStyle startGameFadeStyle = E_FadeStyle.PanelFade;
    [SerializeField, Range(-1f, 10f)] private float startGameFadeOutOverride;
    [SerializeField, Range(-1f, 10f)] private float startGameFadeInOverride;

    [Header("Entrance Scene")] 
    [SerializeField] private string entranceSceneId;
    
    private CanvasGroup _startMenuCanvasGroup;
    private Coroutine _panelFadeRoutine;
    private bool _startMenuShown;
    private bool _startRequested;

    private void Awake()
    {
        _startMenuCanvasGroup = gameStartMenuPanel.GetComponent<CanvasGroup>();
        HideStartMenu();
    }

    private IEnumerator Start()
    {
        yield return null;
        
        ShowStartMenu();
    }
    
    private void OnEnable()
    {
        newGameButton.onClick.AddListener(OnNewGameButtonClicked);
        _startRequested = false;
    }

    private void OnDisable()
    {
        newGameButton.onClick.RemoveListener(OnNewGameButtonClicked);
        StopPanelFadeRoutine();
    }

    #region 主流程
    
    private void ShowStartMenu()
    {
        if (_startMenuShown) return;
        _startMenuShown = true;
        gameStartMenuPanel.SetActive(true);
        newGameButton.Select();
    
        StopPanelFadeRoutine();
        _panelFadeRoutine = StartCoroutine(FadeStartMenu());
    }

    private IEnumerator FadeStartMenu()
    {
        // 1. 先把开始菜单放到透明状态，防止加载瞬间直接跳出来。
        _startMenuCanvasGroup.alpha = 0f;
        _startMenuCanvasGroup.interactable = false;

        // 2. 再用很短的过渡把它淡出来。
        float elapsed = 0f;
        while (elapsed < panelFadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            _startMenuCanvasGroup.alpha = Mathf.Clamp01(elapsed / panelFadeInDuration);
            yield return null;
        }
    
        // 3. 淡入结束后，允许玩家正常交互。
        _startMenuCanvasGroup.alpha = 1f;
        _startMenuCanvasGroup.interactable = true;
        _panelFadeRoutine = null;
    }
    
    #endregion

    #region 按钮事件

    private void OnNewGameButtonClicked()
    {
        if (_startRequested)
            return;

        SceneLoadManager sceneLoadManager = SceneLoadManager.Instance;
        sceneLoadManager.RequestSceneLoad(new SceneLoadRequest(
            sceneLoadManager.MapScene,
            startGameFadeStyle,
            E_GameMode.InteractionMenu,
            entranceSceneId,
            startGameFadeOutOverride,
            startGameFadeInOverride
        ));
        
    }
    
    #endregion
    
    #region Helper
    
    private void HideStartMenu()
    {
        gameStartMenuPanel.SetActive(false);
        _startMenuCanvasGroup.alpha = 0f;
        _startMenuCanvasGroup.interactable = false;
    }

    private void StopPanelFadeRoutine()
    {
        if (_panelFadeRoutine == null)
            return;

        StopCoroutine(_panelFadeRoutine);
        _panelFadeRoutine = null;
    }
    
    #endregion
}
