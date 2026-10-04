# 音频系统接入操作指南（Unity 编辑器侧）

代码已全部就位（`Assets/Scripts/Game/Audio/`），以下是让声音真正响起来的编辑器操作步骤。

## 1. 创建资源（一次性）

1. 菜单栏 `CodePath/Audio/1. Create SoundLibrary Asset`  
   → 在 `Assets/Game Data/Audio/` 生成 `SoundLibrary.asset`。
2. 在首个加载的场景（启动场景）执行 `CodePath/Audio/2. Add AudioManager To Scene`  
   → 场景中生成 `AudioManager` 物体（运行时 DontDestroyOnLoad，全游戏只需一份）。  
   建议拖成 Prefab 备用。

## 2. 放入音频文件

按设计文档的目录结构放（可自行调整）：

```
Assets/Audio/
├── BGM/Menu/  BGM/Town/  BGM/Battle/
└── SFX/Player/  SFX/Battle/  SFX/UI/  SFX/Interaction/
```

导入设置建议：

- **BGM**：Load Type = Streaming，Compression = Vorbis1
- **短音效（<1s）**：Load Type = Decompress on Load，Compression = PCM/ADPCM
- **长音效**：Compressed In Memory + ADPCM

## 3. 配置 SoundLibrary

1. 右键 `Create > CodePath > Audio > AudioCue`，为每个音效建一个 Cue 资产：
   - 把 AudioClip 拖进 Variants（脚步声等高频音效建议放 4~6 个变体）
   - 按需调 Pitch Range（脚步 0.9~1.1）、Max Voices、Cooldown
2. 打开 `SoundLibrary.asset`，把每个 `E_SoundId` 枚举项指到对应 Cue。  
   **不需要的音效留空即可，代码会自动跳过。**
3. BGM 曲目（`BGM_Menu / BGM_Town_Day / BGM_Town_Night / BGM_Battle`）同样各建一个 Cue，只放 1 个 clip。

## 4.（可选但推荐）创建 AudioMixer

不创建也能用（自动降级为 AudioSource 音量控制），但建议配置以获得完整四路音量：

1. `Create > Audio > Audio Mixer`，命名 `CodePathMixer`
2. 在 Mixer 里建 3 个 Group：`BGM`、`SFX`、`UI`
3. 选中 Master，在 Inspector 右上角下拉 `Add exposed Volume`，分别对 4 个节点暴露音量参数，  
   **命名必须是**：`MasterVolume`、`BgmVolume`、`SfxVolume`、`UiVolume`
4. 把 `CodePathMixer` 资产拖到 `AudioManager` 的 `Mixer` 字段。

## 5. UI 音效

给需要声音的 Button 预制体挂 `UIButtonSfx` 组件（可配 Click / Hover 两种声音）。  
批量操作：选中一批 Button 物体后用组件菜单 `Add Component > UIButtonSfx`。

## 6. 音量设置面板

新建一个 Settings 面板 UI（4 个 Slider，范围 0~1，可选 1 个 Toggle），挂  
`AudioSettingsPanelController` 并把滑条拖进对应字段。可挂在暂停菜单或主菜单下。

## 7. 验证

- 运行启动场景 → 进入 Town01_Day → 应听到白天 BGM
- 触发遭遇进战斗 → BGM 快切战斗曲 + `Battle_Start` 音效
- 攻击命中 / 弱点 / 破防 / 击杀 → 对应音效
- 打开任一面板 → `UI_PanelOpen`
- 调滑条 → 音量立即变化，重启游戏后保持

## 新增场景 / 新增音效时

- 新场景 BGM：在 `AudioEventBridge.ResolveSceneBgm()` 里加一条场景名匹配
- 新音效：`E_SoundId` 加枚举 → 建 Cue → SoundLibrary 里登记 → （若有新事件）在 `AudioEventBridge` 加订阅
