# 未使用资源清理记录

> 执行日期：2026-10-16
> 目标：清理工程中长期未使用的资源（场景资源包、Art 未使用精灵等）

## 一、清理结果

| 项目 | 清理前 | 清理后 |
|---|---:|---:|
| `Assets/` 总体积 | 约 1.6 GB | **555 MB** |
| 有 meta 的资产数 | 7597 | **1430** |
| 可达资产（被引用） | 866 | **988** |
| 候选孤儿 | 6230 | **168**（保留） |

**实际删除：12124 个文件（6062 个资产 + 6062 个 .meta），约 1.06 GB**
**追加清理：98 + 7 个孤儿 .meta，105 个空目录**

全部移入 D 盘回收站（可恢复），且 **6062 个资产 100% 已被 git 跟踪**，必要时
`git checkout -- <path>` 亦可还原。

## 二、判定方法

从工程**入口**出发做引用图可达性遍历，遍历不到的即为孤儿。

入口集合：
- `Assets/AddressableAssetsData/AssetGroups/Scenes.asset` 的 10 个场景条目
- `ProjectSettings/EditorBuildSettings.asset`（Persistent）
- `Assets/Resources/**`（代码动态加载）
- `Assets/AddressableAssetsData/AddressableAssetSettings.asset`
- `ProjectSettings/*.asset` 引用的资产（→ URP Settings、Input System 等）

边：任意文本资产里的 `guid:` / `m_GUID:` / `m_AssetGUID:`，以及 `.meta` 的出边
（**归属到属主资产**）、`.cs` 里的 `GUIDToAssetPath("...")`。

## 三、验证（这一步是重点）

单次扫描不足以采信，做了 4 层验证：

1. **反向自洽验证**：把 6230 个孤儿的 guid 收集成集合，全工程 14888 个文件重扫一遍，
   统计「非孤儿引用孤儿」的矛盾数 → **0 处**。
2. **`.meta` 出边补齐**：初版扫描器跳过了 `.meta`，漏掉 FBX 的材质重映射
   （`X.fbx.meta → Y.mat`，86 处误判）。修正后矛盾归零。
3. **二进制盲区排查**：工程有 11 个非文本 Unity 资产（`LightingData.asset`、
   `Terrain.asset`、`Battle.asset` 等，共 6.14MB），它们内部引用以 16 字节原始
   GUID 存储。做了滑动窗口扫描 → 0 命中；这些资产本身全部可达或落入「需确认」。
4. **约定式引用排查**：
   - `csc.rsp` —— Unity 按**文件名约定**读取的编译器响应文件，**必须保留**
   - `#include` 引用的 `.shader` / `.cginc` —— 路径引用，非 GUID
   - `.asmdef` / `.dll` —— 影响编译
   - 以上全部归入「需确认」，不自动删除
   - AssetBundle 标签：孤儿的 `assetBundleName` 命中 **0** 个
   - Addressables 收录：孤儿被组条目收录 **0 个**
   - 代码字符串动态加载：`Resources.Load` **0 处**；`LoadAssetAtPath` 仅 1 处
     （`AudioSystemSetupMenu.cs`，目标是可达的 `SoundLibrary.asset`）

## 四、删除明细

| 品类 | 数量 | 体积 |
|---|---:|---:|
| `Assets/Prefabs/场景资源`（模型/贴图/预览图） | 4998 | 889 MB |
| 第三方插件 Demo 内容（Hovl / JMO / Lana） | 806 | 123 MB |
| `Arts/Characters` 未使用精灵 | 84 | 12 MB |
| `Arts/UI` 未使用精灵 | 72 | 7 MB |
| `Arts/Mask` 未使用遮罩 | 1 | 0.6 MB |
| `Arts/Icons` 未使用图标 | 47 | 1.0 MB |
| `Arts/Audio/BGM/Menu/Menu.mp3` + `你干嘛哎哟.WAV` | 2 | 6 MB |
| `Martials/Terrains` 未使用地形材质 | 21 | 20 MB |
| `TextMesh Pro` / `Environment` / 其他散落 | 31 | 3 MB |
| **合计** | **6062** | **1061 MB** |

## 五、保留项（168 个 / 77.8 MB）

用户选择全部保留。这些有非 GUID 的引用机制或影响编译：

| 类别 | 数量 | 说明 |
|---|---:|---|
| `.cs` 脚本 | 92 | 可能被反射 / 字符串 / 编辑器调用 |
| `.shader` / `.cginc` | 22 | 可能被 `#include` 路径引用 |
| `.asmdef` | 6 | 程序集定义，删了可能破坏编译 |
| 光照 / 烘焙数据 | 30 | `Lightmap` / `ReflectionProbe` / `LightingData`，由二进制资产引用，扫描器看不到 |
| `.unity` 场景 | 9 | 8 个插件 Demo 场景 + `Town01_Night.unity`（游戏场景，零引用但语义重要） |
| `Arts/Fonts` ExtraLight 字体 | 2 | 52 MB（SDF + otf） |
| 其他 | 7 | `.dll` / `.mdb` / `.uxml` / `.uss` / `.rsp` / `.shadergraph` |

## 六、待你决定的遗留项

1. **`Assets/Scenes/Town/Town01_Night/Town01_Night.unity`**（3.5 MB）
   零引用，但它是游戏场景。若确认不做昼夜切换，可删（连同其 Lightmap / LightingData）。
2. **`Assets/Arts/Fonts/SourceHanSerifSC-ExtraLight SDF.asset` + `.otf`**（52 MB）
   TMP 字体资产，零引用。
3. **`Assets/Arts/Audio/BGM/Menu/Menu.mp3`（已删）**
   代码里有 `E_SoundId.BGM_Menu` 枚举，但 `SoundLibrary.asset` 未接线，
   因此该资源此前确实未被加载。若以后需要菜单 BGM，需从回收站恢复并补接 `SoundLibrary`。
4. **8 个插件 Demo 场景**：其依赖的 Demo 资源已删，场景现在是「空壳」。
   若觉得碍事可一并删除（`Assets/Plugins/{Hovl Studio,JMO Assets,JMO Assets 0,Lana Studio}` 下的 Demo 场景）。

## 七、工具与清单

全部在 `Tools/AssetAudit/`（只读分析，可整体删除）：

| 文件 | 用途 |
|---|---|
| `scan_unused.py` | 引用图可达性扫描器 |
| `crosscheck_orphans.py` | 反向自洽验证 |
| `explain_ref.py` | 单资产引用链解释器 |
| `verify_orphan.py` | 单资产全形式引用验证 |
| `scan_binary_refs.py` | 二进制资产 16 字节引用扫描 |
| `build_report.py` / `build_plan.py` | 报告与删除方案生成 |
| `apply_delete.py` | 删除执行器（移入回收站，默认干跑） |
| `delete_safe.tsv` | **本次删除的完整清单（12124 条，可用于恢复）** |
| `delete_review.tsv` | 保留的 168 项清单 |
| `unused_report.md` | 分类报告 |

### 恢复方式

```bash
# 方式 1：git 还原（资产都在 git 跟踪内）
git checkout -- <path>

# 方式 2：从 Windows 回收站还原（D 盘回收站里，按删除时间 2026-10-16 10:46 查找）
```

---

## 八、追加：43 个洋红材质（URP Lit 残留）清理 — 2026-10-16 14:30

### 目标

`Assets/Environment/Materials/` 下 **43 个材质**在 Material 选择器里全是洋红。

**判定依据**：这 43 个的 `m_Shader` 都指向 guid `933532a4fcc9baf4fa0491de14d08ed7`（URP `Lit`），
该 guid 在工程内（含 `Library/PackageCache`）**无法兑现** —— URP 包已删，着色器文件随之消失。
材质在、着色器丢 → 洋红。（对照：`d9cafd33…` 那类**资产整个丢失**的引用**不会**洋红，
Unity 把引用解析成 null 并回落默认材质。）

用同一条判据全量扫描 `Assets/**/*.mat`：**只有这 43 个洋红**，其余 97 个（内置 `Standard` ×74、
CFXR `.cfxrshader` ×11、Synty / TMP 着色器 ×12）全部正常。

### 引用扫描（关键一步）

第一遍用 Python 一次性反向扫描 **漏报了主场景引用**（输出被截断）。
改用 grep 兜底才是权威：

```bash
grep -rlF -f Tools/AssetAudit/_env_guids.txt Assets
```

命中的 118 个文件里，排除每个材质自身的 `.mat.meta` 后只剩两类：

| 引用方 | 数量 | 说明 |
|---|---|---|
| `Assets/Environment/Meshes/*.fbx.meta` / `*.obj.meta` | 74 | ModelImporter 的 `externalObjects` 材质重映射 |
| `Assets/Scenes/Demo_SanXingDui.unity` | 1 | `ObjMD_Daiseido_A3` |

**没有任何 prefab / 其它材质 / Addressables 引用它们。**

主场景那 1 处值得单独说：它挂在 `Hajimi` 物体的 `SpriteRenderer`（组件 `&933337985178095324`）上 ——
一个 3D 环境材质被挂到了 2D 精灵上，本身就是误操作，而且它是洋红的。
删掉后 SpriteRenderer 回落默认精灵材质，**反而比现在正常**。

> ⚠️ 教训：`Assets/Environment/` 整棵树在前一轮被记为「只挂在已删旧场景上」，
> 但实际主场景还有 1 处引用。**引用扫描不能用一次性脚本的结论，必须 grep 交叉复核。**

### 执行结果

| 项 | 数量 |
|---|---|
| 移入回收站 | **87 项**（43 个 `.mat` + 43 个 `.mat.meta` + `Materials.meta`） |
| 清理空目录 | 1 个（`Assets/Environment/Materials/`） |
| 体积 | 0.1 MB |

### 校验

- `Assets/Environment/` 下只剩 `Meshes/` `Prefabs/` `Textures/`
- 全工程 `.mat` = **97 个，着色器缺失 0** → **零洋红**
- 新产生的悬挂引用：74 处 FBX 重映射 + 主场景 1 处（见下）

### 遗留（待处理，动场景前必须先关 Unity）

1. **主场景 1 处悬挂材质引用** —— `Hajimi` 的 SpriteRenderer。
   与之前扫出的另外 3 处（丢失材质 `d9cafd33…` ×27 SpriteRenderer、AnimatorController `ec56ded8…`、
   dialogue `7a1b2c3d…`）可一并清掉。
2. **74 处 FBX 材质重映射悬空** —— `Assets/Environment/Meshes/*.fbx.meta` 的 `externalObjects`。
   修法是删掉这些条目，但更省事的是连同整棵 Environment 孤儿树一起删（见上轮清单）。

### 恢复

原件备份在 `_bak_env_materials/`（86 个文件，已加进 `.gitignore`），
另外 Windows 回收站里也有一份（按删除时间 **2026-10-16 14:30** 查找）。

### 本轮新增工具

| 文件 | 用途 |
|---|---|
| `prep_env_mat_delete.py` | 备份 43 个材质 + 生成删除清单 |
| `delete_env_materials.tsv` | 87 条删除清单（可用于恢复） |
| `_env_guids.txt` / `_env_hits.txt` | 43 个 guid 与 grep 命中文件 |
| `fix_urp_materials.py --all` | 备选方案：**批量把 URP Lit 转成内置 Standard**（未使用） |
