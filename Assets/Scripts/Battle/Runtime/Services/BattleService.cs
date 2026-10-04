using MFramework.Event;
using UnityEngine.AddressableAssets;

public class BattleService : Singleton<BattleService>,
    IEventReceiver<BattleResultConfirmedEvent>
{
    private AssetReference _returnSceneAfterBattle;

    private Vector3 _returnPosition;
    private Quaternion _returnRotation;

    // 战斗启动缓存
    private BattleStartPayload _pendingPayload;
    public bool HasPendingPayload => _pendingPayload is not null;

    private void OnEnable()
    {
        EventBus.Subscribe<BattleResultConfirmedEvent>(this);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<BattleResultConfirmedEvent>(this);
    }

    public BattleStartPayload ConsumeStartPayload()
    {
        var payload = _pendingPayload;
        _pendingPayload = null;
        return payload;
    }

    public void StartBattleFromAction(ChallengeAction action)
    {
        List<CharacterRuntimeData> allies = new(PartyManager.Instance.PartyMembers);
        List<CharacterRuntimeData> enemies = new();
        foreach (var definition in action.npcTeamMembers)
        {
            if (definition is not null)
                enemies.Add(new CharacterRuntimeData(definition));
        }

        StartBattle(allies, enemies, action.battleSceneReference, action.enemyLayoutFormation);
    }

    public void StartBattle(
        List<CharacterRuntimeData> allies, 
        List<CharacterRuntimeData> enemies,
        AssetReference battleScene,
        E_EnemyLayoutFormation  formation)
    {
        SceneLoadManager sceneLoadManager = SceneLoadManager.Instance;

        _returnSceneAfterBattle = sceneLoadManager.activeScene;

        // 记录进战斗前的位置与朝向，战斗结束后传送回触发点
        Transform playerTrans = PartyManager.Instance.FieldController.PlayerTrans;
        _returnPosition = playerTrans.position;
        _returnRotation = playerTrans.rotation;

        // 开始前把友军的战斗会话状态整理好：
        // 1) BP 归零，保证每场战斗都从头开始积攒
        // 2) 如果角色当前已经倒地，就抬到 1 HP，避免直接以死亡状态进入战斗
        NormalizeBattleSessionState(PartyManager.Instance.PartyMembers);
        
        // 启动载荷
        _pendingPayload = new(
            new List<CharacterRuntimeData>(allies),
            new List<CharacterRuntimeData>(enemies),
            formation);
        
        // 加载场景
        sceneLoadManager.RequestSceneLoad(new SceneLoadRequest(
            battleScene,
            E_FadeStyle.WipeMask,
            E_GameMode.Battle
            ));
    }

    private void NormalizeBattleSessionState(List<CharacterRuntimeData> members)
    {
        foreach (var member in members)
        {
            member.ResetBattleBP();
            if (member.CurrentHP < 0)
                member.CurrentHP = 1;
        }
    }

    #region 事件接口

    public void OnEvent(BattleResultConfirmedEvent evt)
    {
        ReturnToPreviousScene();
    }

    #endregion
    
    /// <summary>
    /// 战斗离场：回到进入战斗前的场景
    /// 逃跑与结算确认都会复用这一套回场流程
    /// </summary>
    public void ReturnToPreviousScene()
    {
        // 1. 请求返回上一个场景，并传回进战斗前的落点，直接传送回触发位置
        SceneLoadManager.Instance.RequestSceneLoad(new SceneLoadRequest(
            _returnSceneAfterBattle,
            E_FadeStyle.PanelFade,
            E_GameMode.Explore,
            _returnPosition,
            _returnRotation));

        // 2. 回切请求发出后，立刻清掉本次战斗缓存，避免后面误复用旧数据
        _pendingPayload = null;
        _returnSceneAfterBattle = null;
        
        // 3. 只要本次战斗已经离场，下一场就应该从新的战斗会话开始
        NormalizeBattleSessionState(PartyManager.Instance.PartyMembers);
    }

    public void EnterMenuForGameOver()
    {
        SceneLoadManager sceneLoadManager = SceneLoadManager.Instance;
        
        // 1. 直接切回 Menu 场景
        sceneLoadManager.RequestSceneLoad(new SceneLoadRequest(
            sceneLoadManager.MenuScene,
            E_FadeStyle.PanelFade,
            E_GameMode.InteractionMenu,
            null));
        
        // 2. 失败回标题后，不再需要保留这场战斗的缓存
        _pendingPayload = null;
        _returnSceneAfterBattle = null;
        _returnPosition = Vector3.zero;
    }

}
