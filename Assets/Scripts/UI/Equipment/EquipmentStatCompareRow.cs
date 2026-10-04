using TMPro;

public class EquipmentStatCompareRow : MonoBehaviour
{
    [SerializeField] private TMP_Text currentValueText;
    [SerializeField] private TMP_Text modifyValueText;

    [Header("Colors")]
    [SerializeField] private Color increaseColor;
    [SerializeField] private Color decreaseColor;

    public void SetRow(int currentValue, int previewValue, bool isInPreviewMode = true)
    {
        currentValueText.text = currentValue.ToString();

        if (!isInPreviewMode || currentValue == previewValue)
        {
            modifyValueText.text = "";
            return;
        }

        modifyValueText.text = $"> {previewValue.ToString()}";
            
        modifyValueText.color = currentValue < previewValue ? increaseColor : decreaseColor;
    }
}
