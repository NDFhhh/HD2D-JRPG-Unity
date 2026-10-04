using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class EquipmentSlotButton : MonoBehaviour, ISelectHandler
{
    [SerializeField] private TMP_Text slotNameText;
    [SerializeField] private Image slotIconImage;
    private CanvasGroup _canvasGroup;
    private Button _button;
    
    private int _index;
    private Action<int> _onSelect;
    private Action<int> _onClicked;

    public Button Button
    {
        get
        {
            if (_button is null)
            {
                _button = GetComponent<Button>();
                _defaultNavigation = _button.navigation;
            }
            return _button;
        }
    }

    private bool _isSlotUsable = true;
    public bool _isInputEnabled = false;
    
    public Sprite SlotIconSprite => slotIconImage.sprite;
    
    private Navigation _defaultNavigation;

    private void Awake()
    {
        Button.onClick.AddListener(HandleClick);
        _canvasGroup = GetComponent<CanvasGroup>();
    }

    public void Setup(
        EquipmentItem_SO equipment, 
        int index,
        Action<int> onSelect, Action<int> onClicked,
        bool isSlotUsable)
    {
        _index = index;
        _onSelect = onSelect;
        _onClicked = onClicked;
        _isSlotUsable = isSlotUsable;

        string displayName = equipment is not null ? equipment.ItemName : "未装备";
        slotNameText.text = displayName;

        ApplyButtonInteractableState();
    }

    public void SetInputEnabled(bool isEnabled)
    {
        _isInputEnabled = isEnabled;
        ApplyButtonInteractableState();
    }

    private void ApplyButtonInteractableState()
    {
        bool isInteractable = _isSlotUsable && _isInputEnabled;
        Button.interactable = isInteractable;
        _canvasGroup.alpha = _isSlotUsable ? 1f : 0f;
        _canvasGroup.interactable = isInteractable;
        
        Navigation navigation = _defaultNavigation;

        if (!isInteractable)
            navigation.mode = Navigation.Mode.None;
        
        Button.navigation = navigation;
    }

    private void HandleClick()
    {
        if (!_button.interactable)
            return;
        _onClicked?.Invoke(_index);
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (!_button.interactable)
            return;
        _onSelect?.Invoke(_index);
    }
    
}
