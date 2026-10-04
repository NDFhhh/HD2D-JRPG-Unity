using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class EquipmentCandidateButton : MonoBehaviour, ISelectHandler
{
    [SerializeField] private Image itemIcon;
    [SerializeField] private TMP_Text itemNameText;
    [SerializeField] private TMP_Text ownedCountText;

    [Header("Color")]
    [SerializeField] private Color normalTextColor;
    [SerializeField] private Color disabledTextColor;

    private Button _button;
    private int _index;
    
    public Action<int> OnSelected;
    public Action<int> OnClick;
    
    public Button Button => _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
        _button.onClick.AddListener(HandleClick);
    }

    public void Setup(int index, EquipmentItem_SO item, int ownedCount, bool isInteractable, ItemIconSet_SO iconSet)
    {
        _index = index;
        _button.interactable = isInteractable;
        
        itemNameText.text = item is not null ? item.ItemName : "无装备";
        ownedCountText.text = item is not null ? ownedCount.ToString() : "-";
        if (item is not null)
        {
            itemIcon.sprite = iconSet.GetIcon(item.ItemIconKey);
            itemIcon.enabled = true;
        }
        else
        {
            itemIcon.sprite = null;
            itemIcon.enabled = false;
        }
        
        itemNameText.color = isInteractable ? normalTextColor : disabledTextColor;
        ownedCountText.color = isInteractable ? normalTextColor : disabledTextColor;
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (!_button.interactable) return;
        
        OnSelected?.Invoke(_index);
    }

    public void HandleClick()
    {
        if (!_button.interactable) return;
        
        OnClick?.Invoke(_index);
    }
}
