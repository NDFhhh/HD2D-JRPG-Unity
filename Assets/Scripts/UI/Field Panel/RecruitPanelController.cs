using TMPro;
using UnityEngine.UI;

public class RecruitPanelController : PanelController
{
    [Header("Recruit Panel")]
    [SerializeField] private TMP_Text npcNameText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private Image characterImage;
    
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    public override Type PanelActionType => typeof(RecruitAction);

    public override void SetupPanel(ActionBase actionBase)
    {
        base.SetupPanel(actionBase);
        
        RecruitAction recruitAction = (RecruitAction) actionBase;

        npcNameText.text = recruitAction.CurrentCharacter.Name;
        characterImage.sprite = recruitAction.CurrentCharacter.Portrait;
        levelText.text = $"等级：{recruitAction.CurrentCharacter.BaseLevel.ToString()}";

        BindButtons();
        SetDefaultSelection();
    }

    private void BindButtons()
    {
        RebindButtons(confirmButton, OnConfirm);
        RebindButtons(cancelButton, OnCancel);
    }
}
