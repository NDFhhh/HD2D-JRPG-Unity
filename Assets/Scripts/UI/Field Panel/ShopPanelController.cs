using TMPro;
using UnityEngine.UI;

public class ShopPanelController : PanelController
{
    [Header("一级按钮与金额")]
    [SerializeField] private Button buyButton;
    [SerializeField] private Button sellButton;
    [SerializeField] private TMP_Text currencyAmountText;

    [Header("二级列表")]
    [SerializeField] private ItemPanelController itemPanel;

    [Header("交互区域")]
    [SerializeField] private CanvasGroup leftPart;
    [SerializeField] private CanvasGroup itemPanelCanvasGroup;

    [Header("Confirm Popup")]
    [SerializeField] private RectTransform confirmPopup;
    [SerializeField] private TMP_Text popupTitle;
    [SerializeField] private TMP_Text popupText;
    [SerializeField] private Button popupConfirmButton;
    [SerializeField] private Button popupCancelButton;

    public override Type PanelActionType => typeof(ShopAction);

    private ItemDefinition_SO _pendingItem;
    
    private E_PanelType _currentShopType;
    
    private ShopAction CurrentShopAction => (ShopAction)CurrentAction;

    private void Awake()
    {
        RebindButtons(buyButton, OpenBuyPanel);
        RebindButtons(sellButton, OpenSellPanel);
        RebindButtons(popupConfirmButton,ExcuteTransaction);
        RebindButtons(popupCancelButton, ClosePopup);
        
        confirmPopup.gameObject.SetActive(false);
    }

    public override void SetupPanel(ActionBase actionBase)
    {
        base.SetupPanel(actionBase);
        SetDefaultSelection();
        UpdateCurrencyDisplay();
    }

    private void OpenBuyPanel()
    {
        OpenItemPanel(E_PanelType.Buy);
    }

    private void OpenSellPanel()
    {
        OpenItemPanel(E_PanelType.Sell);
    }

    private void OpenItemPanel(E_PanelType panelType)
    {
        _currentShopType = panelType;
        leftPart.interactable = false;
        itemPanel.gameObject.SetActive(true);

        itemPanel.SetupPanel(panelType, CurrentAction, OpenConfirmPopup);
    }

    private void OpenConfirmPopup(ItemDefinition_SO itemDefinition)
    {
        _pendingItem = itemDefinition;
        confirmPopup.gameObject.SetActive(true);
        itemPanelCanvasGroup.interactable = false;
        
        if(_currentShopType == E_PanelType.Buy)
            SetupBuyPopup(itemDefinition);
        else
            SetupSellPopup(itemDefinition);
    }
    
    private void SetupBuyPopup(ItemDefinition_SO itemDefinition)
    {
        bool canAfford = InventoryManager.Instance.Currency >= itemDefinition.BuyPrice;
        
        popupTitle.text = "确定购买以下物品吗？";
        popupText.text = $"{itemDefinition.ItemName}\n价格：{itemDefinition.BuyPrice}￥";
        
        popupConfirmButton.interactable = canAfford;
        (canAfford ? popupConfirmButton : popupCancelButton).Select();
    }

    private void SetupSellPopup(ItemDefinition_SO itemDefinition)
    {
        popupTitle.text = "确定出售以下物品吗？";
        popupText.text = $"{itemDefinition.ItemName}\n获得：{itemDefinition.SellPrice}￥";
        
        popupConfirmButton.interactable = true;
        popupConfirmButton.Select();
    }

    public override bool HandleCancelInput()
    {
        if (confirmPopup.gameObject.activeSelf)
        {
            confirmPopup.gameObject.SetActive(false);
            itemPanelCanvasGroup.interactable = true;
            if(itemPanel.gameObject.activeSelf)
                itemPanel.SetDefaultSelection();
            return true;
        }

        if (!itemPanel.gameObject.activeSelf)
            return false;
        
        itemPanel.gameObject.SetActive(false);
        leftPart.interactable = true;
        _pendingItem = null;
        FirstButton = _currentShopType == E_PanelType.Buy ? buyButton : sellButton;
        SetDefaultSelection();
        return true;
    }

    private void UpdateCurrencyDisplay()
    {
        if (currencyAmountText is null)
            return;
        var inventory = InventoryManager.Instance;
        int currentMoney = inventory.Currency;
        currencyAmountText.text = currentMoney.ToString();
    }
    
    private void ExcuteTransaction()
    {
        CurrentShopAction.TryExcuteTransaction(_currentShopType, _pendingItem);
        confirmPopup.gameObject.SetActive(false);
        itemPanelCanvasGroup.interactable = true;
        
        UpdateCurrencyDisplay();

        if (_currentShopType == E_PanelType.Buy)
        {
            itemPanel.RefreshItemQuantity(_pendingItem);
            itemPanel.SetDefaultSelection();
            return;
        }
        
        // 卖
        var remaining =
            EquipmentService.GetAvailableItemCount(InventoryManager.Instance, PartyManager.Instance, _pendingItem);
        if (remaining > 0)
        {
            itemPanel.RefreshItemQuantity(_pendingItem);
            itemPanel.SetDefaultSelection();
            return;
        }
        itemPanel.RemoveItemButton(_pendingItem);
    }
    
    private void ClosePopup()
    {
        confirmPopup.gameObject.SetActive(false);
        itemPanelCanvasGroup.interactable = true;
        itemPanel.SetDefaultSelection();
    }
}
