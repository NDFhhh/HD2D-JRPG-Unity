using MFramework.Event;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 战斗摄像机位姿车间：把 Battle Camera Root 的位姿交给「战斗场地里的锚点」决定，
/// 取代原来写死在 Persistent 场景里的固定坐标。
///
/// 挂在 Battle Camera Root 上，切到 Battle 视口时应用一次。
///
/// 三条硬约定，破了就会出奇怪的 bug：
///
///   1. 【只应用一次，且只在入场时】
///      BattleCinematicService 会在演出开始时把镜头当前位置缓存成「回位目标」。
///      如果战斗中反复重设基准位，演出结束就会回到错误的位置，镜头越飘越远。
///      所以这里只监听 GameModeChangedEvent 切到 Battle 的那一下，绝不放 Update。
///
///   2. 【复制位姿，不 SetParent】
///      本组件所在节点是 Persistent 场景的常驻对象。一旦把它挂成动态加载场景
///      （Demo_SanXingDui / Map）里某个物体的子节点，那个场景卸载时它会被一起销毁。
///
///   3. 【必须有兜底】
///      场景里没摆锚点时要回落到 fallback 位姿（默认就是改造前的固定机位）。
///      否则新场地忘了配锚点，进战斗就是相机朝天，而且不会报错，非常难查。
/// </summary>
public class BattleCameraRig : MonoBehaviour, IEventReceiver<GameModeChangedEvent>
{
    [Header("驱动节点")]
    [SerializeField, Tooltip("实际被写入位姿的节点。默认本节点（Battle Camera Root），演出补间写的也是它")]
    private Transform cameraRoot;

    [Header("兜底位姿（场景里没有锚点时使用）")]
    [SerializeField, Tooltip("默认值 = 改造前 Persistent 里写死的固定机位")]
    private Vector3 fallbackPosition = new Vector3(0f, 3.5f, -17f);

    [SerializeField, Tooltip("默认值 = 改造前 Battle Camera 的 8° 俯角")]
    private Vector3 fallbackEuler = new Vector3(8f, 0f, 0f);

    [Header("引用")]
    [SerializeField, Tooltip("可留空。应用新位姿后清掉它缓存的镜头基准，避免下场演出回位到旧机位")]
    private BattleCinematicService cinematicService;

    [Header("Debug")]
    [SerializeField] private bool logResolve;

    /// <summary>本次应用用的是锚点还是兜底，供排查机位不对时定位问题</summary>
    public bool LastApplyUsedAnchor { get; private set; }

    private void Reset()
    {
        cameraRoot = transform;
    }

    private void Awake()
    {
        if (cameraRoot == null)
            cameraRoot = transform;
    }

    private void OnEnable()
    {
        EventBus.Subscribe<GameModeChangedEvent>(this);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<GameModeChangedEvent>(this);
    }

    public void OnEvent(GameModeChangedEvent evt)
    {
        // 只在进战斗这一下应用；退出战斗不还原 —— 探索相机会被 CameraModeController 接管
        if (evt.newMode != E_GameMode.Battle)
            return;

        ApplyBattleCameraPose();
    }

    /// <summary>按当前场景解算并写入战斗相机位姿（也可由外部主动调用，例如战斗场景就绪后）</summary>
    public void ApplyBattleCameraPose()
    {
        if (cameraRoot == null)
            cameraRoot = transform;

        BattleCameraAnchor anchor = FindAnchor();
        LastApplyUsedAnchor = anchor != null;

        if (anchor != null)
        {
            anchor.ApplyTo(cameraRoot);
        }
        else
        {
            // 兜底：没有锚点就沿用改造前的固定机位，保证画面和以前一致
            cameraRoot.SetPositionAndRotation(fallbackPosition, Quaternion.Euler(fallbackEuler));
        }

        // 清掉演出服务缓存的镜头基准：那份基准可能还是上一场战斗的位置，
        // 不清的话下一次强制中断演出会把镜头拉回旧机位
        if (cinematicService != null)
            cinematicService.ResetCameraBaseCache();

        if (logResolve)
        {
            string src = LastApplyUsedAnchor ? $"锚点 {anchor.name}" : "兜底位姿";
            Debug.Log($"[BattleCameraRig] 战斗相机位姿已应用：{src} → pos {cameraRoot.position} / euler {cameraRoot.rotation.eulerAngles}");
        }
    }

    #region 锚点查找

    /// <summary>
    /// 三级查找，优先级从「相机与站位同源」到「兜底」：
    ///   ① 战斗场地（BattleFieldLayout）下面的锚点 —— 正规路径，相机跟着场地走；
    ///   ② 当前已加载的玩法场景里的锚点 —— 场地还没配锚点时的过渡路径；
    ///   ③ null —— 交给兜底位姿。
    /// 全程用运行时查找而不是序列化引用：玩法场景是 Addressables 动态加载的，
    /// Persistent 上的序列化字段指不到它（同 SceneLoadManager.FindSpawnPoint 的做法）。
    /// </summary>
    private BattleCameraAnchor FindAnchor()
    {
        // ① 优先跟战斗场地同源，避免相机和站位标记各自漂移
        BattleFieldLayout layout = FindAnyObjectByType<BattleFieldLayout>();
        if (layout != null)
        {
            BattleCameraAnchor inField = layout.GetComponentInChildren<BattleCameraAnchor>(true);
            if (inField != null)
                return inField;
        }

        // ② 再退一步：在自己以外的已加载场景里找根节点下的锚点
        Scene ownScene = gameObject.scene;
        int sceneCount = SceneManager.sceneCount;
        for (int i = 0; i < sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded || scene == ownScene)
                continue;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                BattleCameraAnchor found = root.GetComponentInChildren<BattleCameraAnchor>(true);
                if (found != null)
                    return found;
            }
        }

        return null;
    }

    #endregion
}
