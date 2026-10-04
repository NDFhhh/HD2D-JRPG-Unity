# 《CodePath Traveler》音频系统设计方案

> 版本：v1.0（设计稿）
> 目标：为游戏加入完整的音频系统，覆盖 BGM、音效（SFX）、音量设置与播放控制，且与项目现有的 MFrameWork（EventBus / Singleton）、GameModeManager、SceneLoadManager 架构无缝集成。

---

## 1. 设计目标与原则

| 原则 | 说明 |
|---|---|
| **事件驱动** | 音频系统**不侵入任何现有玩法代码**，全部通过 `EventBus` 订阅现有事件（`BattleStartedEvent`、`GameModeChangedEvent`、`EntityStatChangedEvent` 等）来触发音效 |
| **一处配置** | 所有音频资源通过 ScriptableObject（`SoundLibrary_SO`）集中索引，代码中只用 `SoundId` 枚举引用，不持有 AudioClip 引用 |
| **全局常驻** | `AudioManager` 挂在 `DontDestroyOnLoad` 的启动场景物体上，跨场景存活，保证 BGM 不因切场景中断 |
| **四路音量** | Master / BGM / SFX / UI 四条 AudioMixer 通道，独立调节并持久化到 PlayerPrefs |
| **低成本优先** | 使用 Unity 内置 AudioMixer + AudioSource 池即可满足需求，不引入第三方中间件（WWise / FMOD）；若未来项目规模扩大再考虑迁移 |

---

## 2. 总体架构

```
启动场景 (Boot)
  └── AudioManager (DontDestroyOnLoad)
        ├── MusicPlayer   —— 2×AudioSource 交叉淡化，播 BGM
        ├── SFXPlayer     —— AudioSource 池，PlayOneShot 播音效
        └── AudioEventBridge —— 订阅 EventBus，事件 → SoundId 映射

AudioMixer (CodePathMixer)
  ├── Master
  │     ├── BGM  (MusicPlayer 的 2 个 AudioSource 输出到此处)
  │     ├── SFX  (SFXPlayer 池输出到此处)
  │     └── UI   (UI 音效单独通道，可在暂停时只保留 UI 声)
  └── (持久化) AudioSettings → PlayerPrefs

SoundLibrary_SO (ScriptableObject 资产)
  └── Dictionary<SoundId, AudioCue>   ← Addressables / 直接引用加载
```

**数据流：** 玩法代码 `EventBus.Publish(事件)` → `AudioEventBridge.OnEvent()` → 查映射表得到 `SoundId` → `SFXPlayer.Play(id)` → 从 `SoundLibrary_SO` 取 `AudioCue` → 池中 AudioSource `PlayOneShot`。

---

## 3. 目录结构与资源组织

```
Assets/
└── Audio/
    ├── Mixers/
    │   └── CodePathMixer.mixer
    ├── BGM/
    │   ├── Menu/
    │   ├── Town/           ← 按场景/区域分子文件夹
    │   │   ├── Town01_Day.ogg
    │   │   └── Town01_Night.ogg
    │   └── Battle/
    │       ├── Battle_Normal_01.ogg
    │       └── Battle_Boss_01.ogg
    ├── SFX/
    │   ├── Player/         ← 移动、攻击、受伤…
    │   ├── Battle/         ← 破防、Break、结算…
    │   ├── UI/             ← 点击、确认、取消、面板开关…
    │   ├── Interaction/    ← 拾取、商店、偷取…
    │   └── Environment/    ← 环境声、脚步
    └── Voice/              ← 预留（角色语音/受击 grunt）
```

### 3.1 SoundId 枚举 + AudioCue

代码中**只用枚举**引用音效，资产在 Inspector 里配置：

```csharp
public enum E_SoundId
{
    // —— UI ——
    UI_Click, UI_Confirm, UI_Cancel, UI_PanelOpen, UI_PanelClose,
    UI_Hover, UI_Error, UI_Reward,

    // —— 玩家 / 漫游 ——
    Player_Footstep, Player_Hurt, Player_Heal,
    Player_Attack_Slash, Player_Attack_Magic, Player_Miss,
    Item_Pickup, Item_Use, Gold_Gain,

    // —— 战斗 ——
    Battle_Start, Battle_Turn_Tick, Battle_Shield_Hit,
    Battle_Weakness_Hit, Battle_Break, Battle_Enemy_Die,
    Battle_Victory, Battle_Defeat, Battle_Escape,

    // —— BGM（供 MusicPlayer 使用） ——
    BGM_Menu, BGM_Town_Day, BGM_Town_Night, BGM_Battle, BGM_Boss,
}
```

```csharp
[CreateAssetMenu(menuName = "CodePath/Audio/AudioCue")]
public class AudioCue : ScriptableObject
{
    [Serializable]
    public class ClipVariant
    {
        public AudioClip Clip;
        [Range(0f, 1f)] public float Volume = 1f;
    }

    [SerializeField] private ClipVariant[] variants;     // 多个变体随机播放
    [SerializeField] private Vector2 pitchRange = new(0.95f, 1.05f); // 随机变调防重复感
    [SerializeField, Range(0, 8)] private int maxVoices = 4;          // 同帧同类音效上限
    [SerializeField] private float cooldown = 0.03f;                  // 同一 cue 最小触发间隔

    // Play(SFXPlayer) 时随机取变体、随机 pitch、检查 voice limit
}
```

> **变体的意义：** 同一刀砍中 8 个敌人时会播 8 次"hit"音，若只有单一 clip 会非常刺耳；随机变体 + 随机 pitch + voice limit 是 JRPG 战斗音效的标配。

### 3.2 SoundLibrary_SO

```csharp
[CreateAssetMenu(menuName = "CodePath/Audio/SoundLibrary")]
public class SoundLibrary_SO : ScriptableObject
{
    [SerializeField] private List<AudioCueEntry> entries; // E_SoundId → AudioCue

    public bool TryGetCue(E_SoundId id, out AudioCue cue) { ... }
}
```

- 全局**只建一份**资产 `SoundLibrary.asset`，赋给 `AudioManager` 的序列化字段。
- 新增音效的工作流：音频文件拖入 `Audio/SFX/**` → 在 `SoundLibrary.asset` 里把枚举项指向一个新建 `AudioCue`。**零代码改动**。

### 3.3 加载策略

当前阶段音频总量不大：**直接序列化引用**即可（SoundLibrary 持有所有 AudioCue 引用）。项目已有 Addressables（`AddressableAssetsData`），若后期音频膨胀（>200MB），将 BGM 改为 Addressables 远程加载，SFX 保持本地——BGM 单文件大、切换频率低，最适合懒加载。

---

## 4. 核心运行时组件

### 4.1 AudioManager（入口单例）

```csharp
public class AudioManager : Singleton<AudioManager>
{
    [SerializeField] private SoundLibrary_SO library;
    [SerializeField] private AudioMixer mixer;

    public MusicPlayer Music { get; private set; }  // 双 AudioSource 交叉淡化
    public SFXPlayer SFX { get; private set; }      // 对象池

    protected override void Awake()
    {
        base.Awake();
        Music = gameObject.AddComponent<MusicPlayer>();
        SFX   = gameObject.AddComponent<SFXPlayer>();
        gameObject.AddComponent<AudioEventBridge>();
        AudioSettings.LoadAndApply(mixer);   // 启动时恢复音量设置
    }
}
```

> 注意：现有 `Singleton<T>` 若未处理重复创建，请确保 AudioManager 只在启动场景放置一次（参考 GameModeManager 的写法）。

### 4.2 MusicPlayer —— BGM 播放与交叉淡化

```csharp
public class MusicPlayer : MonoBehaviour
{
    private AudioSource _a, _b;          // 两个声源轮流当"当前/淡出"
    private Coroutine _fadeJob;

    public void Play(E_SoundId bgmId, float fadeDuration = 1.5f,
                     bool restartIfSame = false)
    {
        // 1. 若与当前曲相同且 !restartIfSame → 直接返回（防重复触发）
        // 2. library.TryGetCue(bgmId) 取 clip
        // 3. 启动交叉淡化协程：新 AudioSource volume 0→目标值，
        //    旧 AudioSource 目标值→0 后 Stop()
    }

    public void Stop(float fadeDuration = 1.0f);
    public void SetPaused(bool paused);   // 暂停菜单用（配合 mixer BGM lowpass 更佳）
}
```

- BGM 的 AudioSource：`loop = true`、`playOnAwake = false`、输出路由到 Mixer 的 **BGM** 组。
- 淡化时长建议 1~2 秒；战斗开始用**快切（0.5s）+ 短暂静音**制造紧张感，战斗结束回城镇用慢切（2s）。

### 4.3 SFXPlayer —— 音效池

```csharp
public class SFXPlayer : MonoBehaviour
{
    private readonly Stack<AudioSource> _pool = new();
    private readonly Dictionary<E_SoundId, float> _lastPlayTime = new();

    public void Play(E_SoundId id, float volumeScale = 1f, Vector3? worldPos = null)
    {
        // 1. cue 检查 cooldown / voice limit
        // 2. 从池中取（或新建）AudioSource，3D 模式按 worldPos 定位，2D 音效居中
        // 3. PlayOneShot(clip, volume * volumeScale)
        // 4. 延迟回收（clip.length 后归还池）
    }
}
```

- 池初始 8 个 AudioSource，上限 24；UI 音效路由 **UI** 组、其余路由 **SFX** 组（通过两套池或输出切换实现）。
- 战斗是回合制、镜头固定，**绝大多数音效用 2D 即可**；仅漫游场景的交互物（宝箱、NPC）与脚步声用 3D 空间声。

### 4.4 AudioEventBridge —— 事件→音效映射（系统核心）

这个组件订阅现有 EventBus，是**唯一知道"哪个事件配哪个声音"的地方**：

```csharp
public class AudioEventBridge : MonoBehaviour,
    IEventReceiver<GameModeChangedEvent>,
    IEventReceiver<SceneLoadCompleteEvent>,
    IEventReceiver<BattleStartedEvent>,
    IEventReceiver<BattleEndedEvent>,
    IEventReceiver<EntityStatChangedEvent>,   // 受伤/治疗/死亡判定
    IEventReceiver<EntityWeaknessChangedEvent>,
    IEventReceiver<EntityShieldChangedEvent>,
    IEventReceiver<EntityRecoverFromBreakEvent>,
    IEventReceiver<SkillNameDisplayEvent>,    // 技能咏唱/施放声
    IEventReceiver<BattleNotificationEvent>,
    IEventReceiver<PanelRequestEvent>,        // 面板开
    IEventReceiver<KillCinematicRequestedEvent>,
    IEventReceiver<BattleResultConfirmedEvent>
{
    // OnEvent 里按事件字段分派：
    // 例：EntityStatChangedEvent + StatType == CurrentHP + NewValue < 旧值 → Player_Hurt
    //     CurrentHP == 0 → Battle_Enemy_Die（由 Target 阵营区分敌我变体）
}
```

---

## 5. 音效需求清单（事件 → 音效映射表）

### 5.1 全局 / UI

| 触发时机 | 现有挂载点 | 建议音效（SoundId） |
|---|---|---|
| 按钮/选项点击 | UI 各 Button（可加统一 `UIButtonSfx` 组件或监听 `IEventReceiver`） | `UI_Click`（短促、中高频） |
| 确认 / 取消 | `ChooseCharacterButtonOnClickEvent` 等 | `UI_Confirm` / `UI_Cancel` |
| 面板打开/关闭 | `PanelRequestEvent` | `UI_PanelOpen` / `UI_PanelClose` |
| 操作无效（BP 不足等） | `BattleNotificationEvent(IsSuccess=false)` | `UI_Error`（低频"咚"） |
| 获得奖励/金钱 | `BattleEndedEvent`、商店购买 | `UI_Reward`、`Gold_Gain` |

### 5.2 漫游场景（Town01_Day / Town01_Night）

| 触发时机 | 挂载点 | 建议音效 |
|---|---|---|
| 行走脚步 | `PlayerController` 移动状态（按步频节拍器触发） | `Player_Footstep`（4~6 个变体随机） |
| 靠近/触发遭遇 | `EncounterZone` / `PlayerEncounterTracker` | 遭遇警告短音效（进入战斗前的"!"） |
| 交互提示弹出 | `InteractionUIController` | `UI_Hover` |
| 探索行动（打听/偷取/挑战/招募） | 各 PathAction（成功/失败两套） | `Item_Pickup` 变体 / `UI_Error` |
| 商店购买/出售 | `ShopPanelController` | `Gold_Gain`（金币声） |
| 拾取物品 | Inventory 相关事件 | `Item_Pickup` |
| 使用道具 | `ItemCommandHandler`（战斗内） | `Item_Use` |

### 5.3 战斗场景

| 触发时机 | 现有事件 | 建议音效 |
|---|---|---|
| 战斗开始 | `BattleStartedEvent` | `Battle_Start`（BGM 同时切战斗曲） |
| 回合行动者切换 | `ActiveEntityChangedEvent` | `Battle_Turn_Tick`（轻"嗒"声，可选） |
| 普攻挥砍/命中 | `AttackSkillExecutionEngine` 命中结算（可发新事件 `DamageDealtEvent`，见 §9 建议） | `Player_Attack_Slash` + 命中声（弱点击中用 `Battle_Weakness_Hit`） |
| 攻击未命中 | 命中判定 miss 分支 | `Player_Miss`（挥空风声） |
| 护盾削减 | `EntityShieldChangedEvent` | `Battle_Shield_Hit`（钝击金属声） |
| 破防 Break | `EntityRecoverFromBreakEvent` 之前的 break 时刻 / `BreakCinematicRequestedEvent` | `Battle_Break`（玻璃碎裂+低频冲击） |
| 弱点揭露 | `EntityWeaknessChangedEvent` | `Battle_Weakness_Hit`（清脆提示音） |
| 技能施放 | `SkillNameDisplayEvent` | `Player_Attack_Magic`（咏唱起手声） |
| 单位死亡 | `KillCinematicRequestedEvent` | `Battle_Enemy_Die`（ dissolution 配合消散音） |
| 战斗胜利 | `BattleEndedEvent(IsWin=true)` | `Battle_Victory`（号角短乐句 + BGM 切胜利 jingle 再回城镇曲） |
| 战斗失败 | `BattleEndedEvent(IsWin=false)` | `Battle_Defeat` |
| 逃跑成功 | `EscapeCommandHandler` | `Battle_Escape` |
| 受伤 | `EntityStatChangedEvent(CurrentHP 下降)` | `Player_Hurt` / 敌方受击 grunt |

### 5.4 BGM 曲目规划

| 场景/状态 | 曲目（SoundId） | 风格参考 |
|---|---|---|
| 主菜单/选人 | `BGM_Menu` | 舒缓钢琴主题旋律 |
| Town01 白天 | `BGM_Town_Day` | 明快小镇主题（木管+弦乐拨奏） |
| Town01 夜晚 | `BGM_Town_Night` | 同主题变奏（弱音、慢速、八音盒音色） |
| 普通战斗 | `BGM_Battle` | 紧迫弦乐+打击乐循环 |
| Boss 战（预留） | `BGM_Boss` | 更强节奏、加入合唱 |
| 胜利结算 | `Battle_Victory`（一次性 jingle，不循环） | 短乐句 3~5 秒 |

---

## 6. BGM 播放控制与切换逻辑

### 6.1 切换规则表（状态驱动）

由 `GameModeChangedEvent` + `SceneLoadCompleteEvent` 驱动，**一处决策**：

```
MusicDirector 逻辑（并入 AudioEventBridge 或独立 MusicDirector 组件）：

SceneLoadCompleteEvent:
    按 scene name → BGM_Town_Day / BGM_Town_Night / BGM_Menu
    Music.Play(对应曲, fade 2s)

GameModeChangedEvent(Battle):
    Music.Play(BGM_Battle, fade 0.5s)          // 快切，紧张感

BattleEndedEvent(IsWin):
    PlayOneShot(Battle_Victory jingle)
    → jingle 播完后（或战斗结果确认时）随场景卸载，
      SceneLoadCompleteEvent 自然触发回城镇曲

GameModeChangedEvent(Pause):
    Music.SetPaused(true) 或 mixer BGM 加 lowpass~800Hz（推荐后者，
    音乐继续播但"隔着玻璃"，解除时恢复）
```

### 6.2 关键细节

- **防重复触发：** 同曲重复 `Play()` 直接忽略（`restartIfSame=false`），避免事件风暴导致 BGM 重头播。
- **切场景配合：** 项目已有 `FadeController` 做画面淡入淡出；BGM 交叉淡化与之并行进行（不等黑屏），体验更流畅。若遇加载卡顿，可让 SceneLoadManager 在开始加载时把 BGM 淡到 30% 音量，加载完成恢复。
- **Boss/精英扩展：** 在 `BattleConfig_SO` 中加一个 `E_SoundId OverrideBGM` 字段，普通遭遇为 None 用默认战斗曲，Boss 指定专属曲——数据驱动，无需改播放器代码。

---

## 7. 音量调节与设置系统

### 7.1 AudioMixer 分组

```
Master (Exposed: MasterVolume)
 ├── BGM  (Exposed: BgmVolume)
 ├── SFX  (Exposed: SfxVolume)
 └── UI   (Exposed: UiVolume)
```

> 经验值：Exposed 参数映射用对数曲线（`Mathf.Log10(v) * 20`，v∈[0.0001,1]），滑条线性但听感线性。

### 7.2 AudioSettings（持久化）

```csharp
public static class AudioSettings
{
    private const string KEY_M = "Audio_Master"; // 同理 BGM/SFX/UI

    public static void SetVolume(string exposedParam, float linear01)
        => mixer.SetFloat(exposedParam, Mathf.Log10(Mathf.Max(linear01, 0.0001f)) * 20f);

    public static void Save(string exposedParam, float linear01) => PlayerPrefs.SetFloat(...);
    public static void LoadAndApply(AudioMixer mixer) { /* 启动时读 PlayerPrefs 并应用 */ }
}
```

- 游戏启动时在 `AudioManager.Awake` 里 `LoadAndApply`，主菜单音量立即正确。
- 滑条 `onValueChanged` → 即时 `SetVolume`（试听体验好）+ `onPointerUp` 时 Save。

### 7.3 设置面板 UI

在现有 UI 体系（`UI/Field Panel` / PanelController）中新增：

```
UI/Settings/AudioSettingsPanelController.cs
  └── 4 个 Slider（主音量/BGM/音效/UI）+ 静音开关
```

- 面板打开时滑条初始化为 PlayerPrefs 值；拖动时即时生效；
- 暂停菜单（Pause 模式）与主菜单均可打开此面板；
- 由于 UI 音效走独立 UI 通道，玩家把 SFX 拉到 0 仍能听到设置面板的点击反馈。

### 7.4 可选进阶：Ducking

对话/重要通知出现时压低 BGM：在 Mixer 的 BGM 组上加一个 Ducking（Send 到 SFX 组的 Sidechain / 或简单用协程把 BGM 音量临时降到 40%），通知关闭后 0.5s 恢复。第一版可不做，留接口。

---

## 8. 音频导入设置规范

| 类型 | Load Type | Compression | 说明 |
|---|---|---|---|
| BGM（1~3 分钟） | **Streaming** | Vorbis, Quality ~60% | 省内存，解码即时 |
| 短 SFX（<1s） | **Decompress on Load** | PCM/ADPCM | 零延迟，战斗手感关键 |
| 长 SFX（>1s，如胜利 jingle） | Compressed In Memory | ADPCM | 折中 |

统一设置采样率 44100Hz；Mono 用于 3D 音效，Stereo 用于 BGM/UI。建议写一个 `AudioAssetPostprocessor`（`OnPreprocessAudio` 按文件夹路径自动套规则），避免美术拖入时配置错。

---

## 9. 对现有代码的最小改动建议

音频系统本身零侵入，但为了让音效触发更精准，建议**补充 3 个轻量事件**（在现有 Battle/Event 或 Game/Event 下新增 struct，实现 IEvent）：

1. `DamageDealtEvent { BattleEntity Attacker, Target; int Amount; bool IsWeakness; bool IsMiss; }` —— 在 `AttackSkillExecutionEngine` 命中结算处 Publish。**这是收益最大的一条**：命中/未命中/弱点击中三类音效全靠它，比从 `EntityStatChangedEvent` 反推（拿不到旧值、区分不了来源）干净得多。
2. `FootstepEvent { Vector3 Position }` —— 在 `PlayerController` 按步频节拍发布（移动中每 0.4~0.5s 一次，可用 SimpleAnimation 步点回调或计时器）。
3. `UiClickEvent { E_UiClickType Type }`（或直接给 Button 挂 `UIButtonSfx` 组件批量处理）—— UI 点击目前没有统一事件，用组件方式更省事：一个小组件 `OnClick → AudioManager.Instance.SFX.Play(UI_Click)`，可做成 Prefab/MenuItem 批量添加。

---

## 10. 分阶段实施计划

| 阶段 | 内容 | 产出 |
|---|---|---|
| **P1 骨架**（0.5~1 天） | AudioMixer、目录、AudioManager + MusicPlayer + SFXPlayer、SoundLibrary_SO、AudioEventBridge 空实现、音量持久化 | 跑通"发布事件→出声"链路 |
| **P2 BGM**（0.5 天） | SceneLoadComplete/GameModeChanged 接入 BGM 切换 + 交叉淡化；接入占位 BGM | 漫游/战斗/菜单 BGM 自动切换 |
| **P3 战斗音效**（1 天） | 新增 `DamageDealtEvent`，接全部战斗事件映射 | 战斗打击感成型 |
| **P4 UI 与漫游音效**（1 天） | UIButtonSfx、面板开关、脚步、交互、商店 | 全场景音效覆盖 |
| **P5 设置面板**（0.5 天） | AudioSettingsPanel 4 滑条 + 持久化 | 玩家可调音量 |
| **P6 打磨**（持续） | Ducking、Boss BGM 覆盖、AudioPostprocessor、混音统一响度（-16 LUFS BGM / -12 LUFS SFX 目标） | 商业品质 |

---

## 11. 风险与注意事项

- **EventBus 遍历中订阅/退订：** 当前 `EventBus.Publish` 遍历时直接 `RemoveAt`，AudioEventBridge 若在回调中订阅新事件可能出问题——音频侧只做"播声音"，不在 OnEvent 里做订阅操作即可规避。
- **响度一致性：** 不同来源的素材响度差异会很大，建议每条 AudioCue 的 Volume 字段在混音阶段统一校准；有条件的话在 DAW 里先 Normalize。
- **移动端/长按连点：** UI 按钮 `UI_Click` 设 `cooldown ≥ 0.05s`，防止连续点击爆音。
- **DontDestroyOnLoad 与场景重载：** AudioManager 在启动场景创建；若项目尚无 Boot 场景，可挂在首个加载场景并靠 Singleton 防重，或加入 SceneLoadManager 的初始化流程。
