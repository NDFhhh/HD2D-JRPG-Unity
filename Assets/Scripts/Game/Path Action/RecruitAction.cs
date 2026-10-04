
public class RecruitAction : ActionBase
{
    public CharacterDefinition_SO CurrentCharacter { get; private set; }

    private void Awake()
    {
        CurrentCharacter = GetComponent<CharacterIdentity>().CharacterDefinition;
    }

    private void Start()
    {
        if (PartyManager.Instance.HasMember(CurrentCharacter))
        {
            HideSceneNPC();
        }
    }

    public override void TriggerAction(AllyDefinition_SO interactor)
    {
        EventBus.Publish(new PanelRequestEvent(this));
    }

    public override void Execute()
    {
        PartyManager.Instance.RecruitMember(CurrentCharacter);
        HideSceneNPC();
    }

    private void HideSceneNPC()
    {
        this.gameObject.SetActive(false);  
    }
}
