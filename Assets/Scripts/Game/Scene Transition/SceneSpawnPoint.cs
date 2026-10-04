public class SceneSpawnPoint : MonoBehaviour
{
    [Header("Spawn Config")] 
    [SerializeField] private string spawnId;
    [SerializeField] private bool isDefaultFallback;
    
    public string SpawnId => spawnId;
    public bool IsDefaultFallback => isDefaultFallback;
}
