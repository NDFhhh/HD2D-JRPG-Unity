
using MFramework.Event;

[RequireComponent(typeof(PartyFieldController))]
public class PartyManager : Singleton<PartyManager>, 
    IEventReceiver<GameModeChangedEvent>,
    IEventReceiver<ChooseCharacterButtonOnClickEvent>
{
    private PartyFieldController fieldController;

    /// <summary>场景中玩家与跟随队伍的控制器 (用于读取玩家位置等)</summary>
    public PartyFieldController FieldController => fieldController;
    
    [Header("Initial Party")]
    private CharacterDefinition_SO PlayerDefinition;

    /// <summary>
    /// 当前队长定义 (队伍还没建起来时就读最近一次选择)
    /// 队伍数据的唯一真相源：和场景里玩家表现出的角色必须一致
    /// </summary>
    public CharacterDefinition_SO LeaderDefinition =>
        partyMembers.Count > 0 ? partyMembers[0].Definition : PlayerDefinition;

    [SerializeField] private List<CharacterRuntimeData> partyMembers = new();
    public List<CharacterRuntimeData> PartyMembers => partyMembers;

    private bool fieldActorsHidden;

    protected override void Awake()
    {
        base.Awake();
        fieldController = GetComponent<PartyFieldController>();
    }

    private void Start()
    {
        ApplyInitialEquipment();
    }

    private void OnEnable()
    {
        EventBus.Subscribe<GameModeChangedEvent>(this);
        EventBus.Subscribe<ChooseCharacterButtonOnClickEvent>(this);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<GameModeChangedEvent>(this);
        EventBus.Unsubscribe<ChooseCharacterButtonOnClickEvent>(this);
    }

    /// <summary>
    /// 设置（或替换）队伍队长。
    /// 队伍为空 = 用选中的角色建队；已有队长 = 把队长换成新选中的角色。
    /// 队伍数据必须跟着"最新一次选择"走，否则会出现
    /// "玩家模型已经换成第二个角色、队伍数据还停在第一个角色"的错位。
    /// </summary>
    public void SetLeader(CharacterDefinition_SO definition)
    {
        if (definition == null)
            return;

        PlayerDefinition = definition;

        if (partyMembers.Count == 0)
        {
            partyMembers.Add(new CharacterRuntimeData(definition));
        }
        else if (partyMembers[0].Definition != definition)
        {
            // 换人：先收回上一个队长初始化时塞进背包的初始装备，再整体重建队长数据
            RevertInitialEquipment(partyMembers[0]);
            partyMembers[0] = new CharacterRuntimeData(definition);
        }
        else
        {
            // 重复选中同一个角色，不改动任何数据
            return;
        }

        RefreshFieldFollowers();
    }

    /// <summary>
    /// 收回 CharacterRuntimeData 初始化时发放到背包的初始装备。
    /// (CharacterRuntimeData.ApplyInitialEquipment 会把初始装备复制一份进背包，
    ///  换队长时如果不收回来，就会留下上一个角色的装备)
    /// </summary>
    private void RevertInitialEquipment(CharacterRuntimeData member)
    {
        InventoryManager inventory = InventoryManager.Instance;
        if (inventory == null || member == null)
            return;

        if (member.Definition is not AllyDefinition_SO allyDefinition)
            return;

        if (allyDefinition.InitialEquipment == null)
            return;

        foreach (var entry in allyDefinition.InitialEquipment)
        {
            if (entry.item == null)
                continue;

            // 只收回确实还在背包里的那一份，避免误删玩家自己捡到的道具
            if (inventory.GetItemQuantity(entry.item) <= 0)
                continue;

            inventory.RemoveItem(entry.item, 1);
        }
    }

    private void AddMember(CharacterDefinition_SO  characterDefinition)
    {
        partyMembers.Add(new CharacterRuntimeData(characterDefinition));
        RefreshFieldFollowers();
    }

    /// <summary>
    /// 是否已包含该角色定义 (用于招募点 / NPC 可见性判断)
    /// </summary>
    public bool HasMember(CharacterDefinition_SO definition)
    {
        foreach (CharacterRuntimeData member in partyMembers)
        {
            if (member.Definition == definition)
                return true;
        }
        
        return false;
    }

    public void RecruitMember(CharacterDefinition_SO newCharacter)
    {
        FadeController.Instance.SetStyle(E_FadeStyle.PanelFade);
        FadeController.Instance.FadeOut(() =>
        {
            AddMember(newCharacter);
            FadeController.Instance.FadeIn(() => GameModeManager.Instance.RequestChangeMode(E_GameMode.Explore));
        });
        
    }
    
    private void RefreshFieldFollowers()
    {
        List<CharacterDefinition_SO> defs = new(partyMembers.Count);
        foreach (CharacterRuntimeData member in partyMembers)
            defs.Add(member.Definition);
        
        fieldController.UpdateFollowers(defs);
    }

    public void ApplyInitialEquipment()
    {
        foreach (CharacterRuntimeData member in partyMembers)
            member.ApplyInitialEquipment();
    }

    #region 事件监听

    public void OnEvent(GameModeChangedEvent evt)
    {
        if (evt.newMode == E_GameMode.Battle)
        {
            if (fieldActorsHidden)
                return;
            
            fieldActorsHidden = true;
            fieldController.SetPlayerActive(false);
            fieldController.ClearFollower();
            return;
        }

        if (evt.newMode == E_GameMode.Explore)
        {
            if (!fieldActorsHidden)
                return;
            
            fieldActorsHidden = false;
            fieldController.SetPlayerActive(true);
            RefreshFieldFollowers();
        }
    }
    
    public void OnEvent(ChooseCharacterButtonOnClickEvent evt)
    {
        SetLeader(evt.CharacterIdentity.CharacterDefinition);
    }

    #endregion

    #region 队伍移动

    /// <summary>
    /// 对外统一入口：重定位玩家与队伍小火车到指定位置
    /// 用于场景切换后的出生落位
    /// </summary>
    public void TeleportPartyTo(Vector3 position, Quaternion rotation)
    {
        if (fieldActorsHidden)
        {
            fieldActorsHidden = false;
            fieldController.SetPlayerActive(true);
            RefreshFieldFollowers();
        }
        
        fieldController.TeleportPartyTo(position, rotation);
    }

    #endregion
    
}
