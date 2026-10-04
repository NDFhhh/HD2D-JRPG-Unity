
using Random = UnityEngine.Random;

public class InquiryAction : ActionBase
{
    [Header("打听消息配置")] 
    [SerializeField] private List<InquiryActionData> inquiryActionDatas = new();

    public int PickRandomMessageIndex() => Random.Range(0, inquiryActionDatas.Count);

    public void GetInquiryActionData(int index, out InquiryActionData inquiryActionData) => inquiryActionData = inquiryActionDatas[index];

    public override void TriggerAction(AllyDefinition_SO interaction)
    {
        EventBus.Publish(new PanelRequestEvent(this));
    }
}

[System.Serializable]
public class InquiryActionData
{
    [Header("消息显示信息")] 
    public string messageTitleText;
    public string npcNameText;
    
    [TextArea(2,6)]
    public string messageContentText;
    public Sprite npcAvatar;
}
