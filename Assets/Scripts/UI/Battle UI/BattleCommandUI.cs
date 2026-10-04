using UnityEngine.UI;

public class BattleCommandUI : Singleton<BattleCommandUI>
{
    [Header("主命令面板")]
    [SerializeField] private CanvasGroup commandMenuCanvasGroup;

    [Header("二级菜单")] 
    [SerializeField] private RectTransform commandMenuPanel;
    [SerializeField] private SkillButton skillButtonPrefab;
    [SerializeField] private ItemButton itemButtonPrefab;

    [Header("Buttons")] 
    [SerializeField] private Button btnAttack;
    [SerializeField] private Button btnSkill;
    [SerializeField] private Button btnItem;
    [SerializeField] private Button btnDefend;
    [SerializeField] private Button btnEscape;
    
    [Header("Attack Type Icon")]
    [SerializeField] private DamageTypeIconSet_SO damageTypeIconSet;
    [SerializeField] private Image[] attackTypeIconImage;
    [SerializeField] private GameObject[] attackTypeIconRoot;
    
    private BattleEntity _currentEntity;

    private readonly List<GameObject> _spawnedSubMenuButtons = new();

    #region 回调

    // 一级命令选择完成后，回传给上层调用者 (通常是 PlayerInputState)
    // 用来告诉战斗状态机：玩家这次选的是 Attack / Defend / Escape 这类主命令
    private Action<E_BattleCommandType> _onCommandSelected;
    
    // 技能按钮点下后，回传给上层调用者 (通常是 PlayerInputState)
    // 用来把当前选中的 SkillData_SO 交出去，后续由上层组装成技能命令
    private Action<SkillData_SO> _onSkillSelected;

    // 物品按钮点下后，回传给上层调用者 (通常是 PlayerInputState)
    // 用来把当前选中的 ItemDefinition_SO 交出去，后续由上层组装成技能命令
    private Action<ItemDefinition_SO>  _onItemSelected; 
    
    #endregion

    private Button _lastPrimaryButton;
    private bool _subMenuOpen;
    
    protected override void Awake()
    {
        base.Awake();
        BindPrimaryButtons();
        commandMenuCanvasGroup.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!commandMenuCanvasGroup.gameObject.activeSelf) return;

        var input = InputSystemController.Instance;

        if (input is not null && input.GetUICancelPressed() && _subMenuOpen)
            CloseSubMenu();
    }

    private void BindPrimaryButtons()
    {
        btnAttack.onClick.AddListener(()=> OnCommandClicked(E_BattleCommandType.Attack));
        btnSkill.onClick.AddListener(()=> OnCommandClicked(E_BattleCommandType.Skill));
        btnItem.onClick.AddListener(()=> OnCommandClicked(E_BattleCommandType.Item));
        btnDefend.onClick.AddListener(() => OnCommandClicked(E_BattleCommandType.Defend));
        btnEscape.onClick.AddListener(() => OnCommandClicked(E_BattleCommandType.Escape));
    }

    private void OnCommandClicked(E_BattleCommandType commandType)
    {
        if (commandType == E_BattleCommandType.Skill)
        {
            OpenSkillMenu();
            return;
        }

        if (commandType == E_BattleCommandType.Item)
        {
            OpenItemMenu();
            return;
        }

        CloseAndInvoke(commandType);
    }

    private void CloseAndInvoke(E_BattleCommandType commandType)
    {
        ClosePanel();
        _onCommandSelected?.Invoke(commandType);
        _onCommandSelected = null;
    }

    public void ShowPanel()
    {
        commandMenuCanvasGroup.gameObject.SetActive(true);
        btnAttack.Select();
    }

    public void ClosePanel()
    {
        CloseSubMenu();
        commandMenuCanvasGroup.gameObject.SetActive(false);
    }


    #region 二级菜单

    private void OpenSkillMenu()
    {
        int currentSP = _currentEntity.CurrentSP;
        List<SkillData_SO> skills = _currentEntity.Definition.InitialSkills;

        if (skills.Count == 0)
            return;
        
        BeginSubMenu(btnSkill);
        Button firstButton = null;
        foreach (var skill in skills)
        {
            if (skill is null) return;

            SkillButton skillButton = Instantiate(skillButtonPrefab, commandMenuPanel);
            skillButton.SetupSkillButton(skill);
            var button = skillButton.GetComponent<Button>();
            
            bool canCast = skill.spCost <= currentSP;
            button.interactable = canCast;
            ApplySkillButtonVisual(skillButton, canCast);
            
            button.onClick.AddListener(() => OnSkillButtonClick(skill));

            if (firstButton is null && canCast)
                firstButton = button;
            
            _spawnedSubMenuButtons.Add(skillButton.gameObject);
        }

        if (_spawnedSubMenuButtons.Count == 0)
        {
            CloseSubMenu();
            return;
        }

        if (firstButton is not null) 
            firstButton.Select();
    }

    private void OpenItemMenu()
    {
        BeginSubMenu(btnItem);
        
        var inventory = InventoryManager.Instance;
        Button firstButton = null;

        foreach (var item in inventory.CurrentInventory)
        {
            if (!item.IsConsumable)
                continue;
            
            var itemButton = Instantiate(itemButtonPrefab, commandMenuPanel);
            itemButton.SetupButton(item, OnItemButtonClick);

            if (firstButton is null)
                firstButton = itemButton.CurrentButton;
            
            _spawnedSubMenuButtons.Add(itemButton.gameObject);
        }

        if (_spawnedSubMenuButtons.Count == 0)
        {
            CloseSubMenu();
            return;
        }
        
        firstButton?.Select();
    }

    private void BeginSubMenu(Button returnButton)
    {
        ClearSubMenuButtons();
        commandMenuPanel.gameObject.SetActive(true);
        _subMenuOpen = true;
        _lastPrimaryButton = returnButton;
        commandMenuCanvasGroup.interactable = false;
    }

    private void CloseSubMenu(bool restorePrimarySelection = true)
    {
        ClearSubMenuButtons();
        commandMenuPanel.gameObject.SetActive(false);
        commandMenuCanvasGroup.interactable = true;

        if (restorePrimarySelection && _subMenuOpen)
            _lastPrimaryButton?.Select();
        
        _lastPrimaryButton = null;
        _subMenuOpen = false;
    }

    private void ClearSubMenuButtons()
    {
        foreach (var button in _spawnedSubMenuButtons)
            Destroy(button);
        _spawnedSubMenuButtons.Clear();
    }

    public void RequestInput(BattleEntity entity,
        Action<E_BattleCommandType> onCommandSelected,
        Action<SkillData_SO> onSkillSelected,
        Action<ItemDefinition_SO> onItemSelected)
    {
        _currentEntity = entity;
        _onCommandSelected = onCommandSelected;
        _onSkillSelected = onSkillSelected;
        _onItemSelected = onItemSelected;

        RefreshAttackTypeIcon();
        ShowPanel();
    }

    private void OnSkillButtonClick(SkillData_SO skill)
    {
        ClosePanel();
        _onSkillSelected?.Invoke(skill);
        _onSkillSelected = null;
    }

    private void OnItemButtonClick(ItemDefinition_SO Item)
    {
        ClosePanel();
        _onItemSelected?.Invoke(Item);
        _onItemSelected = null;
    }

    private void ApplySkillButtonVisual(SkillButton skillButton, bool canCast)
    {
        float alpha = canCast ? 1f : 0.5f;
        foreach (Graphic graphic in skillButton.GetComponentsInChildren<Graphic>(true))
        {
            Color color = graphic.color;
            color.a = alpha;
            graphic.color = color;
        }
    }

    #endregion

    #region 攻击类型显示

    private void RefreshAttackTypeIcon()
    {
        SkillData_SO basicAttack = _currentEntity.Definition.BasicAttack;
        var damageType = basicAttack.ResolveDamageType();
        if (damageType is E_DamageType.None or E_DamageType.Untyped)
        {
            SetAttackTypeIconVisible(false);
            return;
        }
        var icon = damageTypeIconSet.GetIcon(damageType);
        foreach (var image in attackTypeIconImage)
        {
            image.sprite = icon;
        }
        SetAttackTypeIconVisible(true);
    }

    private void SetAttackTypeIconVisible(bool visible)
    {
        for (int i = 0; i < attackTypeIconRoot.Length; i++)
        {
            attackTypeIconRoot[i].SetActive(visible);
            attackTypeIconImage[i].enabled = visible;
            attackTypeIconImage[i].gameObject.SetActive(visible);
        }
        
    }

    #endregion
}
