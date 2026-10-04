using UnityEngine.AddressableAssets;

public class ChallengeAction : ActionBase
{
    [Header("Challenge Action")]
    public AssetReference battleSceneReference;
    public List<CharacterDefinition_SO> npcTeamMembers;
    
    [Header("阵型")]
    public E_EnemyLayoutFormation enemyLayoutFormation = E_EnemyLayoutFormation.Line;
    public CharacterDefinition_SO Currentcharacter { get; private set; }

    public int LastDifficulty { get; private set; }

    private void Awake()
    {
        Currentcharacter = GetComponentInParent<CharacterIdentity>().CharacterDefinition;
        
        npcTeamMembers ??= new();
        if (!npcTeamMembers.Contains(Currentcharacter))
            npcTeamMembers.Insert(0, Currentcharacter);
    }

    public override void TriggerAction(AllyDefinition_SO interactor)
    {
        LastDifficulty = EvaluateDifficulty(EvaluatePlayerTeamPower(), EvaluateEnemyTeamPower());
        EventBus.Publish(new PanelRequestEvent(this));
    }

    public override void Execute()
    {
        // 挑战确认后，直接交给 BattleService 组装开战数据并切场景
        BattleService.Instance.StartBattleFromAction(this);
    }



    #region 计算战斗力

    private int EvaluatePlayerTeamPower()
    {
        var partyMembers = PartyManager.Instance.PartyMembers;
        var totalPower = 0;
        foreach (var member in partyMembers)
            totalPower += CharacterRuntimeData.EvaluatePowerFromStats(member.GetTotalStats());
        
        return totalPower;
    }

    private int EvaluateEnemyTeamPower()
    {
        var totalPower = 0;
        foreach (var member in npcTeamMembers)
            totalPower += CharacterRuntimeData.EvaluatePowerFromStats(member.BaseStats);
        
        return totalPower;
    }

    private int EvaluateDifficulty(int playerPower, int enemyPower)
    {
        float ratio = enemyPower / (float)playerPower;

        return ratio switch
        {
            < 0.55f => 1,
            < 0.65f => 2,
            < 0.75f => 3,
            < 0.85f => 4,
            < 1.05f => 5,
            < 1.20f => 6,
            < 1.40f => 7,
            < 1.65f => 8,
            < 1.95f => 9,
            _ => 10
        };
    }

    #endregion
}
