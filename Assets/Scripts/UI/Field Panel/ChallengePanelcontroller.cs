using TMPro;
using UnityEngine.UI;

public class ChallengePanelcontroller : PanelController
{
    [Header("Challenge Panel")]
    [SerializeField] private TMP_Text npcNameText;
    [SerializeField] private TMP_Text difficultyText;
    [SerializeField] private Image characterImage;
    
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    public override Type PanelActionType => typeof(ChallengeAction);

    public override void SetupPanel(ActionBase actionBase)
    {
        base.SetupPanel(actionBase);
        ChallengeAction challengeAction = (ChallengeAction)actionBase;
        var currentCharacter = challengeAction.Currentcharacter;
        
        npcNameText.text = currentCharacter.Name;
        difficultyText.text = $"难度：{challengeAction.LastDifficulty}";
        characterImage.sprite = currentCharacter.Portrait;

        RebindButtons(confirmButton, OnConfirm);
        RebindButtons(cancelButton, OnCancel);
        FirstButton = confirmButton;
        SetDefaultSelection();
    }
}
