using UnityEditor.Build.Content;
using UnityEngine.AddressableAssets;

public class ScenePartal : MonoBehaviour
{
    [Header("Target Scene")] 
    [SerializeField] private AssetReference targetScene;

    [SerializeField] private string targetSpawnPointId;

    [Header("Trigger Mode")] 
    [SerializeField] private bool requireConfirmKey = true;
    [SerializeField, Min(0)] private float triggerCooldown = 0.4f;

    [Header("Transition")] 
    [SerializeField] private E_FadeStyle fadeStyle = E_FadeStyle.PanelFade;
    
    // 玩家是否处于当前传送门触发区内
    private bool _playerInside;
    // 下一次允许触发传送的时间戳 (防抖)
    private float _nextAllowedTriggerTime;

    private void Update()
    {
        if (!_playerInside || !requireConfirmKey)
            return;
        
        InputSystemController input = InputSystemController.Instance;
        if (input.GetPlayerConfirmPressed())
            RequestTeleport();
    }

    private void OnTriggerEnter(Collider other)
    {
        _playerInside = true;
        if (!requireConfirmKey)
            RequestTeleport();
    }

    private void OnTriggerExit(Collider other)
    {
        _playerInside = false;
    }

    private void RequestTeleport()
    {
        // 1. 还没到冷却时间，就不重复发请求
        if (Time.time < _nextAllowedTriggerTime)
            return;
        
        SceneLoadManager sceneLoadManager = SceneLoadManager.Instance;
        
        // 2. 场景正在切换时，直接等当前流程收口
        if (sceneLoadManager.IsLoading)
            return;
        
        // 3. 把目标场景、出生点和淡入淡出样式一起打包成加载请求
        sceneLoadManager.RequestSceneLoad(new SceneLoadRequest(
            targetScene,
            fadeStyle, 
            E_GameMode.Explore,
            targetSpawnPointId));
        
        // 4. 请求发出后，先关掉本次触发，再推迟下一次可触发时间
        _playerInside = false;
        _nextAllowedTriggerTime = Time.time + triggerCooldown;

    }

}
