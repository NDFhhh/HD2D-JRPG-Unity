
public class CharacterIdentity : MonoBehaviour
{
    [SerializeField] private CharacterDefinition_SO _characterDefinition;
    
    public CharacterDefinition_SO CharacterDefinition => _characterDefinition;
    
    public void SetDefinition(CharacterDefinition_SO characterDefinition)
    {
        _characterDefinition = characterDefinition;
    }
}
