/// <summary>
/// 路径动作：对话。挂在 NPC 的交互物体上（与 InquiryAction 等并列），
/// 在交互菜单中选择"对话"即播放配置的对话资产。
/// </summary>
public class TalkAction : ActionBase
{
    [Header("对话配置")]
    [SerializeField] private DialogueAsset_SO dialogue;

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
        return dialogue is not null && !DialogueManager.IsPlaying;
    }

    public override void TriggerAction(AllyDefinition_SO interactor)
    {
        if (dialogue is null)
        {
            Debug.LogWarning($"[TalkAction] {name} 未配置对话资产");
            return;
        }

        DialogueManager.Play(dialogue);
    }
}
