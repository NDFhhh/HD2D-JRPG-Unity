using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SkillButton : MonoBehaviour, ISelectHandler, IDeselectHandler
{
    [SerializeField] private Image skillIcon;
    [SerializeField] private TMP_Text skillName;
    [SerializeField] private TMP_Text spCostText;
    [SerializeField] private GameObject cursorTrans;

    public void SetupSkillButton(SkillData_SO skillData)
    {
        skillName.text = skillData.skillName;
        spCostText.text = $"SP {skillData.spCost}";

        bool showIcon = skillData.elementType != E_ElementType.None;
        skillIcon.sprite = skillData.icon;
        skillIcon.gameObject.SetActive(showIcon);
    }

    public void OnSelect(BaseEventData eventData)
    {
        cursorTrans.SetActive(true);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        cursorTrans.SetActive(false);
    }
}
