/// <summary>
/// 战斗开始场景
///
/// 这个状态主要负责：
/// 1) 读取 BattleStartPayload，在战斗场景里生成敌我双方单位
/// 2) 把 RuntimeData、BattleUnit、BattleEntity 三层正式绑起来
/// 3) 播放最小开场入场表现
/// 4) 初始化首轮时间轴，然后切到“选择下一位行动者”状态
///
/// 可以把它理解成：
/// “战斗开始后的总装配状态”
/// </summary>
public class BattleSetupState : BattleState
{
    private readonly BattleStartPayload _startPayload;
    
    public BattleSetupState(BattleController controller,BattleStartPayload startPayload) : base(controller)
    {
        _startPayload = startPayload;
    }

    public override IEnumerator Execute()
    {
        EventBus.Publish(new GameModeChangedEvent(E_GameMode.Battle));
        
        // 1. 先按 payload 在战斗场景里生成敌我双方的 BattleUnit
        _controller.FieldManager.SpawnAll(_startPayload);
        
        // 2. 再把 RuntimeData、BattleUnit 和 BattleEntity 正式绑起来
        _controller.AllEntities.Clear();
        var allyEntities = CreateEntities(_startPayload.Allies, _controller.FieldManager.SpawnedAllyUnits, true);
        _controller.AllEntities.AddRange(allyEntities);
        _controller.AllEntities.AddRange(CreateEntities(_startPayload.Enemies,
            _controller.FieldManager.SpawnedEnemyUnits, false));
        
        // 3. 友军先从入场点跑到各自站位，敌军默认直接站好
        float runtime = _controller.Config.StartBattleDelay;
        if (runtime > 0f)
        {
            yield return new WaitForSeconds(runtime);

            foreach (var entity in allyEntities)
            {
                Vector3 homePos = _controller.FieldManager.GetHomePos(entity.Unit);
                _controller.StartCoroutine(entity.Unit.MoveToPosition(homePos, runtime));
            }
            yield return new WaitForSeconds(runtime);
        }
        
        // 4. 战斗正式开始，通知 HUD 建立显示，并初始化首轮时间轴
        EventBus.Publish(new BattleStartedEvent());
        _controller.StartNewRound();
        _controller.UpdateTimelinePrediction();
        
        yield return new WaitForSeconds(0.8f);
        
        _controller.SetState(new SelectNextEntityState(_controller));
        yield break;
    }

    private List<BattleEntity> CreateEntities(List<CharacterRuntimeData> runtimeList, IReadOnlyList<BattleUnit> units, bool isPlayer)
    {
        var entities = new List<BattleEntity>(runtimeList.Count);
        string side = isPlayer ? "P" : "E";

        for (int i = 0; i < runtimeList.Count; i++)
        {
            var runtimeData = runtimeList[i];
            BattleUnit unit = units[i];
            var entity = new BattleEntity(runtimeData, unit, isPlayer, $"{side}_{i:D2}_{runtimeData.Definition.ID}");
            unit.Bind(entity);
            entities.Add(entity);
        }
        
        return entities;
    }
}
