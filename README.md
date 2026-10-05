# HD2D-JRPG-Unity

一个用 Unity 复刻《歧路旅人 / Octopath Traveler》HD-2D 风格的回合制 JRPG 原型项目。
聚焦在**战斗系统架构**与**城镇/野外探索**两端的完整闭环，不依赖任何商业游戏框架，全部由 C# 自研。

> 🎬 **演示视频 / Gameplay GIF**：（这里之后贴 B 站或 YouTube 链接，以及 1~2 张战斗截图）

---

## 技术栈

| 类别 | 选型 |
|---|---|
| 引擎 | Unity 2022.3 LTS |
| 渲染管线 | URP 14 + 后处理栈 |
| 输入系统 | Unity Input System（新输入后端） |
| 资源管理 | Addressables 1.22（异步场景/资产加载） |
| 相机 | Cinemachine 3（Field / Battle / Dialogue 多 Brain 切换） |
| 时间轴 | Timeline（战斗演出、Boss 出场） |
| UI | UGUI + TextMeshPro |
| 架构风格 | 事件总线 + 命令模式 + 有限状态机 + ScriptableObject 数据驱动 |

---

## 核心系统

### ⚔️ 战斗系统（重点自研部分）

完整的回合制战斗框架，代码在 `Assets/Scripts/Battle/`，按 Core / Runtime / Event / Config 分层：

- **命令模式（Command Pattern）**：所有玩家指令抽象为 `BattleCommandHandlerBase` 的子类——普通攻击、技能、防御、道具、逃跑。新增指令类型不需要修改战斗调度器，只需要新增一个 Handler。
- **战斗状态机（FSM）**：7 个状态串起整个回合流程
  `BattleSetup → PlayerInput → TargetSelection → SelectNextEntity → PerformAction → TurnEnd → EnemyAI`
- **回合调度器** `BattleRoundScheduler`：基于速度排序的行动顺序预测条，战斗中实时显示敌方下一个行动位置（八方旅人标志性的时间轴 UI）。
- **弱点破盾系统**：每个敌人有独立的武器/法术弱点表，连续命中弱点削减 Shield Point，Break 后角色获得额外行动回合。
- **BP（Boost Point）系统**：技能 `Transfer BP` 可在队友间转移 BP，Buff 系统支持多层叠加上下文（`BuffInstance`）。
- **敌人 AI 状态机** `EnemyAIState`：根据当前血量、自身 Buff、玩家队伍状态选择攻击 / 防御 / 技能。
- **战斗演出服务** `BattleCinematicService`：攻击命中顿帧、闪白、受击 Dissolve、伤害飘字、屏幕震动、后处理 Bloom Boost，全部通过配置资产驱动。

### 🚶 野外 / 城镇探索

- **Path Action 系统**：模仿八方旅人，每个角色有自己的城镇指令——
  `Challenge（挑战）` / `Inquiry（打听）` / `Recruit（招募伙伴）` / `Steal（偷窃）` / `Shop（商店）`
  抽象基类 `InteractionBase` + `ActionBase`，新增一个 Path Action 只要继承基类。
- **随机遇敌**：`EncounterZone` 触发区域 + `PlayerEncounterTracker` 控制遇敌率。
- **队伍跟随**：野外场景中已招募队友在主角身后排队跟随（`PartyFieldController` + `FieldFollower`）。
- **昼夜切换**：Town 场景独立 Day / Night 两套烘焙光照，通过场景切换加载。

### 🔊 音频系统

自研音频框架，见 `Docs/Audio System Design.md`：

- `AudioManager` 单例 + `AudioCue` 音频事件对象
- `SoundLibrary_SO` 在 Inspector 里可视化配置 BGM / SFX / Boss / 技能 音频
- 通过 `AudioEventBridge` 把战斗事件（Break / Victory / Defeat）和音频解耦
- 自带 Editor 菜单一键初始化音频组

### 🧩 框架层（`Assets/Scripts/MFrameWork/`）

- **EventBus**：泛型事件总线 `IEvent` / `IEventReceiver`，战斗、场景、UI、音频之间零直接引用
- **Singleton**：通用单例基类

### 📦 数据驱动

全部角色 / 敌人 / 技能 / 道具 / 装备使用 ScriptableObject：

- `CharacterDefinition_SO` / `AllyDefinition_SO` / `EnemyDefinition_SO`
- `SkillData_SO` + `SkillLogic_SO`（技能逻辑可插拔，例如 `StealSkillLogic`、`TransferBPLogic`）
- `ItemDefinition_SO` / `EquipmentItem_SO` / `ConsumableItem_SO`
- 基础攻击按武器类型分开配置（剑 / 枪 / 杖 / 匕首 / 弓）

### 🖥 UI 框架

- `PanelController` 基类统一管理所有面板的打开 / 关闭 / 堆叠
- 装备界面实时对比属性变化（`EquipmentStatCompareRow`）
- 战斗 HUD：血条、弱点条、回合时间轴、伤害飘字、战斗结算

---

## 项目结构

```
Assets/
├── Scripts/
│   ├── Battle/          # 战斗系统（Core / Runtime / Event / Config 分层）
│   ├── Game/           # 游戏系统（Audio / Camera / Character / Party / Skill / Inventory...）
│   ├── UI/             # 战斗 UI / 装备 / 背包 / 菜单 / 探索面板
│   ├── MFrameWork/     # EventBus、Singleton 通用框架
│   └── Player/         # 玩家控制器
├── Game Data/          # 全部 ScriptableObject 配置（角色/技能/道具/音频库）
├── Scenes/             # Battle / Field / Town(Day&Night) / Menu / Map / Persistent
├── Prefabs/            # 角色 / 敌人 / NPC / 特效预制体
└── Arts/               # 美术资源
Docs/                   # 音频系统设计文档
```

---

## 如何运行

1. 使用 **Unity 2022.3.62 LTS (Unity China / Tuanjie)** 打开本工程
2. 打开场景 `Assets/Scenes/Menu/Menu.scene` 作为启动入口
3. 等待 Addressables 构建完成后即可 Play

---

## 致谢与资产声明

为了专注玩法与系统开发，本项目使用了以下第三方资源（**美术、音乐、音效、特效插件均非本人制作**）：

- **HD-2D 环境模型**：Asset Store 上的风格化城镇 / 野外建筑包
- **着色器与后期**：Toony Colors Pro、KinoBloom
- **特效**：Cinematic FX Runtime (CFXR)
- **水面 / 瀑布**：Dreamscape Water Shader 系列
- **音频**：自配 BGM / SFX 素材

本人负责的部分：**全部 C# 业务代码、战斗系统架构、UI 逻辑、事件系统、ScriptableObject 数据结构、音频框架、场景流转与 Addressables 配置、Inspector 工具编辑器**。

---

##  License

仅用于学习与作品集展示，不对外发布。
