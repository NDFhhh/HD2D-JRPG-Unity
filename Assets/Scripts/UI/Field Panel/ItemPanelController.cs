
public class ItemPanelController : PanelController
{
    [Header("Item Panel")]
    [SerializeField] private ItemButton itemButtonPrefab;
    [SerializeField] private Transform itemButtonParent;
    
    private readonly List<ItemButton> _itemButtons = new();
    
    private E_PanelType _currentPanelType;
    private Action<ItemDefinition_SO> _onItemClick;

    public void SetupPanel(E_PanelType panelType, ActionBase actionBase = null, Action<ItemDefinition_SO> onItemClick = null)
    {
        if(actionBase is not null)
            base.SetupPanel(actionBase);
        
        _currentPanelType = panelType;
        _onItemClick = onItemClick;

        ClearItemListView();

        var inventoryManager = InventoryManager.Instance;
        switch (panelType)
        {
            case E_PanelType.Item:
                BuildItemList(inventoryManager);
                break;
            case E_PanelType.Sell:
                BuildSellList(inventoryManager);
                break;
            case E_PanelType.Buy:
                BuildBuyList(inventoryManager);
                break;
            case E_PanelType.Equipment:
                break;
        }

        if (_itemButtons.Count == 0)
        {
            FirstButton = null;
            return;
        } 
        FirstButton = _itemButtons[0].CurrentButton;
        SetDefaultSelection();
    }

    private void ClearItemListView()
    {
        foreach (var itemButton in _itemButtons)
        {
            Destroy(itemButton.gameObject);
        }
        _itemButtons.Clear();
        FirstButton = null;
    }

    private void BuildBuyList(InventoryManager inventory)
    {
        ShopAction shopAction = (ShopAction)CurrentAction;
        foreach (var item in shopAction.itemsBag)
        {
            int ownedQuantity = inventory.GetItemQuantity(item);
            AddItemButton(new InventoryItem(item, ownedQuantity));
        }
    }

    private void BuildSellList(InventoryManager inventory)
    {
        foreach (var item in inventory.CurrentInventory)
        {
            int availableForSell =
                EquipmentService.GetAvailableItemCount(inventory, PartyManager.Instance, item.ItemDefinition);

            if (availableForSell > 0)
            {
                AddItemButton(new InventoryItem(item.ItemDefinition, availableForSell));
                continue;
            }

            if (availableForSell == 0
                && item.ItemDefinition is not null
                && item.ItemDefinition.ItemType == E_ItemType.Equipment
                && item.Quantity == 1)
                AddItemButton(new InventoryItem(item.ItemDefinition, 0), false, true);
        }
    }

    private void BuildItemList(InventoryManager inventory)
    {
        foreach (var item in inventory.CurrentInventory)
        {
            AddItemButton(item);
        }
    }

    private void AddItemButton(InventoryItem inventoryItem, bool interactable = true, bool equippedNameFormat = false)
    {
        ItemButton itemButton = Instantiate(itemButtonPrefab, itemButtonParent);
        if (itemButton is ShopItemButton shopItemButton)
            shopItemButton.SetupButton(inventoryItem, _currentPanelType, _onItemClick);
        else
            itemButton.SetupButton(inventoryItem, _onItemClick);
        
        if(equippedNameFormat)
            itemButton.SetEquippedNameFormat();
        
        itemButton.CurrentButton.interactable = interactable;
        _itemButtons.Add(itemButton);
        
        FirstButton = itemButton.CurrentButton;
        SetDefaultSelection();
    }

    public void RefreshItemQuantity(ItemDefinition_SO itemDefinition)
    {
        foreach (var itemButton in _itemButtons)
        {
            if (itemButton.CurrentItemDefinition == itemDefinition)
            {
                FirstButton = itemButton.CurrentButton;
                int newQuantity = InventoryManager.Instance.GetItemQuantity(itemDefinition);
                itemButton.UpdateQuantity(newQuantity);
                break;
            }
        }
    }

    internal void RemoveItemButton(ItemDefinition_SO pendingItem)
    {
        int targetIndex = _itemButtons.FindIndex(itemButton => itemButton.CurrentItemDefinition == pendingItem);
        ItemButton itemButton = _itemButtons[targetIndex];
        
        _itemButtons.RemoveAt(targetIndex);
        Destroy(itemButton.gameObject);
        
        FirstButton = _itemButtons.Count > 0 ? _itemButtons[0].CurrentButton : null;

        if (_itemButtons.Count > 0)
        {
            int newIndex = Mathf.Min(_itemButtons.Count - 1, targetIndex);
            FirstButton = _itemButtons[newIndex].CurrentButton;
            SetDefaultSelection();
        }
    }

}
