using MFramework.Event;
using TMPro;
using UnityEngine.UI;

public class BattleResultPanelController : MonoBehaviour,
    IEventReceiver<BattleResultViewEnterEvent>,
    IEventReceiver<BattleLoseViewEnterEvent>
{
    #region 结算面板配置

    [Header("Panel Root")]
    [SerializeField] private GameObject winPanelRoot;
    [SerializeField] private CanvasGroup winCanvasGroup;
    [SerializeField] private float fadeInDuration = 0.3f;
    [SerializeField] private RectTransform infoHUDRoot;
    [SerializeField] private RectTransform lootItemRoot;
    [SerializeField] private GameObject losePanelRoot;
    [SerializeField] private CanvasGroup loseCanvasGroup;

    [Header("Result Text")] 
    [SerializeField] private TMP_Text expRewardText;

    [SerializeField] private TMP_Text moneyRewardText;
    [SerializeField] private TMP_Text moneyCurrentText;

    [Header("Prefab")] 
    [SerializeField] private InfoHUD infoHUDPrefab;
    [SerializeField] private LootItem lootItemPrefab;

    [Header("Action")] 
    [SerializeField] private Button confirmButton;
    [SerializeField] private bool hideOnConfirm = true;
    [SerializeField] private Button loseConfirmButton;

    [Header("Exp Animation")] [SerializeField]
    private float expTweenStagger = 0.2f;

    #endregion

    #region 运行时缓存

    private Coroutine _fadeRoutine;

    #endregion

    private void Awake()
    {
        HideImmediate();
    }

    private void OnEnable()
    {
        EventBus.Subscribe<BattleResultViewEnterEvent>(this);
        EventBus.Subscribe<BattleLoseViewEnterEvent>(this);
        confirmButton.onClick.AddListener(OnConfirmClicked);
        loseConfirmButton.onClick.AddListener(OnLoseConfirmClicked);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<BattleResultViewEnterEvent>(this);
        EventBus.Unsubscribe<BattleLoseViewEnterEvent>(this);
        confirmButton.onClick.RemoveListener(OnConfirmClicked);
        loseConfirmButton.onClick.RemoveListener(OnLoseConfirmClicked);
    }

    #region 接口

    public void OnEvent(BattleResultViewEnterEvent evt)
    {
        winPanelRoot.SetActive(true);
        ApplyInventoryRewards(evt);
        RefreshRewardText(evt);
        RebuildPartyInfoHud(evt);
        RebuildLootItems(evt);
        
        StopFadeRoutine();
        _fadeRoutine = StartCoroutine(FadeInRoutine(winCanvasGroup));
        
        confirmButton.Select();
    }

    public void OnEvent(BattleLoseViewEnterEvent evt)
    {
        // 失败结果只显示失败面板，不再刷奖励数据
        winPanelRoot.SetActive(false);
        losePanelRoot.SetActive(true);
        
        StopFadeRoutine();
        _fadeRoutine = StartCoroutine(FadeInRoutine(loseCanvasGroup));
        
        loseConfirmButton.Select();
    }
    
    #endregion

    /// <summary>
    /// 把结算金币和掉落写回运行时背包
    /// </summary>
    private void ApplyInventoryRewards(BattleResultViewEnterEvent result)
    {
        // 1. 先把金币写回背包
        InventoryManager inventory = InventoryManager.Instance;
        inventory.AddCurrency(result.MoneyReward);
        
        // 2. 再把掉落物品逐个写回背包
        var drops = result.DropRewards;
        for (int i = 0; i < drops.Count; i++)
        {
            var drop = drops[i];
            inventory.AddItem(drop.ItemDefinition, drop.Quantity);
        }
    }

    /// <summary>
    /// 刷新基础奖励文本
    /// </summary>
    private void RefreshRewardText(BattleResultViewEnterEvent result)
    {
        expRewardText.text = "+" + result.ExpReward;
        moneyRewardText.text = "+" + result.MoneyReward;
        moneyCurrentText.text = InventoryManager.Instance.Currency.ToString();
    }

    /// <summary>
    /// 重建队伍 HUD，并在这里完成经验和倒地复活规则 
    /// </summary>
    private void RebuildPartyInfoHud(BattleResultViewEnterEvent result)
    {
        ClearChildren(infoHUDRoot);
        var partyMembers = PartyManager.Instance.PartyMembers;
        
        // 1. 先统计本场还能吃经验的存活角色数量
        int aliveCount = 0;
        for (int i = 0; i < partyMembers.Count; i++)
        {
            if (partyMembers[i].CurrentHP > 0)
                aliveCount++;
        }
        
        // 2.再把总经验平分给存活角色，余数从前往后补
        int baseExp = aliveCount > 0 ? result.ExpReward / aliveCount : 0;
        int remainder = aliveCount > 0 ? result.ExpReward % aliveCount : 0;
        int aliveIndex = 0;

        for (int i = 0; i < partyMembers.Count; i++)
        {
            var member = partyMembers[i];
            if (member.CurrentHP <= 0)
            {
                member.CurrentHP = 1;
            }
            // 3. 先把结算前的显示值刷出来，再把实际生效的经验值交给 InfoHUD 播放动画
            InfoHUD hud = Instantiate(infoHUDPrefab, infoHUDRoot);
            int startLevel = member.Level;
            int startExp = member.CurrentExp;
            int gainExp = member.CurrentHP > 0 ? baseExp + (aliveIndex++ < remainder ? 1 : 0) : 0;    // 如果角色死了，经验就为 0
            int showTargetExp = member.GetExpRequiredToNextLevel();
            float startProgress = member.GetExpProgress01();    // 获取当前经验值占总经验值的比例

            hud.SetInfo(member.Definition.Name, startLevel, startExp, showTargetExp, startProgress,
                member.Definition.Portrait);
            
            // 动画
            int appliedExp = member.AddExp(gainExp);
            hud.PlayExpGainAnimation(member, appliedExp, expTweenStagger * i);
        }
        
        LayoutRebuilder.ForceRebuildLayoutImmediate(infoHUDRoot);
    }

    /// <summary>
    /// 根据掉落数据重建道具列表 UI
    /// </summary>
    private void RebuildLootItems(BattleResultViewEnterEvent result)
    {
        ClearChildren(lootItemRoot);
        var drops = result.DropRewards;
        for (int i = 0; i < drops.Count; i++)
        {
            var drop = drops[i];
            LootItem item = Instantiate(lootItemPrefab, lootItemRoot);
            item.SetLootItem(new InventoryItem(drop.ItemDefinition, drop.Quantity));
        }
        
        LayoutRebuilder.ForceRebuildLayoutImmediate(lootItemRoot);
    }
    
    /// <summary>
    /// 玩家确认结算后，只发结果确认事件
    /// </summary>
    private void OnConfirmClicked()
    {
        EventBus.Publish(new BattleResultConfirmedEvent());
        if (hideOnConfirm)
            HideImmediate();
    }

    /// <summary>
    /// 玩家在失败面板点击确认后，直接切回 Menu
    /// </summary>
    private void OnLoseConfirmClicked()
    {
        BattleService.Instance.EnterMenuForGameOver();
        if (hideOnConfirm)
            HideImmediate();
    }

    #region Helper

    private IEnumerator FadeInRoutine(CanvasGroup canvasGroup)
    {
        canvasGroup.alpha = 0f;
        float t = 0f;
        while (t < fadeInDuration)
        {
            t += Time.deltaTime;
            canvasGroup.alpha = Mathf.Clamp01(t / fadeInDuration);
            yield return null;
        }
        
        canvasGroup.alpha = 1f;
        _fadeRoutine = null;
        //confirmButton.Select();
    }
    
    private void ClearChildren(Transform root)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            Destroy(root.GetChild(i).gameObject);
        }
    }

    private void StopFadeRoutine()
    {
        if (_fadeRoutine == null)
            return;
        
        StopCoroutine(_fadeRoutine);
        _fadeRoutine = null;
    }

    private void HideImmediate()
    {
        StopFadeRoutine();
        winCanvasGroup.alpha = 0f;
        loseCanvasGroup.alpha = 0f;
        ClearChildren(infoHUDRoot);
        ClearChildren(lootItemRoot);
        winPanelRoot.SetActive(false);
        losePanelRoot.SetActive(false);
    }

    #endregion

    
}
