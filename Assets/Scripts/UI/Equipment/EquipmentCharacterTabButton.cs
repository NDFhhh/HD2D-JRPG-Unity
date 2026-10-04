
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class EquipmentCharacterTabButton : MonoBehaviour, ISelectHandler
{
    [SerializeField] private Image portraitImage;
    [SerializeField] private CanvasGroup canvasGroup;
    
    private Button _button;
    private int _index;
    private Action<int> _onSelect;
    private Action<int> _onClicked;
    
    public Button Button => _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
        _button.onClick.AddListener(HandleClick);
        SetSelectedVisual(false);
    }

    public void Setup(CharacterRuntimeData member, int index, Action<int> onSelect, Action<int> onClicked)
    {
        _index = index;
        _onSelect = onSelect;
        _onClicked = onClicked;
        if (portraitImage is not null)
            portraitImage.sprite = member.Definition.Portrait;
    }

    public void SetSelectedVisual(bool selected)
    {
        canvasGroup.alpha = selected ? 1 : 0.5f;
    }

    public void SetInteractable(bool interactable)
    {
        _button.interactable = interactable;
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
