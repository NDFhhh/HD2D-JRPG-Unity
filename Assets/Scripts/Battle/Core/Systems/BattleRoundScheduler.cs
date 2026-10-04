/// <summary>
/// 正式的 CTB / 双列表回合调度器
///
/// 这个类的职责非常明确：
/// 1. 维护 “当前回合剩余行动列表” 和 “下一回合预测列表”
/// 2. 决定下一位真正能行动的 BattleEntity
/// 3. 把结果整理成时间轴 UI 能直接使用的预测节点
///
/// 教程里可以把它理解成：
/// “只负责排序，不负责播放 UI 和命令执行”的纯调度层
/// </summary>
public class BattleRoundScheduler
{
    private List<BattleEntity> _currentRound = new();
    private List<BattleEntity> _nextRound = new();

    // 记录 “已从 next 队列剔除” 的破盾单位
    // 这些单位会在下一回合开始时消费一次 Break 跳过记数，避免提前恢复
    private readonly Dictionary<BattleEntity, int> _pendingNextRoundBreakSkip = new();

    // 回合开始时登记 “有挂起阶段” 的 BOSS
    // 调度器本身是同步代码不能做演出等待，交给协程上下文（SelectNextEntityState）逐一落地：
    // 一个敌人的阶段提示 + 音效播完，再播下一个
    private readonly List<BattleEntity> _pendingPhaseEntities = new();

    /// <summary>
    /// 逐个取出登记了阶段切换的 BOSS（取空后返回 false）
    /// </summary>
    public bool TryDequeuePendingPhaseEntity(out BattleEntity entity)
    {
        if (_pendingPhaseEntities.Count > 0)
        {
            entity = _pendingPhaseEntities[0];
            _pendingPhaseEntities.RemoveAt(0);
            return true;
        }

        entity = null;
        return false;
    }
    
    public void Initialize(List<BattleEntity> allEntities)
    {
        _currentRound = GenerateSortOrder(allEntities);
        _nextRound = GenerateSortOrder(allEntities);
        TriggerRoundStart(allEntities);
    }

    /// <summary>
    /// 获取下一位真正要行动的单位
    /// 这个入口只做 “取人” 和 “跳过不可行动者”
    /// 不负责命令输入、AI、表现播放
    /// </summary>
    public BattleEntity GetNextActor(List<BattleEntity> allEntities)
    {
        // 1. 场上已经没有活人时，就不再继续取行动者
        int aliveCount = 0;
        for (int i = 0; i < allEntities.Count; i++)
        {
            if (allEntities[i].IsAlive)
                aliveCount++;
        }
        if (aliveCount <= 0)
            return null;
        
        // 2. 加一层 guard，避免极端情况下死循环
        int guard = aliveCount * 8;
        while (guard-- > 0)
        {
            // 3. 当前回合已经取空了，就推进到下一回合
            if (_currentRound.Count == 0)
            {
                StartNextRound(allEntities);
                if(_nextRound.Count == 0)
                    continue;
            }
            
            BattleEntity candidate = _currentRound[0];
            _currentRound.RemoveAt(0);
            if (!candidate.IsAlive)
                continue;
            
            return candidate;
        }
        
        return null;
    }

    private void StartNextRound(List<BattleEntity> allEntities)
    {
        // 先把下一回合顶上来，变成新的当前回合
        _currentRound = _nextRound;
        // 再重新生成一份新的下一回合预测
        _nextRound = GenerateSortOrder(allEntities);
        
        TriggerRoundStart(allEntities);
    }

    private List<BattleEntity> GenerateSortOrder(List<BattleEntity> allEntities)
    {
        List<BattleEntity> result = new();
        for (int i = 0; i < allEntities.Count; i++)
        {
            if(allEntities[i].IsAlive)
                result.Add(allEntities[i]);
        }

        result.Sort ((a, b) =>
        {
            int speedCompare = b.GetCurrentSpeed() - a.GetCurrentSpeed();
            if(speedCompare != 0)
                return speedCompare;
            
            return string.CompareOrdinal(a.ID, b.ID);
        });

        string orderLog = "CTB顺序";
        for (int i = 0; i < result.Count; i++)
        {
            BattleEntity entity = result[i];
            orderLog += $"{i + 1}.{entity.RuntimeData.Definition.Name}({entity.GetCurrentSpeed()})";
        }
        Debug.Log(orderLog);
        
        return result;
    }

    /// <summary>
    /// 把当前调度结果转换成 Timeline UI 能直接使用的预测节点
    /// </summary>
    public List<BattleTimelinePredictionNode> BuildTimelinePrediction()
    {
        List<BattleTimelinePredictionNode> result = new(_currentRound.Count+_nextRound.Count);
        
        // Round 0 = 当前回合剩余行动者
        for (int i = 0; i < _currentRound.Count; i++)
        {
            BattleEntity entity = _currentRound[i];
            if (!entity.IsAlive)
                continue;
            result.Add(new BattleTimelinePredictionNode($"{entity.ID}_R0",entity,0));
        }
        
        // Round 1 = 下一回合预测
        for (int i = 0; i < _nextRound.Count; i++)
        {
            BattleEntity entity = _nextRound[i];
            if (!entity.IsAlive)
                continue;
            result.Add(new BattleTimelinePredictionNode($"{entity.ID}_R1", entity, 1));
        }
        
        return result;
    }
    
    private void TriggerRoundStart(List<BattleEntity> allEntities)
    {
        for (int i = 0; i < allEntities.Count; i++)
        {
            var entity = allEntities[i];
            if (!entity.IsAlive)
                continue;
            
            // 1. 先处理 Break 恢复，避免单位永久停留在破盾状态
            entity.ResolveBreakRecoveryAtRoundStart();
            
            // 2. 再消费上一轮挂起 “下一回合开场” 才生效的 Break 跳过计数
            if (_pendingNextRoundBreakSkip.Remove(entity))
            {
                entity.ConsumeBrokenTurnsByTimeLine(1);
            }

            // 3. BOSS 阶段切换登记：不在这里直接落地
            //    多个敌人同时进入新阶段时，由状态机排队一个一个播放（提示 + 音效）
            if (!entity.IsBroken && entity.HasPendingBossPhase)
                _pendingPhaseEntities.Add(entity);
            
            // 4. 每回合开始时要把防御姿态清掉，并重新触发本回合的 Break 跳过判定
            entity.ClearDefendStance();
            entity.TriggerBreakSkipForRound();

            // 5. 推进 Buff / Debuff 持续回合，到期的在这里统一移除
            entity.TickBuffsAtRoundStart();

            // 6. 最后只给玩家单位回 1 点 BP
            if (entity.IsPlayer)
                entity.RecoverBP();
        }
    }

    #region 破盾

    /// <summary>
    /// 当单位被破盾或死亡时调用
    /// 规则：
    /// 1) 死亡：从当前与下一回合都剔除
    /// 2) 已行动后破盾：仅剔除下一回合
    /// 3) 未行动就破盾：同时剔除当前与下一回合
    /// </summary>
    public void KickOutFromTimeLine(BattleEntity target)
    {
        // 1. 先处理空引用，避免破防信号从空目标进来
        if (target == null)
            return;
        
        // 2. 死亡单位直接从当前回合和下一回合都移除
        if (!target.IsAlive)
        {
            _currentRound.Remove(target);
            _nextRound.Remove(target);
            _pendingNextRoundBreakSkip.Remove(target);
            return;
        }
        
        // 3. 没有 Break 的单位不需要再踢出时间轴
        if (!target.IsBroken)
            return;
        
        // 4. 当前回合还没行动就被破盾时，直接在这里补一次跳过计数
        if (_currentRound.Remove(target))
        {
            target.AddBrokenSkipTurns(1);
            target.ConsumeBrokenTurnsByTimeLine(1);
        }
        
        // 5. 下一回合里如果也有它，就先记到挂起表里，等回合切换时再消费
        if (_nextRound.Remove(target))
        {
            // 已经行动后破盾：只需要保证下一回合开场时再补跳过一次即可
            _pendingNextRoundBreakSkip[target] = 1;
        }
    }

    #endregion
    
}
