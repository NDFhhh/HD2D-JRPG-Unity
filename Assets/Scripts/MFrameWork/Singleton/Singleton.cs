
public class Singleton<T> : MonoBehaviour where T : Singleton<T>
{
    private static T _instance;
    public static T Instance => _instance;

    protected virtual void Awake()
    {
        if (_instance is null)
        {
            _instance = this as T;
        }

        // 运行时挂上单例管理器脚本
        // 先把原有的实例删除
        if (_instance != this)
        {
            Destroy(gameObject);
        }
    }
}
