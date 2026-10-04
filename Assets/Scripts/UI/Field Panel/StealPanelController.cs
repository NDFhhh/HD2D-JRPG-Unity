
using TMPro;
using UnityEngine.UI;

public class StealPanelController : PanelController
{
    [Header("Steal Panel")]
    [SerializeField] private StealItemButton stealItemButtonPrefab;
    [SerializeField] private RectTransform contentRoot;
    
    [Header("Confirm Popup")]
    [SerializeField] private RectTransform confirmPopup;
    [SerializeField] private TMP_Text popupText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    private readonly List<StealItemButton> _stealItemButtons = new();
    public override Type PanelActionType => typeof(StealAction);
    private StealAction CurrentStealAction => (StealAction)CurrentAction;
    
    
    private ItemDefinition_SO _pendingItem;

    public override void SetupPanel(ActionBase actionBase)
    {
        base.SetupPanel(actionBase);
        
        confirmPopup.gameObject.SetActive(false);
        
        RefreshItemList();
    }

    private void RefreshItemList()
    {
        ClearItemList();

        if (CurrentStealAction.itemsToSteal.Count == 0)
        {
            // 通知 InteractionBase 重新检测可见性，隐藏 ActionIcon
            var interactionBase = CurrentStealAction.GetComponent<InteractionBase>();
            if (interactionBase != null)
                EventBus.Publish(new InteractionRefreshEvent(interactionBase));

            ClosePanel();
            GameModeManager.Instance.RequestChangeMode(E_GameMode.Explore);
            return;
        }
        
        foreach (InventoryItem item in CurrentStealAction.itemsToSteal)
        {
            StealItemButton stealItemButton = Instantiate(stealItemButtonPrefab, contentRoot);
            stealItemButton.SetupButton(item, OpenConfirmPopup);
            _stealItemButtons.Add(stealItemButton);
        }

        if (_stealItemButtons.Count == 0) return;
        
        FirstButton = _stealItemButtons[0].CurrentButton;
        SetDefaultSelection();
    }

    private void OpenConfirmPopup(ItemDefinition_SO itemDefinition)
    {
        _pendingItem = itemDefinition;
        FirstButton = confirmButton;
        confirmPopup.gameObject.SetActive(true);
        cancelButton.gameObject.SetActive(true);

        popupText.text = $"{itemDefinition.ItemName} 成功率：{itemDefinition.RarityWeight}%";
        
        SetButtonInteractable(false);
        
        RebindButtons(confirmButton, OnConfirm);
        RebindButtons(cancelButton, ClosePopup);
        SetDefaultSelection();
    }

    protected override void OnConfirm()
    {
        bool success = CurrentStealAction.TrySteal(_pendingItem);
        popupText.text = success ? "偷取成功" : "偷取失败";
        cancelButton.gameObject.SetActive(false);
        RebindButtons(confirmButton, ClosePopup);
    }

    private void ClosePopup()
    {
        HidePopup();
        RefreshItemList();
    }

    private void HidePopup()
    {
        _pendingItem = null;
        confirmPopup.gameObject.SetActive(false);
        SetButtonInteractable(true);
    }

    public override bool HandleCancelInput()
    {
        if (confirmPopup.gameObject.activeSelf)
        {
            ClosePopup(); 
            return true;
        }

        return false;
    }

    private void SetButtonInteractable(bool interactor)
    {
        foreach (StealItemButton stealItemButton in _stealItemButtons)
            stealItemButton.CurrentButton.interactable = interactor;
    }

    private void ClearItemList()
    {
        foreach (StealItemButton stealItemButton in _stealItemButtons)
            Destroy(stealItemButton.gameObject);
        
        _stealItemButtons.Clear();
        FirstButton = null;
    }

}
