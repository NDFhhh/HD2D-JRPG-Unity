/// <summary>
/// 路径动作：对话。挂在 NPC 的交互物体上（与 InquiryAction 等并列），
/// 在交互菜单中选择"对话"即播放配置的 Yarn 对话节点。
/// </summary>
public class TalkAction : ActionBase
{
    [Header("对话配置")]
    [Tooltip("Yarn 脚本里的节点名（title: 后面那个名字，如 ElderGreeting）")]
    [SerializeField] private string dialogueNode;

    private void Reset()
    {
        CommandInfo = new ActionCommandInfo
        {
            DisplayName = "对话",
            Icon = null,
            Order = -1 // 排在打听/购买等动作之前
        };
    }

    public override bool CanExecute(AllyDefinition_SO interactor)
    {
        return !string.IsNullOrEmpty(dialogueNode) && !DialogueManager.IsPlaying;
    }

    public override void TriggerAction(AllyDefinition_SO interactor)
    {
        if (string.IsNullOrEmpty(dialogueNode))
        {
            Debug.LogWarning($"[TalkAction] {name} 未配置对话节点名");
            return;
        }

        DialogueManager.Play(dialogueNode);
    }
}
