using TMPro;
using UnityEngine.UI;

public class InquiryPanelController : PanelController
{
    [Header("Inquiry Panel")]
    [SerializeField] private TMP_Text npcNameText;
    [SerializeField] private Image npcAvatar;
    [SerializeField] private TMP_Text messageTitleText;
    [SerializeField] private TMP_Text messageContentText;
    
    private InquiryAction _currentAction;
    private int _currentIndex = -1;

    [Header("Button")] 
    [SerializeField] private Button confirmButton;

    public override Type PanelActionType => typeof(InquiryAction);

    public override void SetupPanel(ActionBase actionBase)
    {
        base.SetupPanel(actionBase);
        
        FirstButton = confirmButton;
        
        BindButton();
        SetDefaultSelection();
        
        _currentAction = (InquiryAction)CurrentAction;
        
        ApplyMessage(_currentAction.PickRandomMessageIndex());
    }
     
    private void ApplyMessage(int messageIndex)
    {
        _currentAction.GetInquiryActionData(messageIndex, out InquiryActionData inquiryActionData);
        _currentIndex = messageIndex;

        npcNameText.text = inquiryActionData.npcNameText;
        npcAvatar.sprite = inquiryActionData.npcAvatar;
        messageTitleText.text = inquiryActionData.messageTitleText;
        messageContentText.text = inquiryActionData.messageContentText;
    }

    private void BindButton()
    {
        RebindButtons(confirmButton,OnCancel);
    }
}
