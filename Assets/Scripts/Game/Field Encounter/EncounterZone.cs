using UnityEngine.AddressableAssets;
using Random = UnityEngine.Random;

[Serializable]
public struct EncounterGroup
{
    #region 遇敌组配置

    [Tooltip("敌方阵容组合")] 
    public List<CharacterDefinition_SO> Enemies;

    [Tooltip("敌方阵型")] 
    public E_EnemyLayoutFormation Formation;

    [Tooltip("出现权重 (权重越高越容易遇到)"), Min(1)] 
    public int Weight;

    #endregion
}

public class EncounterZone : MonoBehaviour
{
    [Header("Battle Scene"),Tooltip("该区域对应的战斗场景 (例如：森林战斗场景)")]
    public AssetReference battleSceneReference;

    [Header("Encounter Settings"), Tooltip("遇敌需要的最小移动距离")]
    public float minEncounterDistance = 15f;
    [Tooltip("遇敌需要的最大移动距离")]
    public float maxEncounterDistance = 30f;
    
    [Header("Enemy Pools"),Tooltip("在这个区域可能遇到的敌兵组合池")]
    public List<EncounterGroup> encounterGroups = new ();
    
    /// <summary>
    /// 根据权重随机抽取一组敌人。
    /// 当前约定：每个 EncounterZone 都至少配置一组 EncounterGroup。
    /// </summary>
    public EncounterGroup GetRandomEncounter()
    {
        // 1. 先把所有组的权重累加出来，作为随机区间总长度。
        // 这里直接在本函数里把权重加总，不用再跳一层。
        int totalWeight = 0;
        for (int i = 0; i < encounterGroups.Count; i++)
        {
            totalWeight += encounterGroups[i].Weight;
        }

        // 2. 生成一个落在总区间里的随机值。
        int randomValue = Random.Range(0, totalWeight);
        int currentWeight = 0;

        for (int i = 0; i < encounterGroups.Count; i++)
        {
            var group = encounterGroups[i];
            currentWeight += group.Weight;
            // 3. 随机值落到哪一段，就返回哪组敌人。
            if (randomValue < currentWeight)
            {
                return group;
            }
        }
        return encounterGroups[^1]; // 如果没有匹配到，返回最后一个组
    }

}
