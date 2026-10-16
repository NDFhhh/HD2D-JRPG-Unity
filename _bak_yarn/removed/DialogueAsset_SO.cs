/// <summary>
/// 一段完整对话的节点图资产。
/// 通过 CreateAssetMenu 创建：右键 → Create → CodePath → Dialogue → Dialogue Asset
///
/// 结构说明：
/// - Entries：入口规则列表，按顺序匹配，第一条条件通过的入口生效（用于"初次见面/再次见面"等分支开场）；
/// - Nodes：对话节点，线性推进用 NextNodeId，分支用 Choices；
/// - 节点上的 Conditions 不满足时，该节点被静默跳过，直接走它的 NextNodeId（条件链）；
/// - 节点 / 选项上的 FlagOperation 用于写全局变量（GameFlagManager），
///   OnEnterEventKey 通过 EventBus 广播自定义事件（任务/剧情系统挂接点）。
/// </summary>
[CreateAssetMenu(fileName = "Dialogue_", menuName = "CodePath/Dialogue/Dialogue Asset")]
public class DialogueAsset_SO : ScriptableObject
{
    [Header("入口配置")]
    [Tooltip("优先按顺序匹配 Entries，全部不匹配时使用该默认入口")]
    public string StartNodeId = "start";

    [Tooltip("条件入口列表，按顺序匹配，第一条条件全部通过的入口生效")]
    public List<DialogueEntry> Entries = new();

    [Header("对话节点")]
    public List<DialogueNode> Nodes = new();

    private Dictionary<string, DialogueNode> _lookup;

    /// <summary>解析本次对话应从哪个节点开始</summary>
    public string ResolveStartNodeId()
    {
        for (int i = 0; i < Entries.Count; i++)
        {
            DialogueEntry entry = Entries[i];
            if (!string.IsNullOrEmpty(entry.StartNodeId) && entry.ConditionsPass())
                return entry.StartNodeId;
        }
        return StartNodeId;
    }

    public DialogueNode FindNode(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId))
            return null;

        EnsureLookup();
        return _lookup.TryGetValue(nodeId, out DialogueNode node) ? node : null;
    }

    private void EnsureLookup()
    {
        // 编辑器里资产可能被随时修改，这里每次都重建，开销极小（节点数量级为十）
        _lookup = new Dictionary<string, DialogueNode>(Nodes.Count);
        for (int i = 0; i < Nodes.Count; i++)
        {
            DialogueNode node = Nodes[i];
            if (node is null || string.IsNullOrEmpty(node.NodeId))
                continue;
            _lookup[node.NodeId] = node;
        }
    }
}

/// <summary>条件入口：条件全部通过时，从 StartNodeId 开始播放</summary>
[Serializable]
public class DialogueEntry
{
    public string StartNodeId;
    public List<FlagCondition> Conditions = new();

    public bool ConditionsPass()
    {
        for (int i = 0; i < Conditions.Count; i++)
        {
            if (!Conditions[i].Evaluate())
                return false;
        }
        return true;
    }
}

/// <summary>对话节点：一段文本 + 推进/分支 + 条件 + 副作用</summary>
[Serializable]
public class DialogueNode
{
    [Tooltip("节点唯一 ID，同一份资产内不可重复")]
    public string NodeId;

    [Tooltip("说话人，可为空（旁白，无姓名牌）")]
    public Speaker_SO Speaker;

    [TextArea(2, 5)]
    [Tooltip("对话正文，支持 TMP 富文本（如 <color=#E8C97A>、<b>）")]
    public string Text;

    [Tooltip("无选项时，点击推进到的下一个节点；留空则对话结束")]
    public string NextNodeId;

    [Tooltip("分支选项列表，非空时玩家必须选择后才能继续")]
    public List<DialogueChoice> Choices = new();

    [Header("条件与副作用")]
    [Tooltip("全部通过才进入该节点；不满足时静默跳过，直接走 NextNodeId")]
    public List<FlagCondition> Conditions = new();

    [Tooltip("进入该节点时执行的全局变量写入")]
    public List<FlagOperation> OnEnterOperations = new();

    [Tooltip("进入该节点时通过 EventBus 广播的自定义事件 Key（任务/剧情系统挂接点）")]
    public string OnEnterEventKey;

    public bool ConditionsPass()
    {
        for (int i = 0; i < Conditions.Count; i++)
        {
            if (!Conditions[i].Evaluate())
                return false;
        }
        return true;
    }
}

/// <summary>分支选项</summary>
[Serializable]
public class DialogueChoice
{
    [Tooltip("选项显示文本")]
    public string Text;

    [Tooltip("选择后跳转的节点；留空则对话结束")]
    public string NextNodeId;

    [Tooltip("条件不满足时该选项不显示")]
    public List<FlagCondition> Conditions = new();

    [Tooltip("选择该选项时执行的全局变量写入")]
    public List<FlagOperation> OnSelectOperations = new();

    public bool IsVisible()
    {
        for (int i = 0; i < Conditions.Count; i++)
        {
            if (!Conditions[i].Evaluate())
                return false;
        }
        return true;
    }
}

public enum E_CompareOp
{
    Equal,
    NotEqual,
    Greater,
    Less,
    GreaterOrEqual,
    LessOrEqual
}

public enum E_FlagOp
{
    Set,
    Add,
    Toggle
}

/// <summary>全局变量条件：读取 GameFlagManager 并比较</summary>
[Serializable]
public class FlagCondition
{
    public string Key;
    public E_CompareOp Op = E_CompareOp.Equal;
    public int Value;

    public bool Evaluate()
    {
        int current = GameFlagManager.Instance is null ? 0 : GameFlagManager.Instance.Get(Key);
        return Op switch
        {
            E_CompareOp.Equal => current == Value,
            E_CompareOp.NotEqual => current != Value,
            E_CompareOp.Greater => current > Value,
            E_CompareOp.Less => current < Value,
            E_CompareOp.GreaterOrEqual => current >= Value,
            E_CompareOp.LessOrEqual => current <= Value,
            _ => false
        };
    }
}

/// <summary>全局变量写入操作</summary>
[Serializable]
public class FlagOperation
{
    public string Key;
    public E_FlagOp Op = E_FlagOp.Set;
    public int Value;

    public void Apply()
    {
        GameFlagManager manager = GameFlagManager.EnsureInstance();
        switch (Op)
        {
            case E_FlagOp.Set:
                manager.Set(Key, Value);
                break;
            case E_FlagOp.Add:
                manager.Add(Key, Value);
                break;
            case E_FlagOp.Toggle:
                manager.Toggle(Key);
                break;
        }
    }
}
