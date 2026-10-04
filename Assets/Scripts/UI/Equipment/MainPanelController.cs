
using TMPro;
using UnityEngine.UI;

public class MainPanelController : PanelController
{
    [Header("Main Panel")] 
    [SerializeField] private Button itemButton;
    [SerializeField] private Button equipmentButton;
    [SerializeField] private TMP_Text currencyAmountText;
    
    [Header("Panels")]
    [SerializeField] private ItemPanelController itemPanelController;
    [SerializeField] private EquipmentPanelController equipmentPanelController;
    [SerializeField] private CanvasGroup leftPartCanvasGroup;

    private GameObject _currentPanel;
    private Button _lastButton;
    
    private void Awake()
    {
        RebindButtons(itemButton, OpenItemPanel);
        RebindButtons(equipmentButton, OpenEquipmentPanel);
    }

    private void OnEnable()
    {
        FirstButton = itemButton;
        SetDefaultSelection();
        UpdateCurrencyDisplay();
    }

    private void OnDisable()
    {
        if (_currentPanel != null)
            _currentPanel.SetActive(false);
        leftPartCanvasGroup.interactable = true;
        _currentPanel = null;
    }

    private void OpenItemPanel()
    {
        OpenPanel(itemPanelController.gameObject, itemButton);
        itemPanelController.SetupPanel(E_PanelType.Item);
    }

    private void OpenEquipmentPanel()
    {
        OpenPanel(equipmentPanelController.gameObject, equipmentButton);
        equipmentPanelController.SetupWithPartyMember(PartyManager.Instance is not null
            ? PartyManager.Instance.PartyMembers
            : null);
    }

    private void OpenPanel(GameObject panel, Button sourceButton)
    {
        _lastButton = sourceButton;
        panel.SetActive(true);
        _currentPanel = panel;
        leftPartCanvasGroup.interactable = false;
    }
    
    private void UpdateCurrencyDisplay()
    {
        if (currencyAmountText is null)
            return;
        var inventory = InventoryManager.Instance;
        int currentMoney = inventory.Currency;
        currencyAmountText.text = currentMoney.ToString();
    }

    public override bool HandleCancelInput()
    {
        if (_currentPanel is null || !_currentPanel.activeSelf)
            return false;

        if (equipmentPanelController is not null
            && _currentPanel == equipmentPanelController.gameObject
            && equipmentPanelController.HandleCancelInput())
            return true;

        CloseCurrentPanel();
        return true;
    }

    private void CloseCurrentPanel()
    {
        FirstButton = _lastButton;
        _currentPanel.SetActive(false);
        _currentPanel = null;
        
        leftPartCanvasGroup.interactable = true;
        SetDefaultSelection();
    }
}
