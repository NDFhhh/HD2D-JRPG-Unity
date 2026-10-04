using TMPro;

public class EquipmentPanelController : PanelController
{
    private enum E_FocusLayer
    {
        CharacterTabs,
        SlotList,
        CandidateList
    }

    [Header("Character Tabs")] 
    [SerializeField] private RectTransform tabRoot;
    [SerializeField] private CanvasGroup tabCanvasGroup;
    [SerializeField] private EquipmentCharacterTabButton tabPrefab;

    [Header("Slot Buttons")]
    [SerializeField] private EquipmentSlotButton[] slotButtons;
    [SerializeField] private CanvasGroup slotListCanvasGroup;
    
    [Header("Left Category Name")]
    [SerializeField] private TMP_Text[] leftCategoryNameText;
    [SerializeField] private Color leftCategoryEnableColor = Color.black;
    [SerializeField] private Color leftCategoryDisableColor = new Color(0.72f, 0.72f, 0.72f);

    [Header("Header")] 
    [SerializeField] private TMP_Text memberNameText;

    [Header("Stat Preview")]
    [SerializeField] private EquipmentStatPreviewPanel statPreviewPanel;
    
    [Header("Candidate Panel")]
    [SerializeField] private GameObject candidatePanelRoot;
    [SerializeField] private EquipmentCandidatePanelController candidatePanelController;
    
    #region 运行时缓存

    private List<EquipmentCharacterTabButton> _tabButtons = new();
    private List<CharacterRuntimeData> _partyMember = new();

    private int _memberIndex;
    private int _slotIndex;

    private E_FocusLayer _focusLayer = E_FocusLayer.CharacterTabs;
    
    private CharacterRuntimeData CurrentMember =>
        _partyMember.Count >= 0 && _memberIndex < _partyMember.Count ? _partyMember[_memberIndex] : null;
    
    #endregion

    private void Awake()
    {
        candidatePanelController.OnSelectedIndexChanged += OnCandidateSelected;
        candidatePanelController.OnCandidateClicked += OnCandidateClicked;
    }

    private void OnDestroy()
    {
        candidatePanelController.OnSelectedIndexChanged -= OnCandidateSelected;
        candidatePanelController.OnCandidateClicked -= OnCandidateClicked;
    }

    public void SetupWithPartyMember(List<CharacterRuntimeData> partyMembers)
    {
        _partyMember.Clear();
        if (partyMembers is null) return;

        for (int i = 0; i < partyMembers.Count; i++)
        {
            var member = partyMembers[i];
            _partyMember.Add(member);
        }

        BuildCharactertabs();
        ClampMemberIndex();
        RefreshCurrentMemberView();
        EnterCharacterTabLayer();
    }
    
    private void RefreshCurrentMemberView()
    {
        memberNameText.text = CurrentMember?.Name;

        for (int i = 0; i < FixedSlotOrder.Length; i++)
        {
            EquipmentSlotButton slotButton = slotButtons[i];
            E_EquipSlot slot = FixedSlotOrder[i];
            EquipmentItem_SO equippedItem = CurrentMember?.GetEquippedItem(slot);

            slotButton.gameObject.SetActive(true);
            slotButton.Setup(equippedItem, i, OnSlotSelected, OnSlotClicked, IsSlotUsableForMember(CurrentMember, i));
        }

        statPreviewPanel.Refresh(CurrentMember.GetTotalStats(), CurrentMember.GetTotalStats(), false);
        UpdateTabSelectionVisual();
        RefreshLeftCategoryColors(CurrentMember);
    }

    private void RefreshLeftCategoryColors(CharacterRuntimeData member)
    {
        for (int i = 0; i < leftCategoryNameText.Length; i++)
        {
            leftCategoryNameText[i].color = IsSlotUsableForMember(member,i)
                ? leftCategoryEnableColor
                : leftCategoryDisableColor;
        }
    }

    private bool IsSlotUsableForMember(CharacterRuntimeData member, int slotIndex)
    {
        if (slotIndex >= WeaponSlotTypes.Length)
            return true;    // 非武器槽位，默认可用

        return member.Definition is AllyDefinition_SO allyDef
               && allyDef.CanEquipWeaponType(WeaponSlotTypes[slotIndex]);
    }

    public override bool HandleCancelInput()
    {
        if (candidatePanelRoot.activeInHierarchy)
        {
            EnterSlotLayer();
            return true;
        }

        if (_focusLayer == E_FocusLayer.SlotList)
        {
            EnterCharacterTabLayer();
            return true;
        }

        return false;
    }

    #region Tab切换人物
    
    private void BuildCharactertabs()
    {
        ClearTabButtons();
        
        for (int i = 0; i < _partyMember.Count; i++)
        {
            var tab = Instantiate(tabPrefab, tabRoot);
            tab.Setup(_partyMember[i],i,OnTabSelected,OnTabClick);
            tab.SetSelectedVisual(i == _memberIndex);
            _tabButtons.Add(tab);
        }
    }

    private void ClearTabButtons()
    {
        for (int i = 0; i < _tabButtons.Count; i++)
        {
            Destroy(_tabButtons[i].gameObject);
        }
        _tabButtons.Clear();
    }

    private void ClampMemberIndex()
    {
        _memberIndex = _tabButtons.Count > 0
            ? Mathf.Clamp(_memberIndex, 0, _partyMember.Count - 1)
            : 0;
    }
    
    private void EnterCharacterTabLayer()
    {
        _focusLayer = E_FocusLayer.CharacterTabs;
        SetCharacterTabInteractable(true);
        UpdateTabSelectionVisual();
        SetSlotListInteractable(false);
        
        _memberIndex = Mathf.Clamp(_memberIndex, 0, _partyMember.Count - 1);
        FirstButton = _tabButtons[_memberIndex].Button;
        SetDefaultSelection();
    }

    private void SetCharacterTabInteractable(bool interactable)
    {
        tabCanvasGroup.interactable = interactable;
        for (int i = 0; i < _tabButtons.Count; i++)
            _tabButtons[i].Button.interactable = interactable;
    }

    private void UpdateTabSelectionVisual()
    {
        for (int i = 0; i < _tabButtons.Count; i++)
        {
            _tabButtons[i].SetSelectedVisual(i == _memberIndex);
        }
    }

    private void OnTabClick(int index)
    {
        //TODO 切换人物装备
        OnTabSelected(index);
        EnterSlotLayer();
    }

    private void OnTabSelected(int index)
    {
        _memberIndex = index;
        RefreshCurrentMemberView();
    }
    
    #endregion

    #region 装备Slot Layer

    private void EnterSlotLayer()
    {
        _focusLayer = E_FocusLayer.SlotList;
        CloseCandidateListInternal();
        SetCharacterTabInteractable(false);    // 禁用角色标签
        EnsureSelectedSlotValid();
        SetSlotListInteractable(true);

        if (_slotIndex >= slotButtons.Length) return;
        
        FirstButton = slotButtons[_slotIndex].Button;
        SetDefaultSelection();
    }

    private void EnsureSelectedSlotValid()
    {
        if (IsSlotIndexUsable(_slotIndex))
            return;

        for (int i = 0; i < FixedSlotOrder.Length; i++)
        {
            if (IsSlotIndexUsable(i))
            {
                _slotIndex = i;
                return;
            }
        }
        
        _slotIndex = -1;
    }

    private bool IsSlotIndexUsable(int index) =>
        index >= 0 && index < FixedSlotOrder.Length && IsSlotUsableForMember(CurrentMember, index);

    private void SetSlotListInteractable(bool interactable)
    {
        slotListCanvasGroup.interactable = interactable;
        for (int i = 0; i < slotButtons.Length; i++)
        {
            slotButtons[i].SetInputEnabled(interactable);
        }
    }

    private void OnSlotSelected(int index)
    {
        if (CurrentMember == null || statPreviewPanel == null)
            return;
        
        statPreviewPanel.Refresh(CurrentMember.GetTotalStats(),CurrentMember.GetTotalStats(),false);
    }

    private void OnSlotClicked(int index)
    {
        OnSlotSelected(index);
        OpenCandidateList(index);
    }

    #endregion

    #region 可选择装备面板

    private void OpenCandidateList(int slotIndex)
    {
        SetSlotListInteractable(false);
        SetCharacterTabInteractable(false);
        
        candidatePanelRoot.SetActive(true);
        E_EquipSlot slot = FixedSlotOrder[slotIndex];
        string slotName = leftCategoryNameText[slotIndex].text;
        candidatePanelController.OpenForSlot(slot, CurrentMember, slotName, slotButtons[slotIndex].SlotIconSprite);
        
        _focusLayer = E_FocusLayer.CandidateList;
        _slotIndex = slotIndex;
        
        FirstButton = candidatePanelController.GetFirstButton();
        SetDefaultSelection();
        UpdateCandidatePreview(0);
    }

    private void CloseCandidateListInternal()
    {
        candidatePanelController.Close();
        candidatePanelRoot.SetActive(false);
    }

    private void OnCandidateSelected(int index)
    {
        // 更新能力值变化
        UpdateCandidatePreview(index);
    }

    private void OnCandidateClicked(int index)
    {
        var slot = FixedSlotOrder[_slotIndex];
        EquipmentItem_SO targetItem = candidatePanelController.GetCandidate(index);
        if (CurrentMember.GetEquippedItem(slot) == targetItem)
            return;
        
        CurrentMember.SetEquippedItem(slot, targetItem);
        RefreshCurrentMemberView();
        OpenCandidateList(_slotIndex);
    }

    private void UpdateCandidatePreview(int candidateIndex)
    {
        if (!IsSlotIndexUsable(_slotIndex))
            return;
        
        E_EquipSlot slot = FixedSlotOrder[_slotIndex];
        EquipmentItem_SO previewItem = candidatePanelController.GetCandidate(candidateIndex);

        RefreshPreview(slot, previewItem, true);
    }

    private void RefreshPreview(E_EquipSlot slot, EquipmentItem_SO previewItem, bool isPreviewMode)
    {
        var currentTotal = CurrentMember.GetTotalStats();
        var previewTotal = EquipmentService.BuildPreviewTotalStats(CurrentMember, slot, previewItem);
        
        statPreviewPanel.Refresh(currentTotal, previewTotal, isPreviewMode);
    }

    #endregion
    
    #region 固定槽位顺序

    private readonly E_WeaponType[] WeaponSlotTypes =
    {
        E_WeaponType.Sword,
        E_WeaponType.Spear,
        E_WeaponType.Dagger,
        E_WeaponType.Axe,
        E_WeaponType.Bow,
        E_WeaponType.Staff
    };

    private readonly E_EquipSlot[] FixedSlotOrder =
    {
        E_EquipSlot.Sword,
        E_EquipSlot.Spear,
        E_EquipSlot.Dagger,
        E_EquipSlot.Axe,
        E_EquipSlot.Bow,
        E_EquipSlot.Staff,
        E_EquipSlot.Shield,
        E_EquipSlot.Head,
        E_EquipSlot.Body,
        E_EquipSlot.Accessory1,
        E_EquipSlot.Accessory2
    };

    #endregion

}
