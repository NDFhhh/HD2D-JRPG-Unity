
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ItemButton : MonoBehaviour, ISelectHandler, IDeselectHandler
{
    [SerializeField] private Image itemIcon;
    [SerializeField] private TMP_Text itemName;
    [SerializeField] private TMP_Text itemDescription;
    [SerializeField] private TMP_Text itemQuantity;
    [SerializeField] private GameObject itemTips;

    protected Button _button;
    public Button CurrentButton => _button;
    
    protected InventoryItem _currentItem;
    public ItemDefinition_SO CurrentItemDefinition => _currentItem.ItemDefinition;

    private Action<ItemDefinition_SO> _onItemClick;
    
    protected void Awake()
    {
        _button = GetComponent<Button>();
        _button.onClick.AddListener(OnClick);
    }
    
    public virtual void SetupButton(InventoryItem inventoryItem) => SetupButton(inventoryItem,null);    // 默认不执行回调

    public virtual void SetupButton(InventoryItem inventoryItem, Action<ItemDefinition_SO> onItemClick)
    {
        _currentItem = inventoryItem;
        _onItemClick = onItemClick;
        
        itemIcon.sprite = InventoryManager.Instance.IconSet.GetIcon(inventoryItem.ItemDefinition.ItemIconKey);
        itemName.text = inventoryItem.ItemDefinition.ItemName;
        itemDescription.text = inventoryItem.ItemDefinition.ItemDescription;

        if (itemQuantity is not null)
            itemQuantity.text = inventoryItem.Quantity.ToString();
    }

    public void SetEquippedNameFormat()
    {
        if (CurrentItemDefinition is not null)
            itemName.text = CurrentItemDefinition.ItemName + "(装备中)";
    }

    protected virtual void OnClick()
    {
        _onItemClick?.Invoke(CurrentItemDefinition);
    }

    public void UpdateQuantity(int quantity)
    {
        if (itemQuantity is not null)
            itemQuantity.text = quantity.ToString();
    }

    #region UI状态回调

    public void OnSelect(BaseEventData eventData)
    {
        itemTips.SetActive(true);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        itemTips.SetActive(false);
    }

    #endregion
    
}
