using UnityEngine;

/// <summary>
/// 战斗摄像机锚点：用它的位姿决定战斗机位，替代原来写死在 Persistent 场景里的固定坐标。
///
/// 归属约定（重要）：
///   锚点属于「战斗场地」，不属于 Persistent —— 建在 Battle Layout / Battle Field Layout 下面，
///   场地整体挪动或旋转时，机位跟着走；反过来把机位写死在常驻场景里，场地一动就错位。
///
/// 偏移约定：
///   偏移量在「锚点局部空间」下解释，和 BattleCinematicService 的镜头演出保持一致
///   （那边是 _cameraBaseRot * settings.CameraPositionOffset）。锚点自己已经把构图摆好时，
///   两个偏移都留 0 即可；只有在需要临时微调、又不想动锚点位置时才用它。
///
/// 本组件只提供数据，不持有任何运行时状态，实际写位姿的是 BattleCameraRig。
/// </summary>
public class BattleCameraAnchor : MonoBehaviour
{
    [Header("机位微调（锚点局部空间，通常留 0）")]
    [SerializeField, Tooltip("相对锚点的位置偏移。锚点已经摆好构图时留 (0,0,0)")]
    private Vector3 positionOffset;

    [SerializeField, Tooltip("相对锚点的欧拉角偏移。锚点已经摆好构图时留 (0,0,0)")]
    private Vector3 eulerOffset;

    public Vector3 PositionOffset => positionOffset;
    public Vector3 EulerOffset => eulerOffset;

    /// <summary>最终相机基准位置 = 锚点位置 + 锚点旋转 * 位置偏移</summary>
    public Vector3 ResolvePosition()
    {
        return transform.position + transform.rotation * positionOffset;
    }

    /// <summary>最终相机基准朝向 = 锚点旋转 * 旋转偏移</summary>
    public Quaternion ResolveRotation()
    {
        return transform.rotation * Quaternion.Euler(eulerOffset);
    }

    /// <summary>
    /// 把解算出的位姿写到相机节点上。
    /// 注意这里是「复制位姿」而不是 SetParent：目标节点是 Persistent 场景的常驻对象，
    /// 一旦成为动态加载场景物体的子节点，场景卸载时会被一起带走。
    /// </summary>
    public void ApplyTo(Transform cameraRoot)
    {
        if (cameraRoot == null) return;
        cameraRoot.SetPositionAndRotation(ResolvePosition(), ResolveRotation());
    }

    private void OnDrawGizmos()
    {
        Transform t = transform;
        Vector3 pos = ResolvePosition();
        Quaternion rot = ResolveRotation();

        // 锚点本体：一个小方块，方便在 Scene 视图里点到
        Gizmos.color = new Color(0.18f, 0.65f, 0.85f, 1f);
        Gizmos.DrawWireCube(t.position, Vector3.one * 0.6f);

        // 解算后的机位：Unity 相机朝向是局部 +Z，这里按 Z 负方向画机身、正方向画镜头朝向
        Gizmos.color = new Color(0.85f, 0.45f, 0.15f, 1f);
        Matrix4x4 prev = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(pos, rot, Vector3.one);
        Gizmos.DrawWireCube(new Vector3(0f, 0f, -0.8f), new Vector3(1.6f, 0.9f, 0.8f));
        Gizmos.DrawLine(new Vector3(-0.8f, -0.45f, 0f), new Vector3(0.8f, 0.45f, 0f));
        Gizmos.DrawLine(new Vector3(-0.8f, 0.45f, 0f), new Vector3(0.8f, -0.45f, 0f));
        Gizmos.matrix = prev;

        // 有偏移时，把锚点本体和解算机位连起来
        if (positionOffset != Vector3.zero)
        {
            Gizmos.color = new Color(0.85f, 0.45f, 0.15f, 0.6f);
            Gizmos.DrawLine(t.position, pos);
        }
    }
}
