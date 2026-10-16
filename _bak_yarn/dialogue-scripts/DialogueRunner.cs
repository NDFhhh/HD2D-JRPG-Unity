using MFramework.Event;

/// <summary>
/// 对话运行时状态机（纯逻辑，不依赖 UI，可独立单元测试）。
///
/// 状态流转：
///   Start(asset) → 逐节点 EnterNode → 玩家 Advance / SelectChoice → 直到无下一节点 → End
///
/// UI 层只需监听 NodeEntered / DialogueEnded 两个事件即可完整渲染。
/// </summary>
public class DialogueRunner
{
    /// <summary>条件链最大跳转次数，防止资产配置成环导致死循环</summary>
    private const int MaxConditionHops = 64;

    public DialogueAsset_SO Asset { get; private set; }
    public DialogueNode CurrentNode { get; private set; }
    public bool IsRunning { get; private set; }

    /// <summary>进入新节点（UI 刷新姓名牌/正文/立绘）</summary>
    public event Action<DialogueNode> NodeEntered;

    /// <summary>整段对话结束（UI 关闭面板）</summary>
    public event Action DialogueEnded;

    private readonly List<DialogueChoice> _visibleChoices = new();

    /// <summary>当前节点条件过滤后实际可点的选项</summary>
    public IReadOnlyList<DialogueChoice> VisibleChoices => _visibleChoices;

    public void Start(DialogueAsset_SO asset)
    {
        if (asset is null)
        {
            Debug.LogError("[DialogueRunner] 启动失败：对话资产为空");
            return;
        }

        Asset = asset;
        IsRunning = true;
        EnterNode(asset.ResolveStartNodeId());
    }

    /// <summary>推进到下一节点（当前节点有可点选项时无效，必须做选择）</summary>
    public void Advance()
    {
        if (!IsRunning || CurrentNode is null)
            return;
        if (_visibleChoices.Count > 0)
            return;

        GoNext(CurrentNode.NextNodeId);
    }

    /// <summary>选择某个可见选项（下标对应 <see cref="VisibleChoices"/>）</summary>
    public void SelectChoice(int index)
    {
        if (!IsRunning || index < 0 || index >= _visibleChoices.Count)
            return;

        DialogueChoice choice = _visibleChoices[index];
        for (int i = 0; i < choice.OnSelectOperations.Count; i++)
            choice.OnSelectOperations[i].Apply();

        GoNext(choice.NextNodeId);
    }

    private void GoNext(string nextNodeId)
    {
        if (string.IsNullOrEmpty(nextNodeId))
        {
            End();
            return;
        }
        EnterNode(nextNodeId);
    }

    private void EnterNode(string nodeId)
    {
        DialogueNode node = Asset.FindNode(nodeId);

        // 条件不满足的节点静默跳过，沿 NextNodeId 继续找（条件链），带成环保护
        int hops = 0;
        while (node is not null && !node.ConditionsPass() && hops < MaxConditionHops)
        {
            node = string.IsNullOrEmpty(node.NextNodeId) ? null : Asset.FindNode(node.NextNodeId);
            hops++;
        }
        if (hops >= MaxConditionHops)
            Debug.LogWarning($"[DialogueRunner] {Asset.name} 条件链跳转超过 {MaxConditionHops} 次，疑似配置成环");

        if (node is null)
        {
            End();
            return;
        }

        CurrentNode = node;

        for (int i = 0; i < node.OnEnterOperations.Count; i++)
            node.OnEnterOperations[i].Apply();

        if (!string.IsNullOrEmpty(node.OnEnterEventKey))
            EventBus.Publish(new DialogueCustomEvent(node.OnEnterEventKey));

        RebuildVisibleChoices();
        NodeEntered?.Invoke(node);
    }

    private void RebuildVisibleChoices()
    {
        _visibleChoices.Clear();
        if (CurrentNode is null)
            return;

        for (int i = 0; i < CurrentNode.Choices.Count; i++)
        {
            DialogueChoice choice = CurrentNode.Choices[i];
            if (choice is not null && choice.IsVisible())
                _visibleChoices.Add(choice);
        }
    }

    private void End()
    {
        IsRunning = false;
        CurrentNode = null;
        _visibleChoices.Clear();
        DialogueEnded?.Invoke();
    }
}
