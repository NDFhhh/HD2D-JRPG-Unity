
public class PartyFieldController : MonoBehaviour
{
    [Header("References")] 
    [SerializeField] private Transform followersParent;
    [SerializeField] private GameObject fieldFollowerPrefab;
    
    [SerializeField] private Transform playerTrans;
    public Transform PlayerTrans => playerTrans;
    
    [Header("Settings")]
    [SerializeField] private float followDistance = 1.2f;    //相邻两人间距
    [SerializeField] private float followSpeed = 5f;

    [SerializeField] private float zOffset = 0.01f;
    [SerializeField] private float sampleMinDistance = 0.05f;    //最小采样值

    private List<Vector3> trail = new();    //记录移动轨迹
    private List<FieldFollower> fieldFollowers = new();    //跟随者

    private void LateUpdate()
    {
        UpdateLeaderTrail();

        for (int i = 0; i < fieldFollowers.Count; i++)
        {
            var follower = fieldFollowers[i];
            float targetDistance = followDistance * (i + 1);
            Vector3 targetPos = GetPointAtDistance(targetDistance);
            follower.MoveTo(ApplyFollowerOffset(targetPos, i), followSpeed);
        }
    }

    /// <summary>
    /// 获取在路径上距离领导者指定距离的点
    /// </summary>
    /// <param name="distanceFromLeader">距离领导者的距离</param>
    /// <returns>路径上对应点的位置</returns>
    private Vector3 GetPointAtDistance(float distanceFromLeader)
    {
        // 如果路径为空，返回玩家当前位置
        if (trail.Count == 0) return playerTrans.position;

        float accumulated = 0f;    //累计距离

        // 遍历路径中的所有点
        for (int i = 0; i < trail.Count - 1; i++)
        {
            Vector3 a = trail[i];
            Vector3 b = trail[i + 1];
            
            float dist = Vector3.Distance(a, b);

            // 如果累计距离加上当前线段距离大于等于目标距离
            if (accumulated + dist >= distanceFromLeader)
            {
                // 计算在线段上的插值比例
                float t = (distanceFromLeader - accumulated) / dist;
                // 返回两点之间的插值点
                return Vector3.Lerp(a, b, t);
            }
            
            accumulated += dist;    //更新累计距离
        }
        
        // 如果所有线段长度之和仍小于目标距离，返回路径最后一个点
        return trail[^1];
    }

    public void UpdateFollowers(List<CharacterDefinition_SO> partyMembers)
    {
        int followerCount = partyMembers.Count - 1;    //减去主角
        while (fieldFollowers.Count < followerCount)
        {
            int index = fieldFollowers.Count;
            // 新同伴直接生成在自己的队形位置上，而不是生成在主角身上再等人来拉
            Vector3 pos = GetFormationSlotPosition(index);
            GameObject followerObj = Instantiate(fieldFollowerPrefab, pos, Quaternion.identity, followersParent);
            
            fieldFollowers.Add(followerObj.GetComponent<FieldFollower>());
        }

        for (int i = 0; i < followerCount; i++)
            fieldFollowers[i].SetupFollower(partyMembers[i + 1]);

        // 队伍还没走动过时 trail 里只有主角一个点，跟随者会被挤到主角身上。
        // 这里补一段队形轨迹，保证站着不动的时候每个人也都有自己的目标点。
        if (trail.Count <= 1)
            RebuildFormationTrail();
    }

    private Vector3 ApplyFollowerOffset(Vector3 position, int index)
    {
        // 只保留很小的 z 偏移压深度冲突；跟随者严格落在移动轨迹上，不做横向偏移
        position.z += zOffset * (index + 1);
        return position;
    }

    /// <summary>
    /// 更新跟随者位置
    /// </summary>
    private void UpdateLeaderTrail()
    {
        Vector3 leaderPos = playerTrans.position;

        if (trail.Count == 0)
        {
            trail.Add(leaderPos);
            return;
        }
        
        float dist = Vector3.Distance(leaderPos, trail[0]);
        if (dist > sampleMinDistance)
        {
            trail.Insert(0, leaderPos);
            
            if(trail.Count > 100)
                trail.RemoveAt(trail.Count - 1);    //最多记录100个点
        }
    }

    /// <summary>
    /// 出生 / 传送后的队形重建。
    /// 原来是把所有跟随者都 snap 到主角脚下（只差 0.01 的 z），一进场景所有人叠在一起；
    /// 现在改成：主角不动，跟随者沿主角背后依次排开成队形，
    /// 并把这段队形写进 trail，让 LateUpdate 的跟随逻辑第一帧就能取到各自的目标点，
    /// 不会再一股脑把人拽回主角身上。
    /// </summary>
    private void RebuildTrailAndSnapFollowers()
    {
        trail.Clear();

        if (playerTrans is null)
            return;

        for (int i = 0; i < fieldFollowers.Count; i++)
            fieldFollowers[i].snapTo(GetFormationSlotPosition(i));

        RebuildFormationTrail();
    }

    /// <summary>
    /// 用"队形轨迹"填满 trail：0 号点是主角，索引越大越靠后。
    /// trail 是从主角往后排的历史点，GetPointAtDistance 靠它取每个跟随者的目标点；
    /// 点不够长时，排在后面的跟随者会一起落到轨迹末端（也就是主角身上），这正是出生点重叠的原因。
    /// </summary>
    private void RebuildFormationTrail()
    {
        trail.Clear();

        if (playerTrans is null || fieldFollowers.Count == 0)
            return;

        Vector3 basePos = playerTrans.position;
        Vector3 backDir = GetLeaderBackDirection();

        // 铺满"最后一个跟随者 + 一格间距"的长度，保证每个人都取得到自己的点
        float totalLength = followDistance * (fieldFollowers.Count + 1);
        int steps = Mathf.CeilToInt(totalLength / sampleMinDistance);
        for (int i = 0; i <= steps; i++)
            trail.Add(basePos + backDir * (sampleMinDistance * i));
    }

    /// <summary>
    /// 某个跟随者在队形里的目标位置：主角背后 followDistance * (index + 1) 处
    /// </summary>
    private Vector3 GetFormationSlotPosition(int index)
    {
        // 出生/传送后的落点同样在"主角背后的轨迹延长线"上，不横向偏移
        return ApplyFollowerOffset(
            playerTrans.position + GetLeaderBackDirection() * (followDistance * (index + 1)),
            index);
    }

    /// <summary>
    /// 主角背后的水平方向（队形沿它排开）。
    /// 主角朝向可能带俯仰，先水平化再归一化，避免队形被压扁或归一化除零。
    /// </summary>
    private Vector3 GetLeaderBackDirection()
    {
        Vector3 backDir = -playerTrans.forward;
        backDir.y = 0f;

        if (backDir.sqrMagnitude < 0.0001f)
            return Vector3.back;

        return backDir.normalized;
    }

    public void SetPlayerActive(bool active)
    {
        playerTrans.gameObject.SetActive(active);
    }

    public void ClearFollower()
    {
        foreach (var follower in fieldFollowers)
        {
            if (follower is not null)
                Destroy(follower.gameObject);
        }
        fieldFollowers.Clear();
        trail.Clear();
    }

    internal void TeleportPartyTo(Vector3 position, Quaternion rotation)
    {
        CharacterController cc = playerTrans.GetComponent<CharacterController>();
        cc.enabled = false;
        playerTrans.position = position;
        playerTrans.rotation = rotation;
        cc.enabled = true;
        
        playerTrans.GetComponent<PlayerEncounterTracker>().ResetEncounterTracking(position);
        RebuildTrailAndSnapFollowers();
    }
}
