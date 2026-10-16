# 旧游戏场景清理记录

> 执行日期：2026-10-16　｜　起因：项目转向 Built-in 管线 + 新场景 `Demo_SanXingDui`，旧的 Field / Battle / Town 场景不再需要。

## 结论

`Assets/Scenes/Battle/`、`Assets/Scenes/Field/`、`Assets/Scenes/Town/` 三个目录整体删除，
**171 个文件（75 个资产 + 93 个 .meta + 3 个目录 .meta），66.5 MB**，全部**移入 Windows 回收站**（可恢复）。

保留的场景：`Demo_SanXingDui`、`Map`、`Menu`、`Persistent`。

## 删除范围

| 目录 | 文件 | 体积 | 内容 |
|---|---|---|---|
| `Assets/Scenes/Battle/` | 42 | 7.1 MB | `Battle01`/`Battle02`/`Battle03` 场景 + 光照贴图/反射探针 + `Battle01/Battle.asset`(1.96MB) + `Battle02/Terrains/New Terrain.asset`(2.2MB) + 3 个 BoostVFX Profile |
| `Assets/Scenes/Field/` | 20 | 4.5 MB | `Field01`/`Field02`/`Field03` 场景 + 光照数据 + `Field02/Field02.asset`(1.96MB) |
| `Assets/Scenes/Town/` | 106 | 56 MB | `Town01_Day`/`Town01_Night` 场景 + 12 张烘焙贴图 + `Effect/`（Water/Waterfall 着色器、VFX prefab、`T_Waterfall_Directional_Noise_N.png` 19MB 等） |

## 删除前的引用扫描（关键）

对三个目录内全部 93 个资产做**全工程反向引用**扫描，来自目录**外部**的引用只有三类：

| 引用方 | 目标 | 处理 |
|---|---|---|
| `Assets/AddressableAssetsData/AssetGroups/Scenes.asset` | 7 条场景地址条目 | 移除条目 |
| `Assets/Scenes/Persistent/Persistent.unity`（**保留的场景**） | `Town01_Day` × 2 处 | 改指 `Demo_SanXingDui` |
| `Assets/Prefabs/VFX/{circles_blue,star_yellow,fire_red}.prefab` | `Battle01/BoostVFX{1,2,3} Profile.asset` | 摘掉宿主节点 |

> 三个目录内**没有任何资产**被目录外的美术/配置引用 —— 这是可以整体删除的前提。

## 引用修复（`Tools/AssetAudit/fix_scene_refs.py`，干跑/`--apply`）

### 1. Addressables `Scenes` 组

| 动作 | 条目 |
|---|---|
| 删除 | `Field01`、`Field02`、`Field03`、`Battle01`、`Battle02`、`Battle03`、`Town01_Day` |
| 保留 | `Map`、`Menu`、`Persistent` |
| **新增** | `Demo_SanXingDui`（guid `7a661ea4d53d17d499cb6b5b359a5e86`） |

> `Town01_Night` 本来就不在 Addressables 组里（它当时已经零引用）。
> 新增 `Demo_SanXingDui` 是**必须**的：`SceneLoadManager` 用 `Addressables.LoadSceneAsync(AssetReference)` 加载，
> 场景不在 Addressables 组里会报 AssetReference 无效。

### 2. `Persistent.unity` 的 `SceneLoadManager`

`activeScene` 与 `_startupGameplayScene` 原本都指向 `Town01_Day`（guid `cda5df05…`，共 2 处），
现改为 `Demo_SanXingDui`（guid `7a661ea4…`）。

- `activeScene`：`Awake` 里 `loadMenuOnStartup ? menuScene : activeScene`；运行中会被 `LoadFlow` 覆写；
  `BattleService` 用它记录"战斗结束回哪个场景"
- `_startupGameplayScene`：`ChooseCharacterButton` 用 —— **选完角色进哪个玩法场景**，这是这次改动最实际的效果

### 3. 三个 VFX prefab 的失效 Volume 节点

`circles_blue` / `star_yellow` / `fire_red` 各有一个子节点 `BoostVFX1/2/3`，
身上只有 Transform + 一个 URP `Volume` 组件（profile 就是 `Battle01/BoostVFX{1,2,3} Profile.asset`）。
URP 已移除 → Volume 脚本早就没了 → 这三个节点彻底无用，整个摘掉：

- 删 `Volume` 组件块、`GameObject` 块、`Transform` 块
- 从父 Transform 的 `m_Children` 摘掉对应条目

## 验证

| 项 | 结果 |
|---|---|
| 三个目录 + 三个目录 `.meta` | 全部已删除 |
| 剩余场景 | `Demo_SanXingDui`、`Map`、`Menu`、`Persistent`（4 个） |
| Addressables `Scenes` 组 | 4 条：`Demo_SanXingDui`/`Map`/`Menu`/`Persistent` |
| `Persistent` 里 `Town01_Day` guid 残留 | **0** |
| 三个 prefab 的 Volume / BoostVFX 残留 | **0 / 0** |
| YAML 完整性（`m_Component` 与 `m_Children` 断链） | 4 个文件均 **0 断链** |
| 空目录清理 | 21 个 |

## 备份与恢复

| 内容 | 位置 |
|---|---|
| 引用修复前的原文件 | **`_bak_scene_del/`**（已 gitignore）：`Scenes.asset.bak`、`Persistent.unity.bak`、3 个 prefab 的 `.bak` |
| 场景与资源本体 | **Windows 回收站**（清单 `Tools/AssetAudit/scene_delete.tsv`，171 条） |

⚠️ 回收站要清空才真正释放磁盘。恢复时场景本体从回收站还原，同时把 `_bak_scene_del/` 里的文件放回原位，并删掉 `Demo_SanXingDui` 的 Addressables 条目。

## 后续：删除后新增的孤儿资产

删掉旧场景后重新跑 `Tools/AssetAudit/scan_unused.py`，工程资产 1717 个、可达 931 个，
**候选孤儿 495 个 / 94.2 MB**。其中本次删除直接导致的：

| 目录 | 数量 | 体积 | 原因 |
|---|---|---|---|
| `Assets/Environment/Textures` | 78 | 18.1 MB | 旧城镇贴图，只被旧场景引用 |
| `Assets/Environment/Meshes` | 73 | 10.6 MB | 同上 |
| `Assets/Arts/Environment` | 4 | 7.2 MB | 天空盒等 |
| `Assets/Arts/Characters` | 8 | 1.7 MB | 旧角色立绘 |
| `Assets/Environment/{Prefabs,Materials}` | 116 | 0.4 MB | 同上 |
| `Assets/Animation/Ally` | 17 | ~0 | 旧动画 |

按之前的判定规则拆分：

- **安全清单 348 个 / 39.5 MB**（纯美术资源：`.tga` / `.fbx` / `.psd` / `.png` / `.obj` / `.pdf` 等）
- **需确认 147 个 / 54.7 MB**（`.cs`(97) / `.unity`(8 演示场景) / `.shader` / `.cginc` / 字体 / 光照数据）

> ⚠️ `.cs` 不能靠引用图判定 —— 通过 `AddComponent<T>()` 或纯静态调用使用的类不会有 guid 引用。
> 上一次清理也是因此把 `.cs` 归入需确认。

**尚未执行清理**，等确认。
