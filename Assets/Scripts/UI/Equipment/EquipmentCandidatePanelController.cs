
using TMPro;
using UnityEngine.UI;

public class EquipmentCandidatePanelController : MonoBehaviour
{
    [Header("Header")]
    [SerializeField] private TMP_Text slotNameText;
    [SerializeField] private Image slotIconImage;

    [Header("List")] 
    [SerializeField] private RectTransform candidateListRoot;
    [SerializeField] private EquipmentCandidateButton candidateButtonPrefab;
    
    private readonly List<EquipmentCandidateButton> _buttons = new();
    private readonly List<EquipmentItem_SO> _candidates = new();
    
    public event Action<int> OnSelectedIndexChanged;
    public event Action<int> OnCandidateClicked;

    public EquipmentItem_SO GetCandidate(int index) => _candidates[index];

    public Button GetFirstButton() => _buttons[0].Button;
    
    public void OpenForSlot(E_EquipSlot slot, CharacterRuntimeData member, string slotDisplayName, Sprite slotIcon)
    {
        RefreshHeader(slotDisplayName, slotIcon);
        RebuildCandidates(slot);
    }

    private void RebuildCandidates(E_EquipSlot slot)
    {
        _candidates.Clear();
        ClearButtons();

        InventoryManager inventory = InventoryManager.Instance;
        PartyManager partyManager = PartyManager.Instance;

        _candidates.AddRange(EquipmentService.BuildCandidates(inventory, slot));

        for (int i = 0; i < _candidates.Count; i++)
        {
            EquipmentCandidateButton candidateButton = Instantiate(candidateButtonPrefab, candidateListRoot);

            int availableQuantity = EquipmentService.GetAvailableItemCount(inventory, partyManager, _candidates[i]);
            bool isInteractable = _candidates[i] == null || availableQuantity > 0;    // ==null代表未装备   >0代表物品可以使用

            candidateButton.Setup(i, _candidates[i], availableQuantity, isInteractable, inventory.IconSet);
            _buttons.Add(candidateButton);
            
            candidateButton.OnClick += HandleCandidateClicked;
            candidateButton.OnSelected += HandleCandidateSelected;
        }
    }

    private void RefreshHeader(string slotDisplayName,Sprite slotIcon)
    {
        slotNameText.text = slotDisplayName;
        slotIconImage.sprite = slotIcon;
        
        slotIconImage.enabled = slotIcon is not null;
    }

    public void Close()
    {
        _candidates.Clear();
        ClearButtons();
    }

    private void ClearButtons()
    {
        for (int i = 0; i < _buttons.Count; i++)
        {
            if (_buttons[i] is not null)
                Destroy(_buttons[i].gameObject);
        }
        _buttons.Clear();
    }

    private void HandleCandidateClicked(int index)
    {
        OnCandidateClicked?.Invoke(index);
    }

    private void HandleCandidateSelected(int index)
    {
        OnSelectedIndexChanged?.Invoke(index);
    }
    
}
