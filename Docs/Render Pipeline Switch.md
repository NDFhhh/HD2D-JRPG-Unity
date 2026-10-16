# 渲染管线切换：URP → Built-in RP

> 执行日期：2026-10-16　｜　起因：新导入的 `Assets/Scenes/Demo_SanXingDui.unity` 是 Built-in 管线场景，在 URP 工程里显示为洋红。

## 结论

工程已从 **URP 14.0.12** 切换为 **Built-in Render Pipeline**，`com.unity.render-pipelines.universal` 与 `com.unify.unified-universal-blur` 已从包清单移除。

## 为什么原场景是洋红

| 项 | 内容 |
|---|---|
| 新场景 | `Assets/Scenes/Demo_SanXingDui.unity`（三星堆 Demo） |
| 它用的资源 | `Assets/LostTemple-BuiltInRP/`（包名即 BuiltInRP）+ `Assets/PolygonSciFiWorlds/`（Synty）+ `Assets/Scenes/Meshes/` |
| 它用的着色器 | 22 个材质里 **19 个指向 Unity 内置资源**（Standard 系），2 个 Synty 自定义（Amplify 生成，Built-in），1 个 Synty 三平面 |
| 场景里 URP 组件 | **0 个** |
| 工程当时管线 | URP（Graphics → `URP-HighFidelity.asset`；Quality 三档各自挂 URP 资产） |

Built-in 的 Standard 着色器在 URP 下没有对应 SubShader → Unity 回退到洋红错误着色器。

## 实际改动（共 6 项）

执行前 Unity 编辑器已完全关闭（编辑器运行时改 `ProjectSettings` 会被它覆盖回去）。

### 1. `Packages/manifest.json`
删除两条依赖，依赖数 42 → 40：
- `com.unity.render-pipelines.universal: 14.0.12`
- `com.unify.unified-universal-blur`（Git URL 依赖）

> **为什么必须连 blur 一起删**：`com.unify.unified-universal-blur` 的 `package.json` 里写着
> `"dependencies": { "com.unity.render-pipelines.universal": "14.0.9" }`——只删 URP 的话，
> 包管理器会自动把它当依赖重新装回来。

### 2. 删除 `Packages/packages-lock.json`
让 Unity 下次打开时重新解析依赖图并重建。

### 3. `ProjectSettings/GraphicsSettings.asset`
```diff
- m_CustomRenderPipeline: {fileID: 11400000, guid: 7b7fd9122c28c4d15b667c7040e3b3fd, type: 2}
+ m_CustomRenderPipeline: {fileID: 0}

- m_SRPDefaultSettings:
-   UnityEngine.Rendering.Universal.UniversalRenderPipeline: {fileID: 11400000, guid: 18dc0cd2…, type: 2}
+ m_SRPDefaultSettings: {}
```

### 4. `ProjectSettings/QualitySettings.asset`
三档（Performant / Balanced / HighFidelity）的 `customRenderPipeline` 全部改为 `{fileID: 0}`。

### 5. `Assets/Settings/URP Settings/` → 移出 Assets
7 个资产（3 管线 + 3 Renderer + `UniversalRenderPipelineGlobalSettings`）连同 `URP Settings.meta`
移到 `_bak_pipeline_switch/`。不移的话 URP 包消失后它们会变成 Missing Script 资产。

### 6. 孤立的工程设置移出
`ProjectSettings/URPProjectSettings.asset`、`ProjectSettings/ShaderGraphSettings.asset` → `_bak_pipeline_switch/`。

## 备份与还原

全部备份在项目根目录 **`_bak_pipeline_switch/`**（已加入 `.gitignore`）：

```
_bak_pipeline_switch/
├─ manifest.json.bak               # 原包清单
├─ packages-lock.json.bak          # 原包锁
├─ GraphicsSettings.asset.bak      # 原 Graphics 设置
├─ QualitySettings.asset.bak       # 原 Quality 设置
├─ URP Settings/                   # 原 Assets/Settings/URP Settings 整个目录
├─ URP Settings.meta
├─ URPProjectSettings.asset.bak
└─ ShaderGraphSettings.asset.bak
```

**还原方法**：把上述文件放回原位（`URP Settings/` 连同 `.meta` 放回 `Assets/Settings/`），
并在 `manifest.json` 里加回那两条依赖，然后删掉 `packages-lock.json` 重开 Unity。

## ⚠️ 副作用：47 个材质会变洋红

用 `Tools/AssetAudit/find_urp_materials.py` 扫出——`Assets/` 下 146 个材质里 **47 个**用的是已移除包的着色器：

| 着色器 | 数量 | 位置 |
|---|---|---|
| URP `Lit.shader` | 46 | `Assets/Environment/Materials/*`（43 个，旧城镇场景用）+ `Assets/Martials/Material Shadow {Battle,Field,NPC}.mat` |
| Blur `TintedBlurUI.shader` | 1 | `Assets/Martials/UniversalBlurUI.mat` |

其中 **`Material Shadow Battle/Field/NPC.mat` 被 `Assets/Prefabs/Character/BattleUnit.prefab`、`Follower.prefab` 引用**，
这两个是角色预制体，删旧场景不会一起消失。若要保留，需要把这 3 个材质从 URP/Lit 改指 `Standard`，
并重映射属性：`_BaseMap` → `_MainTex`、`_BaseColor` → `_Color`（还有 `_Metallic` / `_Smoothness` / `_BumpMap` / `_OcclusionMap`）。

## 验证清单（已通过）

- `manifest.json` JSON 合法；URP / blur 均已不在依赖列表
- `GraphicsSettings`：`m_CustomRenderPipeline: {fileID: 0}`、`m_SRPDefaultSettings: {}`
- `QualitySettings`：3 处 `customRenderPipeline: {fileID: 0}`
- `grep -r UniversalRenderPipeline ProjectSettings/` = **0**
- `grep -r UniversalRenderPipeline Assets/*.asmdef` = **0**；自研脚本 0 处引用 URP API

---

## 第二轮：URP Volume 后处理的连锁影响（重开 Unity 后）

### 报错

```
Assets\Scripts\Battle\Runtime\Components\BattleHitPostFX.cs(8,31): error CS0246:
The type or namespace name 'Volume' could not be found
```

### 根因

`Volume` / `VolumeProfile` 是 **SRP Core**（`com.unity.render-pipelines.core`）里的类型，
它只是 URP 的依赖 —— 删 URP 时被一起带走了，全工程只此一处编译错误。

### 影响面（全量扫描结果）

| 类型 | 内容 |
|---|---|
| 编译错误 | `BattleHitPostFX.cs:8` 的 `Volume breakVolume`（唯一一处） |
| 失效的 `Volume` 组件 | **11 个**：`Persistent`/`Battle01`/`Battle03`/`Field01`/`Field03`/`Town01_Day`/`Town01_Night` 7 个场景 + `circles_blue`/`fire_red`/`star_yellow` 3 个第三方 VFX prefab + 插件 Demo `CFXRF Demo.unity` |
| 失效的 Volume Profile | **6 个**：`Break Volume Profile`、`Battle01 Volume Profile`、`BoostVFX1/2/3 Profile`、`SGS_VolumeProfile` |

被移除的后处理效果（`Break Volume Profile` 里叠的 5 个）：**Bloom(强度 3) + Vignette + 景深 + 色差 + 镜头畸变**。

### 处理方式：改为自包含的全屏 UI 闪屏

用户选择「不装包、改成全屏 UI 闪屏」。`BattleHitPostFX.cs` 重写为驱动一个全屏 `Image` 覆盖层：

- 不再 `using UnityEngine.Rendering`，字段 `Volume breakVolume` → `Image flashOverlay`（可留空）
- **覆盖层由代码在 `Awake` 自动创建**：根 GameObject（`Canvas` 必须是根节点，嵌套 Canvas 的 `renderMode` 不生效）→
  `Canvas.renderMode = ScreenSpaceOverlay`、`sortingOrder = 32000` → `Image`（不挂 sprite 即纯色四边形、
  `raycastTarget = false` 不挡 UI）→ RectTransform 四边拉伸铺满
- 新增可调字段：`flashColor`（默认白，alpha 0.85）、`flashWeight`、`flashDuration`、`disableWhenIdle`
- `Play()` **对外签名不变** → `BattleCinematicService` 无需改动
- 用 `WaitForSecondsRealtime` 等待（破盾演出同时有慢动作，不能用受 `timeScale` 影响的等待）

### 场景侧改动

`Tools/AssetAudit/fix_persistent_volume.py`（干跑/`--apply`）：

1. 删除 `Persistent.unity` 里 GameObject `Break Volume` 上的 `Volume` 组件块 `&261066788`
2. 从该 GameObject 的 `m_Component` 列表摘掉对应条目
3. 刷新 `BattleHitPostFX` 组件 `&261066790` 的序列化字段 → `flashColor`/`flashWeight`/`flashDuration`/`disableWhenIdle`/`flashOverlay`

> **保留了同一个 GameObject**（仍叫 `Break Volume`），因为 `BattleCinematicService.breakPostFx: {fileID: 261066790}` 指向它。
> `Break Volume Profile.asset`（唯一引用方就是刚删掉的那个组件）已移到 `_bak_pipeline_switch/`。

### 验证

- `grep Volume 脚本 guid Persistent.unity` = **0**；`&261066788` = **0**
- `Assets/Scripts` 引用 `UnityEngine.Rendering.Universal` = **0**
- `Assets/**.asmdef` 引用 `Unity.RenderPipelines` = **0**
- `CFXR_Demo.cs` 的 `using UnityEngine.Rendering.Universal;` 有 `#if CFXR_URP_INSTALLED` 保护，安全

### 后续处理（2026-10-16 下午已完成）

删除 Battle / Field / Town 场景时一并处理，详见 `Docs/Scene Cleanup.md`：

| 项 | 处理 |
|---|---|
| 3 个 VFX prefab 的 `Volume` 组件 | ✅ 摘掉。`circles_blue`/`star_yellow`/`fire_red` 里那个只挂 Transform + Volume 的 `BoostVFX1/2/3` 子节点已整个移除（连同父 Transform 的 `m_Children` 条目） |
| `BoostVFX1/2/3 Profile.asset` | ✅ 随 `Assets/Scenes/Battle/` 一起删除 |
| `Battle01 Volume Profile.asset` | ✅ 随 `Assets/Scenes/Battle/` 一起删除 |
| `SGS_VolumeProfile.asset`、`SGS_LUT.png` | ✅ 随 `Assets/Scenes/Town/`、`Assets/Scenes/Field/` 删除（`SGS_VolumeProfile` 在 `Assets/PostProcess/`，见 `Docs/Scene Cleanup.md` 待办） |
| 旧场景里的 `Volume` 组件 | ✅ 随场景一起删除 |

---

## 第三轮：Missing Script 全清 + 四个洋红材质还原（2026-10-16 傍晚）

### 3.1 判据：别再写死组件 id，改用「guid 能不能兑现」

上一轮只处理了 `Persistent.unity` 的 `Volume`，漏了同类的 URP 伴生组件。这次改成通用判据——
**扫出所有场景/prefab 里 `m_Script` 的 guid 在工程内无法兑现的 `MonoBehaviour` 块**，一次抓干净。

工具：`Tools/AssetAudit/fix_urp_leftovers.py`（干跑 / `--apply`）

| 文件 | 摘掉的块 | 挂在 |
|---|---|---|
| `Assets/Scenes/Persistent/Persistent.unity` | `UniversalAdditionalCameraData` `&1545161102` | **Main Camera** ← 截图里的 Missing Script |
| `Assets/Scenes/Demo_SanXingDui.unity` | `PickupAction` × 4 | `Quest_01` / `Quest_01 (1)(2)(3)` |
| `Assets/Prefabs/VFX/circles_blue.prefab` | `UniversalAdditionalLightData` `&1783668852483797150` | `Point Light` |
| `Assets/Prefabs/VFX/star_yellow.prefab` | `UniversalAdditionalLightData` `&9205241387175790090` | `Point Light` |
| `Assets/Prefabs/VFX/fire_red.prefab` | `UniversalAdditionalLightData` `&6813886114552016305` | `Point Light` |

> **`UniversalAdditionalCameraData` / `UniversalAdditionalLightData` 和 `Volume` 一样，都是 URP 包里的类**，
> 删包后必然变 Missing Script。相机/灯光本体是 `Camera`/`Light`（内置组件），摘掉这两个附加值组件不影响功能。

`Quest_01` 那 4 个是**历史遗留、非本次迁移造成**：脚本 guid `b5a58dbb…` 在 git 全历史里查不到，
它引用的物品资产（`b048ba30…`）和图标（`dfb32f07…`）也都不存在 —— 原作者没提交的本地实验残留。
按用户选择只摘组件、保留 `Quest_01` 物件本身。

另：`Assets/PostProcess/SGS_VolumeProfile.asset`（SRP Core 的 `VolumeProfile`，唯一引用方是它自己）已移入 `_bak_pipeline_switch/`。

### 3.2 四个洋红材质的还原方式

材质在**首次提交时就已经是 URP 形态**（`git show HEAD` 同样是 `933532a4…`），所以无法从 git 恢复原始版本，
只能按属性语义反推。反推依据：URP `Lit` 里残留着 `_Glossiness` / `_GlossyReflections` / `_Parallax` /
`_SmoothnessTextureChannel` / `_Mode` —— **这些是 Built-in `Standard` 才有的属性名**，
说明原材质就是 `Standard`，被 URP 升级工具改写过。

工具：`Tools/AssetAudit/fix_urp_materials.py`（干跑 / `--apply` / `--all` / `--strip-version`）

| 材质 | 改前 | 改后 |
|---|---|---|
| `Material Shadow Battle.mat` | URP `Lit` | 内置 `Standard`，`_Mode: 1`（Cutout），队列 2468 |
| `Material Shadow Field.mat` | URP `Lit` | 同上，队列 2474，`_Color` 保留原 0.647 灰 |
| `Material Shadow NPC.mat` | URP `Lit` | 同上，队列 2468（**无任何引用方**，孤儿） |
| `UniversalBlurUI.mat` | blur 包 `TintedBlurUI`（包已删） | → **`Assets/Martials/UI Scrim.mat`** + 新写着色器 `UI Scrim.shader` |

属性映射：`_BaseMap` → `_MainTex`、`_BaseColor` → `_Color`；
关键字由模式推导：`_ALPHATEST_ON`（Cutout）+ `_GLOSSYREFLECTIONS_OFF` / `_SPECULARHIGHLIGHTS_OFF`（原 `_SpecularHighlights: 0`、`_GlossyReflections: 0`）；
**渲染队列沿用原数值**（2468 / 2474），保证与切管线前的绘制顺序完全一致。

#### 为什么模糊材质没有直接用 `Standard`

我原本承诺的是「改用 Standard 透明模式」，但落地时改主意了，原因是：

- Canvas 默认只往顶点流里提供 `TexCoord0`，而 `Standard` 需要 `Normal` 通道 →
  挂到 UGUI 上 Unity 会报「Canvas 缺少 Normal 着色器通道」；
- `Standard` 是**受光**着色器，在 Overlay Canvas 里表现不可控。

所以照工程里已有的 `Assets/Martials/Wipe Shader.shader`（同一个作者、同一类问题的手写替代品）的路子，
新写了 `Assets/Martials/UI Scrim.shader`（`UI/Scrim`，`Fallback "UI/Default"` 兜底）——
**无光照、吃顶点色、带 Stencil / RectMask2D 裁剪**，是标准的 UGUI 着色器写法。

- 材质：`_Color = {0.05, 0.05, 0.07, 0.7}` → 约 70% 不透明的深灰蓝遮罩
- **沿用原 guid `5b1fbdac26f39e943b0e13aac45087d4`**（改写而非新建再改引用），
  所以 `Persistent.unity` 里 `ShopAction Panel` 背景层的 `m_Material` 一个字节都不用动
- 顺手把该 Image 的 `m_Color` 从 `{0.784,0.784,0.784,1}` 归一成白色，
  让遮罩深浅只由材质的 `_Color` 决定，避免两处相乘

工具：`Tools/AssetAudit/fix_blur_scrim.py`（干跑 / `--apply`）

### 3.3 清掉 50 个材质里的 `MaterialVersion` 伴生块

每个被 URP 升级过的 `.mat` 文件头都挂着一块 `MonoBehaviour`（script guid `d0353a89b1f911e48b9e16bdc9f2e058`），
这是 URP 用来记录材质版本的，删包后同样是无主脚本。

`fix_urp_materials.py --strip-version [--apply]` 全工程扫一遍：**46 个文件 / 46 个块**（其余 4 个随转换一起处理），已清空。

### 3.4 验证

| 检查 | 结果 |
|---|---|
| 5 个被改文件的 YAML 内部断链（`{fileID: N}` 找不到对应块） | **0** |
| 4 个游戏场景 + 关键 prefab 的失效脚本 | **0** |
| 全工程 `.mat` 里的 `MaterialVersion` | **0** |
| 4 个目标材质着色器兑现 | Shadow ×3 → 内置 `Standard`；`UI Scrim.mat` → `UI Scrim.shader` ✅ |
| 场景 → 遮罩材质引用（`&795452619` 的 `m_Material`） | 指向 `5b1fbdac…`，仍有效 ✅ |

### 3.5 本轮新发现（未处理，待定）

`Demo_SanXingDui.unity` 里还有 3 个**资产**（不是脚本）的悬挂引用。该场景**从未提交过 git**，无法追溯：

| guid | 类型 | 挂在 | 数量 |
|---|---|---|---|
| `d9cafd33bb3d9ff4d8621ea5f90f7e88` | 材质 | `Hajimi` / `point` / `Sign (1)` 等 27 个 `SpriteRenderer.m_Materials` | 27 处 |
| `ec56ded879523d94292b3815936737a7` | AnimatorController | `Test` 的 `m_Controller` | 1 处 |
| `7a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d` | dialogue（SO） | `Hajimi` 的 `dialogue` 字段 | 1 处 |

> 这类引用**不会显示洋红**：材质资产丢失时 Unity 会把引用解析成 `null`，`SpriteRenderer` 回落到默认材质；
> 只有「材质在、着色器丢」才渲染洋红 —— 正好解释了为什么用户只看到那 4 个。
> 清掉它们只是让 Inspector 不再出现 `Missing` 提示，视觉上无变化。

## 后续待办

1. ~~决定 `Assets/Environment/Materials/*` **43 个 URP `Lit` 材质**的去留。~~
   ✅ **已删**（2026-10-16 14:30，87 项入回收站）。见 `Docs/Asset Cleanup.md` 第八节。
   全工程 `.mat` 现在 **97 个，着色器缺失 0 → 零洋红**。
   备份 `_bak_env_materials/`；`fix_urp_materials.py --all --apply` 仍可作批量转换的备选（已不再需要）。
2. 决定 3.5 节那 3 个悬挂引用 + 本轮新增的 1 处（`Hajimi` 的 SpriteRenderer →
   `ObjMD_Daiseido_A3`，随第 1 条一起被删）是否清掉（把引用置 `{fileID: 0}`）。
   **动场景前必须先关 Unity。**
3. `Assets/Environment/Meshes/*.fbx.meta` 里 **74 处材质重映射**现在悬空
   （`externalObjects` 指向刚删的 43 个材质）。修法是删掉这些条目；
   更省事的是连同整棵 Environment 孤儿树一起删。
4. 决定删除后新增的 495 个孤儿资产（94.2 MB）是否清理。
5. `Assets/Martials/Wipe Shader.shadergraph` 随 ShaderGraph 包一起作废（本就已坏、无引用，已有 `.shader` 替代），建议删除。
6. 新场景目前**不在 Build Settings**（`EditorBuildSettings` 里只有 `Persistent.unity` 且 `enabled: 0`），要用它做主场景需自行加入。

> ⚠️ **引用扫描的教训**：本轮第一遍「Python 一次性反向扫描」**漏报了主场景引用**，
> 差点把主场景还在用的材质当孤儿删掉。**权威判据是 `grep -rlF -f <guids> Assets`**，
> 任何脚本结论都必须 grep 交叉复核。

### 第三方插件 Demo 场景（可忽略）

下面这些是插件自带的 demo 场景，不在 Addressables 组、也不在 Build Settings，它们的 Missing Script 一直存在：

| 场景 | 缺失脚本 guid |
|---|---|
| `Assets/Plugins/JMO Assets/Cartoon FX Remaster/CFXRF Demo.unity` | `17251560…`（URP `Volume` × 2） |
| `Assets/Plugins/JMO Assets 0/Cartoon FX (legacy)/Demo/CFX Free Demo.unity` | `f70555f1…` × 26 |
| `Assets/Plugins/Hovl Studio/…/Demo scene/Magic effects*.unity` | `8b9a305e…`、`948f4100…` |
| `Assets/Plugins/Lana Studio/…/LanaDemo0*.unity` | `23c1ce4f…` |

**但插件目录里也有游戏在用的资产**，这类不能忽略，已一并清掉（2026-10-16 14:35）：

| 文件 | 失效组件 | 引用方 |
|---|---|---|
| `Assets/Plugins/Hovl Studio/Magic effects pack/Prefabs/AoE effects/Crystals crossfade 2.prefab` | `UniversalAdditionalLightData`（`Light` 上 1 个） | `Game Data/Skill/Skill Data/落冰.asset` |
| `Assets/Plugins/Hovl Studio/Magic effects pack/Prefabs/AoE effects/Crystals front attack.prefab` | 同上 | `Game Data/Skill/Skill Data/冰暴.asset` |

> 判据同 `fix_urp_leftovers.py`：清完后两个 prefab **断链 0、失效脚本 0**，`Light` 的 `m_Component` 完整。
> 备份在 `_bak_pipeline_switch/urp_leftover/`。

**排除第三方 Demo 场景后，主工程已无任何失效脚本或洋红材质。**

唯一残留：`Assets/PolygonSciFiWorlds/Models/Collision/Convex/*.asset` 共 **8 个**，
脚本 guid `5b71ad40…`（Synty 凸包碰撞数据工具类）**从未提交进本仓库**（git 全历史 0 提交）——
这是 Synty 资产包自带缺陷，不影响渲染、不报编译错，被 `PolygonSciFiWorlds/Prefabs/Environments/` 下 3 个 prefab 引用。
去留归入下面的孤儿清单一起决定。
