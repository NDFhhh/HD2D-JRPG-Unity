using MFramework.Event;
using UnityEngine.UI;

public class ChooseCharacterButton : MonoBehaviour,
    IEventReceiver<ChooseCharacterButtonOnClickEvent>
{
    private Button _button;
    public Button Button => _button;

    private CharacterIdentity _characterIdentity;
    public CharacterIdentity CharacterIdentity => _characterIdentity;
    
    [Header("Menu -> First Scene Fade")]
    [SerializeField] private E_FadeStyle startGameFadeStyle = E_FadeStyle.PanelFade;
    [SerializeField, Range(-1f, 3f)] private float startGameFadeOutOverride;
    [SerializeField, Range(-1f, 3f)] private float startGameFadeInOverride;

    [Header("Entrance Scene")]
    [SerializeField] private string entranceSceneId;
    
    private void Awake()
    {
        _button = GetComponent<Button>();
        _characterIdentity = GetComponent<CharacterIdentity>();
    }

    private void OnEnable()
    {
        EventBus.Subscribe<ChooseCharacterButtonOnClickEvent>(this);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<ChooseCharacterButtonOnClickEvent>(this);
    }

    public void OnEvent(ChooseCharacterButtonOnClickEvent evt)
    {
        SceneLoadManager sceneLoadManager = SceneLoadManager.Instance;
        sceneLoadManager.RequestSceneLoad(new SceneLoadRequest(
            sceneLoadManager.StartupGameplayScene,
            startGameFadeStyle,
            E_GameMode.Explore,
            entranceSceneId,
            startGameFadeOutOverride,
            startGameFadeInOverride
        ));
    }
}
