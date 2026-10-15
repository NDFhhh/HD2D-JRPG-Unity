# 任务系统设计方案（Quest System Design）

> 状态：已实施 · 2026-10-08
> 日期：2026-10-08
> 适用范围：CodePath Traveler（团结引擎 1.10.0 / Unity 2022.3.62t12）
>
> 实施说明：本方案已落地为 `Assets/Scripts/Game/Quest/` 下的完整代码。详见文末「十一、实施记录」。

---

## 一、概述

### 1.1 定位

任务系统在整体架构中属于**逻辑层**：

| 系统 | 层级 | 职责 |
|---|---|---|
| 对话系统 | 表现层 | 播放对话内容、分支、立绘 |
| **任务系统** | **逻辑层** | 目标接取、进度追踪、完成判定、奖励发放 |
| 剧情系统 | 编排层 | 章节调度，串起对话与任务 |

任务系统**承接**对话系统产出的全局变量（Flag）与事件，同时**为剧情系统提供**任务完成/阶段推进的信号。三者通过事件总线解耦，互不直接引用。

### 1.2 设计原则

1. **数据驱动**：任务内容全部用 ScriptableObject 资产配置，运行时代码不含具体任务逻辑（延续 `ItemDefinition_SO`、`Speaker_SO` 的既有模式）。
2. **事件解耦**：进度监听不轮询，而是订阅 `FlagChangedEvent`、`DialogueEndedEvent`、`DialogueCustomEvent` 等现有事件，与对话系统零直接引用。
3. **可存档**：运行时状态（进度、阶段）与 `GameFlagManager` 一样实现序列化接口，接存档系统即可。
4. **增量叠加**：整个系统是对现有代码的增量补充，不修改任何已有文件。

---

## 二、数据结构设计

### 2.1 任务状态枚举

```csharp
public enum E_QuestState
{
    Locked,      // 未解锁（前置未满足）
    Available,   // 可接取（未接）
    Active,      // 进行中
    Completed,   // 已完成待交付
    TurnedIn,    // 已交付/结算完毕（终态）
    Failed       // 失败（可选，用于限时/剧情失败）
}
```

### 2.2 任务资产（静态定义）

```csharp
[CreateAssetMenu(menuName = "CodePath/Quest/Quest Asset")]
public class QuestAsset_SO : ScriptableObject
{
    public string QuestId;                 // 全局唯一 ID，存档/跨资产引用的键
    public string Title;                   // 任务名
    [TextArea] public string Description;  // 描述

    public List<FlagCondition> UnlockConditions = new();   // 解锁条件（复用对话系统 FlagCondition）
    public List<FlagCondition> AcceptConditions = new();   // 接取条件
    public List<QuestObjective> Objectives = new();        // 目标列表

    public List<RewardEntry> Rewards = new();              // 奖励（物品/金钱/经验）
    public List<FlagOperation> OnCompleteOperations = new(); // 完成时写 Flag（触发后续剧情）

    public string CompleteDialogueId;       // 完成时触发哪段对话（可选）
    public bool AutoDeliver;                // 目标达成后是否自动交付
}
```

### 2.3 单个目标（进度最小单元）

```csharp
[Serializable]
public class QuestObjective
{
    public string ObjectiveId;             // 目标 ID
    [TextArea] public string Description;  // 目标描述（"击杀 3 只魔物"）

    public E_ObjectiveType Type;           // 目标类型
    public string TargetKey;               // 目标参数：Flag 键 / 物品 ID / 敌人 ID / 事件 Key
    public int RequiredCount = 1;          // 需要达到的数量

    public bool IsOptional;                // 可选目标（不影响任务完成判定）
    public bool Hidden;                    // 隐藏目标（达成前不显示）
}

public enum E_ObjectiveType
{
    FlagReach,          // 全局变量达到某值（最通用）
    ItemCollect,        // 收集物品 N 个
    ItemConsume,        // 消耗物品（交付）
    EnemyKill,          // 击杀特定敌人 N 个
    DialoguePlay,       // 播放某段对话（通过 DialogueEndedEvent 判定）
    CustomEvent,        // 监听自定义事件 Key（与 DialogueCustomEvent 同源）
    SceneEnter,         // 进入某场景/区域
}
```

### 2.4 运行时状态（动态快照，进存档）

```csharp
[Serializable]
public class QuestRuntimeState
{
    public string QuestId;
    public E_QuestState State = E_QuestState.Locked;
    public List<ObjectiveProgress> Progress;   // 每个目标的当前进度
}

[Serializable]
public class ObjectiveProgress
{
    public string ObjectiveId;
    public int CurrentCount;
    public bool IsDone;    // 是否已达成（缓存判定结果）
}
```

### 2.5 奖励条目

```csharp
[Serializable]
public class RewardEntry
{
    public E_RewardType Type;   // Item / Gold / Exp / Flag
    public string Id;           // 物品 ID 或 Flag 键
    public int Amount;
}
```

> **关键决策**：`QuestAsset_SO`（静态定义）与 `QuestRuntimeState`（运行时快照）**分离**。定义永远只读、可复用、可热更；状态才进存档。这样"同一个任务被多个存档"不会互相污染。

---

## 三、状态流转

```
Locked ──①解锁条件满足──▶ Available ──②接取──▶ Active
                                                   │
                          ┌────────────────────────┤
                          │ ⑤限时/剧情失败          │ ③目标全部达成
                          ▼                        ▼
                       Failed                  Completed ──④交付/结算──▶ TurnedIn(终态)
```

| 序号 | 流转 | 触发条件 |
|---|---|---|
| ① | Locked → Available | `UnlockConditions` 全部满足 |
| ② | Available → Active | 玩家接取（`TryAccept`） |
| ③ | Active → Completed | 所有 `IsOptional == false` 的目标达成 |
| ④ | Completed → TurnedIn | 玩家交付或 `AutoDeliver` 自动结算 |
| ⑤ | Active → Failed | 限时/剧情失败（可选扩展） |

- **实线** = 主动流转（玩家动作驱动）
- **虚线** = 条件触发（系统判定驱动）
- `Failed` 为可选扩展态，MVP 阶段可先不实现。

---

## 四、关键模块与接口

### 4.1 QuestManager（运行时管理器，单例中枢）

```csharp
public class QuestManager : Singleton<QuestManager>
{
    private Dictionary<string, QuestAsset_SO> _definitions;      // 静态定义注册表
    private Dictionary<string, QuestRuntimeState> _runtimeStates; // 运行时状态

    // ── 对外核心接口 ──
    public void RegisterQuest(QuestAsset_SO def);    // 启动时注册
    public bool TryAccept(string questId);           // Available → Active
    public bool TryComplete(string questId);         // Active → Completed
    public bool TryDeliver(string questId);          // Completed → TurnedIn + 发奖励
    public E_QuestState GetState(string questId);
    public IReadOnlyList<QuestRuntimeState> GetActiveQuests();
    public void NotifyProgress(string objectiveId, int delta);  // 进度推进入口

    // ── 存档接口（与 GameFlagManager 对称）──
    public List<QuestRuntimeState> ExportState();
    public void ImportState(List<QuestRuntimeState> data);
}
```

**职责边界**：任务状态机所有流转的**唯一判定中枢**。外部只能通过 `NotifyProgress` 推进进度，"目标是否达成 → 任务是否完成"的判定全部收敛在 `QuestManager` 内，避免状态散落各处导致不一致。

### 4.2 QuestProgressTracker（进度追踪器）

任务系统与外部世界的"神经末梢"，**订阅现有事件**，把外部行为翻译成任务进度：

```csharp
public class QuestProgressTracker : IEventReceiver<FlagChangedEvent>,
                                    IEventReceiver<DialogueEndedEvent>,
                                    IEventReceiver<DialogueCustomEvent>
{
    public void OnEvent(FlagChangedEvent evt)
    {
        // 遍历 Active 任务，FlagReach 目标用 TargetKey 匹配 evt.Key，命中则推进
    }
    public void OnEvent(DialogueEndedEvent evt)
    {
        // DialoguePlay 目标的 TargetKey == evt.Dialogue 的 ID
    }
    public void OnEvent(DialogueCustomEvent evt)
    {
        // CustomEvent 目标的 TargetKey == evt.Key（接上 DialogueNode.OnEnterEventKey）
    }
}
```

> 追踪器**从不直接改任务状态**，只调 `QuestManager.NotifyProgress`，把判定权交回管理器。

### 4.3 QuestPanelController（任务 UI，表现层）

- 任务日志列表：Active 任务 + 目标 + 进度（`CurrentCount / RequiredCount`）
- 接取/交付面板：复用现有 `PanelController` 体系
- 只读 `QuestManager.GetActiveQuests()`，不写任何状态

### 4.4 QuestAction（任务动作接线，与 TalkAction 并列）

```csharp
public class QuestAction : ActionBase
{
    [SerializeField] private string questId;

    public override bool CanExecute(AllyDefinition_SO interactor)
    {
        // 根据当前状态决定显示"接取"/"交付"/"查看"指令
    }
    public override void TriggerAction(AllyDefinition_SO interactor)
    {
        switch (QuestManager.Instance.GetState(questId))
        {
            case E_QuestState.Available: QuestManager.Instance.TryAccept(questId); break;
            case E_QuestState.Completed: QuestManager.Instance.TryDeliver(questId); break;
        }
    }
}
```

---

## 五、进度跟踪：核心事件流

```
外部行为                    事件                      进度追踪器                任务状态
─────────                 ──────                   ────────────              ────────
玩家对话选择写 Flag        → FlagChangedEvent     → 匹配 FlagReach 目标     → 进度 +1
一段对话播放完毕           → DialogueEndedEvent   → 匹配 DialoguePlay 目标 → 进度 +1
对话节点触发自定义事件     → DialogueCustomEvent  → 匹配 CustomEvent 目标  → 进度 +1
击杀敌人（战斗系统事件）   → EnemyKilledEvent     → 匹配 EnemyKill 目标    → 进度 +1
                                                          ↓ 目标达成
                                                          ↓ 所有必选目标达成
                                                          ↓ QuestManager 判定 → Completed
                                                          ↓ 广播 QuestCompletedEvent
                                                          ↓（剧情系统监听，推进剧情）
```

---

## 六、完成与验收

验收分三层，不要混为一谈：

| 层级 | 判定逻辑 | 位置 |
|---|---|---|
| 目标达成 | `ObjectiveProgress.CurrentCount >= RequiredCount` | `ObjectiveProgress` |
| 任务完成 | 所有 `IsOptional == false` 的目标均达成 | `QuestManager` 内部 |
| 交付结算 | 发奖励 + 写 Flag + 触发对话 + 置 `TurnedIn` | `QuestManager.TryDeliver` |

`AutoDeliver` 开关决定流程差异：

- `true`：目标达成即自动结算（"杀怪即完成"类任务，无 NPC 交付）
- `false`：需玩家回 NPC 处 `TryDeliver`（"回村交任务"，有仪式感，贴合《歧路旅人》式任务结构）

---

## 七、与现有系统的衔接点（已预留，零改动）

| 现有资产 | 任务系统用法 |
|---|---|
| `FlagCondition` / `FlagOperation` | **直接复用**——任务前置条件、完成副作用与对话节点同一套 |
| `GameFlagManager` + `FlagChangedEvent` | 计数型目标（"询问 N 个村民"）的主要监听源 |
| `DialogueCustomEvent`（`DialogueNode.OnEnterEventKey`） | 对话节点直接触发任务开启/推进 |
| `DialogueEndedEvent` | "与某人交谈过"类任务的判定 |
| `ActionBase` 交互体系 | `QuestAction` 与 `TalkAction` 并列，交互菜单自然扩展 |
| `AllyDefinition_SO` / `E_PlayerJob` | 职业限定任务（如 `E_PlayerJob.Thief` 专属），接取条件判 `interactor.Job` |

---

## 八、事件定义（新增）

```csharp
public struct QuestStartedEvent : IEvent     { public readonly string QuestId; }
public struct QuestProgressedEvent : IEvent  { public readonly string QuestId; public readonly string ObjectiveId; public readonly int Current; public readonly int Required; }
public struct QuestCompletedEvent : IEvent   { public readonly string QuestId; }
public struct QuestDeliveredEvent : IEvent   { public readonly string QuestId; }
```

- `QuestCompletedEvent` 是剧情系统"推进剧情"的核心信号。
- 各事件携带 `QuestId`，供 UI 刷新、剧情编排、音效反馈等订阅方按需消费。

---

## 九、实施顺序（参考）

1. **数据层**：`QuestAsset_SO`、`QuestObjective`、`E_ObjectiveType`、运行时状态结构 + 条件/奖励复用。
2. **运行时**：`QuestManager`（状态机 + 存档接口）+ 任务事件定义。
3. **追踪器**：`QuestProgressTracker` 订阅现有事件，打通进度流。
4. **接线**：`QuestAction` 挂 NPC，先跑通"接取 → 杀怪 → 交付"最小闭环。
5. **UI**：任务日志面板（后期，可先 Console 调试）。

---

## 十、总结

核心是**三层分离**——`QuestAsset_SO`（静态定义）/ `QuestRuntimeState`（动态快照）/ `QuestManager`（唯一状态判定中枢），加上**事件驱动的进度追踪**。任务系统天然复用对话系统的 Flag、条件、事件设施，并为剧情系统提供 `QuestCompletedEvent` 作为推进信号。全程增量叠加，不改动任何现有代码。

---

## 十一、实施记录（2026-10-08）

### 11.1 新增文件

| 文件 | 职责 |
|---|---|
| `Assets/Scripts/Game/Quest/QuestAsset_SO.cs` | `E_QuestState`、`E_ObjectiveType`、`E_RewardType`、`QuestAsset_SO`、`QuestObjective`、`RewardEntry` |
| `Assets/Scripts/Game/Quest/QuestRuntimeState.cs` | `QuestRuntimeState`、`ObjectiveProgress`（运行时快照，进存档） |
| `Assets/Scripts/Game/Quest/QuestEvents.cs` | `QuestStartedEvent` / `QuestProgressedEvent` / `QuestCompletedEvent` / `QuestDeliveredEvent` |
| `Assets/Scripts/Game/Quest/InventoryEvents.cs` | `ItemAddedEvent` / `ItemRemovedEvent`（补齐物品事件源） |
| `Assets/Scripts/Game/Quest/QuestManager.cs` | 任务状态机中枢（单例），含存档接口与奖励发放 |
| `Assets/Scripts/Game/Quest/QuestProgressTracker.cs` | 进度追踪器，订阅外部事件翻译为进度 |
| `Assets/Scripts/Game/Quest/QuestAction.cs` | 任务交互动作，挂 NPC，与 TalkAction 并列 |

### 11.2 对现有文件的增量改动（仅 1 处）

| 文件 | 改动 |
|---|---|
| `Assets/Scripts/Game/Inventory/InventoryManager.cs` | `AddItem` / `RemoveItem` 末尾各补一行 `EventBus.Publish`，广播 `ItemAddedEvent` / `ItemRemovedEvent`（让 ItemCollect / ItemConsume 目标可用） |

### 11.3 设计稿落地时的关键调整（相对原始方案）

1. **`RewardEntry` 物品奖励改用直接引用**：`ItemDefinition_SO` 没有唯一字符串 ID（只有 `ItemName`），故 `RewardEntry.Item` 改为直接引用资产，而非字符串 Id。
2. **`ObjectiveProgress.IsDone` 改为缓存字段**：不再用 `CurrentCount >= RequiredCount` 表达式（运行态拿不到静态定义的 RequiredCount），改由 `QuestManager` 在推进进度时显式维护，消除脏依赖。
3. **`EnemyKill` 目标暂降级为"胜利 N 场"语义**：战斗系统目前只广播 `BattleEndedEvent`（整场胜负），未广播逐只击杀事件。故 `EnemyKill` 目标按"胜利场次 +1"推进；待战斗系统补 `EnemyKilledEvent` 后再升级为精确逐只击杀。
4. **`ItemCollect` 用"加入后总量"绝对值判断**：通过 `ItemAddedEvent.NewTotal` 调 `SetProgress`，避免"收集 3 个又用掉 1 个"的进度回退歧义。
5. **进度追踪器随 `QuestManager.EnsureInstance` 自动挂载**：与 `DialogueManager.EnsureInstance` 同模式，无需手动挂场景。

### 11.4 使用方式

```csharp
// 1. 启动时注册任务定义（在任意启动脚本里）
QuestManager.EnsureInstance().RegisterQuest(myQuestAsset);

// 2. 接取 / 交付（通常由 QuestAction 挂 NPC 自动处理）
QuestManager.Instance.TryAccept("quest_001");
QuestManager.Instance.TryDeliver("quest_001");

// 3. 查状态 / 取进行中任务（供 UI）
E_QuestState s = QuestManager.Instance.GetState("quest_001");
IReadOnlyList<QuestRuntimeState> active = QuestManager.Instance.GetActiveQuests();
```

### 11.5 待办（后续可选）

- 战斗系统补 `EnemyKilledEvent`，把 `EnemyKill` 升级为精确逐只击杀。
- 经验奖励（`E_RewardType.Exp`）接入角色成长系统。
- 任务日志 UI 面板（`QuestPanelController`，可先 Console 调试）。
- `Failed` 状态与限时/放弃任务的完整实现。
