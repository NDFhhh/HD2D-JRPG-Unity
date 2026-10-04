
using MFramework.Event;

public class PlayerInteractor : MonoBehaviour,
    IEventReceiver<ChooseCharacterButtonOnClickEvent>
{
    private CharacterIdentity _characterIdentity;
    private InteractionBase _target;

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
        _characterIdentity =  evt.CharacterIdentity;
    }

    private void Update()
    {
        if (_target is null || _target.cachedCommandInfos.Count == 0) return;
        
        var input = InputSystemController.Instance;
        
        if (input is null) return;

        if (input.GetPlayerConfirmPressed()&&_target is not null)
        {
            _target.Interact(_characterIdentity.CharacterDefinition as AllyDefinition_SO);
        }

    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out InteractionBase interactionBase))
        {
            _target = interactionBase;
            interactionBase.OnFocus(_characterIdentity.CharacterDefinition as AllyDefinition_SO);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent(out InteractionBase interactionBase))
        {
            interactionBase.OnLostFocus(_characterIdentity.CharacterDefinition as AllyDefinition_SO);
        }
        _target = null;
    }
}
