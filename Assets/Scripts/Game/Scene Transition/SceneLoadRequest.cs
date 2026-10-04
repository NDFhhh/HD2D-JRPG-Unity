using UnityEngine.AddressableAssets;

public struct SceneLoadRequest
{
    public readonly AssetReference Scene;
    public readonly E_FadeStyle FadeStyle;
    public readonly E_GameMode ModeAfterLoad;

    public readonly string SpawnPointId;

    /// <summary>
    /// 显式落点 (世界坐标)，替代按 SpawnPointId 查找出生点。
    /// 为 null 时走 SpawnPointId / 默认出生点逻辑。
    /// 用于"回到上一个场景的原位置"，例如战斗结束后返回遇敌点。
    /// </summary>
    public readonly Vector3? SpawnPointPosition;
    public readonly Quaternion? SpawnPointRotation;

    public readonly float FadeOutDurationOverride;
    public readonly float FadeInDurationOverride;

    public SceneLoadRequest(AssetReference scene,E_FadeStyle fadeStyle, E_GameMode modeAfterLoad, string spawnPointId = null,
        float fadeOutDurationOverride = -1f, float fadeInDurationOverride = -1f)
    {
        Scene =  scene;
        FadeStyle = fadeStyle;
        ModeAfterLoad = modeAfterLoad;

        SpawnPointId = spawnPointId;
        SpawnPointPosition = null;
        SpawnPointRotation = null;

        FadeOutDurationOverride = fadeOutDurationOverride;
        FadeInDurationOverride = fadeInDurationOverride;
    }

    public SceneLoadRequest(AssetReference scene,E_FadeStyle fadeStyle,E_GameMode modeAfterLoad, Vector3 spawnPointPosition,
        Quaternion spawnPointRotation, float fadeOutDurationOverride = -1f, float fadeInDurationOverride = -1f)
    {
        Scene =  scene;
        FadeStyle = fadeStyle;
        ModeAfterLoad = modeAfterLoad;

        SpawnPointId = null;
        SpawnPointPosition = spawnPointPosition;
        SpawnPointRotation = spawnPointRotation;

        FadeOutDurationOverride = fadeOutDurationOverride;
        FadeInDurationOverride = fadeInDurationOverride;
    }

}
