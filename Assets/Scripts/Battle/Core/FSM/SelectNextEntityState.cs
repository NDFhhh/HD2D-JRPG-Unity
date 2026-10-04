/// <summary>
/// 选择下一位行动者状态
///
/// 这个状态主要负责：
/// 1) 先检查战斗是否已经结束
/// 2) 再从 BattleRoundScheduler 里取出下一位真正要行动的实体
/// 3) 同步当前行动者事件和时间轴显示
/// 4) 最后根据阵营切到 玩家输入状态 或 敌方AI状态
///
/// 可以把它理解成：
/// “状态机里的回合分发器”
/// </summary>
public class SelectNextEntityState : BattleState
{
    public SelectNextEntityState(BattleController controller) : base(controller)
    {
    }

    public override IEnumerator Execute()
    {
        // 1. 检查战斗是否已经结束
        if (BattleOutcomeResolver.TryGetBattleEndedEvent(_controller.AllEntities, out BattleEndedEvent endedEvent))
        {
            _controller.EndBattle(endedEvent);
            yield break;
        }

        // 2. 从 BattleRoundScheduler 里取出下一位真正要行动的实体
        BattleEntity nextEntity = _controller.GetNextActorByRound();

        // 2.5 BOSS 阶段切换演出：多个敌人同时进入新阶段时排队，一个一个播放
        //     当前敌人的阶段提示 + 音效播完，再落地下一个
        while (_controller.TryDequeuePendingPhaseEntity(out BattleEntity phaseEntity))
        {
            // 落地时可能因破盾等状态不再允许切换，跳过它继续下一个
            if (!phaseEntity.TryApplyPendingBossPhase(out BossPhaseConfig appliedPhase))
                continue;

            // 提示文字显示总时长与阶段音效时长保持一致
            float soundDuration = appliedPhase.ResolveSoundDuration();
            EventBus.Publish(new BattleNotificationEvent(
                phaseEntity.ResolveBossPhasePrompt(appliedPhase),
                displayDuration: soundDuration));

            // 等当前阶段音效播完，再播下一个敌人的阶段 / 进入下一战斗状态
            if (soundDuration > 0f)
                yield return new WaitForSeconds(soundDuration);
        }

        // 3. 记录当前行动者，并立刻刷新时间轴预测，让 UI 先同步这一轮队列变化
        _controller.CurrentEntity = nextEntity;
        _controller.UpdateTimelinePrediction();

        if (_controller.Config.TurnStartDelay > 0f)
            yield return new WaitForSeconds(_controller.Config.TurnStartDelay);
        
        // 4. 更新当前行动者的大头像与名字
        _controller.TimelineUI.SetActiveEntity(nextEntity);
        // 5. “广播” 当前轮到谁行动了
        EventBus.Publish(new ActiveEntityChangedEvent(nextEntity));
        
        // 6. 最后根据阵营决定下一步去哪里
        //    玩家进输入状态，敌人进 AI 状态
        if (nextEntity.IsPlayer)
            _controller.SetState(new PlayerInputState(_controller));
        else
            _controller.SetState(new EnemyAIState(_controller));
    }
}
