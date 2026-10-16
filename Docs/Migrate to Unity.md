# 从团结引擎迁移到 Unity 引擎 · 方案

> 对象工程：CodePath Traveler（HD2D-JRPG-Unity）
> 编写依据：对当前工程的实际扫描结果（`guid_report.txt`，2026-10-15 生成）
> 配套工具：`Tools/MigrateToUnity/`

---

## 0. 一句话结论

**可以迁，但不能"直接用 Unity 打开"。**

唯一真正的硬骨头是**资产 GUID 体系**——团结引擎 1.x 把 `.meta` 里的 GUID 换成了自己的 56 位 base64 格式，标准 Unity 不认，会给资产重新发 GUID，于是场景 / prefab / ScriptableObject 里所有引用当场断裂。
除此之外，你这个工程**几乎不需要改代码**：场景里只用了标准 Unity 组件，C# 里也搜不到任何团结专有 API。

正确顺序是：**先在团结引擎里把资产身份归一化 → 再换官方 Unity 打开**。反过来做会付出几十倍的修引用代价。

---

## 1. 为什么不能直接用 Unity 打开（原理）

| 视角 | 团结引擎 | 标准 Unity |
|---|---|---|
| `.meta` 里的 `guid` | 56 位 base64（XGUID），如 `Cy5LvXmpUHqrQkZAUQP9Tl6HAf9UGnqf8CYglX1+PB5YGl6kK718Kr4=` | 32 位 hex，如 `c9429c5f6656e49ec8a2e5e2ef1709e2` |
| 场景/prefab/SO 里的引用 | **仍然是 32 位 hex GUID** | 32 位 hex GUID |
| YAML 头 | `%TAG !u! tag:yousandi.cn,2023:` | `%TAG !u! tag:unity3d.com,2011:` |

关键点：**引用那一侧没有变，只有"身份证"那一侧变了**。团结官方社区的原话是——这套 base64 格式"原本是为了与 Unity 彻底割裂而设计的"，后来"已支持互转"，但没有对外公开发布转换工具。

所以迁移的本质是：**把每个 `.meta` 的 `guid` 改回"YAML 里正在引用的那个 32 位 hex 值"**。改完引用就一条都不会断。

---

## 2. 本工程体检报告（实测）

| 检查项 | 实测结果 | 迁移动作 |
|---|---|---|
| 引擎版本 | `2022.3.62t12` / Tuanjie `1.10.0` | 换成官方 **Unity 2022.3.62f1**（同基线，改动最小） |
| `.meta` GUID 格式 | **XGUID 6009 个** / hex 1587 个 | **必须归一化**（核心工作） |
| YAML 引用健康度 | 1037 个 YAML 引用 1553 个不同 GUID，其中 **436 个只能由 XGUID 资产兑现** | 直接给 Unity 打开 = 当场断 436 处引用 |
| YAML `%TAG` 命名空间 | 185 个文件是 `yousandi.cn,2023` | 批量替换为 `unity3d.com,2011` |
| 包注册表 | `packages-lock.json` 里 22 处指向 `packages.tuanjie.cn` | 删除 `packages-lock.json`，让 Unity 重新解析 |
| 包版本 | `inputsystem 1.14.4-t3`、`URP/core/shadergraph 14.2.0-t1` | 去掉 `-t1/-t3` 后缀，改用官方可用版本 |
| 团结专有模块 | `com.unity.modules.infinity`、`com.unity.modules.tuanjiegi` | 从 `manifest.json` 删除 |
| 团结专有设置 | `ProjectSettings/AutoStreamingSettings.asset`；`ProjectSettings.asset` 里的 `instantGame*` / `WeixinMiniGame` / `openHarmony*` 字段 | 建议删掉 AutoStreamingSettings；其余 Unity 会自动忽略 |
| 场景/prefab 用的组件 | 只有标准 classID（1/4/114/212/95/108/1001/218…） | **无需处理** ✅ |
| 代码里的团结 API | 搜 `Tuanjie` / `WeChatWASM` / `InstantGame` / `YooAsset` 均为 0 命中 | **无需改写** ✅ |
| 技术栈 | URP + 新输入系统 + Addressables + Cinemachine 3 + Timeline + UGUI/TMP | 迁移后基本无痛 ✅ |

**结论：这是一次"资产层迁移"，不是"代码层重写"。风险集中在前 3 行。**

---

## 3. 迁移步骤

### 阶段 0 · 备份与分支（10 分钟，别跳过）

当前工作区**有大量未提交改动**（Dialogue / Flag / TalkAction 新系统、Menu·Persistent·Town 场景改动、7 个字体 SDF 改动、开场视频替换为 `_low` 版）。迁移要批量改写约 6000 个 `.meta`，混在未提交改动里会很难回退。

```bash
git add -A && git commit -m "chore: 迁移 Unity 前的最后状态"
git tag pre-unity-migration
git switch -c migrate/unity-2022
```

再复制一份工程目录做离线备份（`Library/` 可以先不复制，占空间）。

### 阶段 1 · 资产身份归一化（核心步骤，在团结引擎里做）

三选一：

**方案 A（首选·用引擎 API，最稳）**
1. 把 `Tools/MigrateToUnity/TuanjieGuidConverter.cs` 拷到工程的 `Assets/Editor/` 下；
2. 打开团结引擎，菜单栏出现 `Tools/迁移到Unity`，依次执行：
   - `1 - 体检` → 先记录基线（XGUID 数量、断链数）
   - `2 - 把 .meta 的 GUID 归一化为 Unity 32 位 hex` ← **必须全量，一个 XGUID 都不能留**
   - `3 - 修正 YAML 的 %TAG 命名空间`
   - `4 - 把 .scene 场景改名为 .unity` ← 团结场景后缀 Unity 不认
   - `5 - 验收：列出所有仍然断链的引用` ← **这一步是成败判据**
3. 验收标准：断链数掉到"几十个以内"，且剩下的都是第三方特效包内部的历史断链和 Unity 内置资源占位 GUID（`0000...f000...`）。
4. 若菜单 5 显示断链数**没有下降**，说明引擎 API 返回的是另一个变体：把每个 hex 做一次"每字节高低半字节互换"再重跑菜单 2 即可（工具注释里写了）。

**方案 B（离线·不用开团结，⚠️ 会漏）**
```bash
python Tools/MigrateToUnity/tuanjie_guid_normalize.py            # 只看报告，不动文件
python Tools/MigrateToUnity/tuanjie_guid_normalize.py --convert --fix-tag
```
这套算法**不用猜**：它从 `Library/SourceAssetDB` 反推真实 GUID，再用"YAML 里确实有人在引用这个 GUID"作为反向佐证，只改写能被唯一确定的资产，其余全部跳过并写进清单。
本次实测：可确定修复 411 个资产，断链 436 → 25。

> ⚠️ **但方案 B 有严重缺陷（2026-10-16 实际踩到）**：剩下 5485 个未被引用的资产保持 XGUID，而 **Unity 会直接忽略这些资产**（文件夹不可见、脚本不参与编译）。同时它只扫了 10 种 YAML 后缀，**漏掉了 `.scene` 场景文件**，导致 139 处"只被场景引用"的引用没被覆盖。**不要只用方案 B 就切 Unity**，必须配合路线 A 或 `fix_remaining_guids.py` 收尾（见第 5 章）。
> 另外，一旦 Unity 打开过工程，`Library/SourceAssetDB` 就被 Unity 重写，方案 B 赖以反推的团结数据库就没了 —— **要么先备份 Library，要么直接走路线 A**。

**方案 C（官方渠道）**
如果你的团队有团结引擎的支持渠道，直接向官方索取 GUID/FileID 互转工具或"格式兼容"选项——这是最省事的路子。

> ⚠️ 归一化完成并通过验收后，**不要再回到团结引擎里改资产**，以免它把 `.meta` 又写回 XGUID。如确需验证，先 `git diff` 看清它改了什么。

### 阶段 2 · 切工程配置（15 分钟）

1. **`ProjectSettings/ProjectVersion.txt`** 改成官方版本：
   ```
   m_EditorVersion: 2022.3.62f1
   m_EditorVersionWithRevision: 4af31df58517 (以 Hub 实际显示为准)
   ```
   删掉 `m_TuanjieEditorVersion` 那一行。
2. **`Packages/manifest.json`**：
   - 删除 `com.unity.modules.infinity`、`com.unity.modules.tuanjiegi`
   - `com.unity.inputsystem`: `1.14.4-t3` → 官方 `1.7.0`
   - `com.unity.render-pipelines.universal`: `14.2.0-t1` → `14.0.11`
   - `com.unity.addressables 1.22.3`、`com.unity.cinemachine 3.1.7`、`com.unity.textmeshpro 3.0.10`、`com.unity.timeline 1.7.7` 等在 Unity 里若报"版本不可用"，按 Package Manager 的建议值就近调整
3. **删除 `Packages/packages-lock.json`**（否则 Unity 会继续按 `packages.tuanjie.cn` 解析）
4. **删除 `ProjectSettings/AutoStreamingSettings.asset`**（团结专有）
5. 用 Unity Hub 安装 **Unity 2022.3.62f1**（`unity.com/releases/editor/whats-new/2022.3.62f1`）。若你用 Unity 中国版 Hub，`2022.3.62f2c1 / f3c1` 也可以。

### 阶段 3 · 打开并验收（半天）

打开前建议**删掉整个 `Library/`**（`SourceAssetDB` 已备份），让它干净重建。

验收清单：

- [ ] Console 无编译错误（脚本能编过 = 一大半成功）
- [ ] 打开 `Assets/Scenes/Menu/Menu.scene`，开场视频能播、`StartMenu` 面板正常
- [ ] `Persistent.scene` → `Menu.scene` 场景流转正常
- [ ] `Battle` 场景：战斗流程、弱点破盾、回合时间轴、伤害飘字、UI 全正常
- [ ] `Town01_Day` / `Town01_Night` / `Field` 场景昼夜切换正常
- [ ] `Assets/Game Data/` 下所有 ScriptableObject：无 Missing（角色/技能/道具/音频库）
- [ ] 层级面板全选，Inspector 里无 **Missing Script**
- [ ] `Assets/Settings/` 下 URP Asset / Renderer 引用完整（材质不变粉）
- [ ] `Project Settings > Player > Active Input Handling` = Input System（新输入系统）
- [ ] TMP 中文字体（`SourceHanSerifSC-*-SDF`）正常，**中文不显示成 □**
- [ ] Addressables：Groups 完整、能 Build；模板 `AddressableAssetsData/AssetGroupTemplates/Packed Assets.asset` 若断链，重建或重新指定即可
- [ ] Cinemachine 3 相机切换、Timeline 战斗演出播放正常
- [ ] 真机/PC 打包一次（Player Settings 里团结专有字段会丢失，需要重新确认分辨率、图标、脚本后端）

### 阶段 4 · 提交

```bash
git add -A
git commit -m "chore: migrate engine from Tuanjie 1.10 to Unity 2022.3.62f1

- normalize .meta GUID from Tuanjie XGUID(56-base64) to Unity hex(32)
- fix YAML %TAG namespace (yousandi.cn -> unity3d.com)
- drop tuanjie-only packages/modules and packages.tuanjie.cn registry
- repoint URP / Input System to official versions"
git push -u origin migrate/unity-2022
```

---

## 4. 关于 Git / GitHub（你问的第二件事）

**结论：迁移引擎对 git 和 GitHub 本身零影响。** git 只管文件字节，不关心文件是哪个引擎写的，正常 `commit / push` 继续更新即可。但这几件事必须注意：

1. **先提交再迁**。现在工作区还有一堆未提交改动（新 Dialogue/Flag/TalkAction、三个场景、字体 SDF、开场视频替换），迁移的批量改写一旦和它们混在一起，出问题很难回退。
2. **用分支 + 单独一个 commit** 做迁移，别直接在 `main` 上动手。出事 `git checkout main` 就能全身而退。
3. **diff 会非常大**（6000 个 `.meta` 内容变化 + 185 个 YAML 头部 + 包锁文件），这是正常的，不代表资产坏了——`.meta` 只改了 `guid:` 一行。
4. **不要 `rebase` / `force push`**。保留团结版本的历史。现有两个存档 tag：
   - `pre-unity-migration` → `f317c0c`（**团结原版最后状态**，随时能回去）
   - `pre-tuanjie-repair` → `b1555f0`（Unity 首次导入后的状态 = 回团结修复前的快照）
5. **`.gitattributes` 已补齐**（2026-10-16 新增）。核心是给 Unity 资产加 `-text`，禁止 git 做 CRLF/LF 转换 —— 之前 git 一直在警告 `LF will be replaced by CRLF`，而 Unity 的 YAML 资产一旦行尾被改掉，就会出现"资源反复被标记修改""meta 被判定损坏""合并冲突难解"这类经典坑：
   ```
   *.meta                            -text
   *.unity  *.scene                  -text
   *.prefab *.asset                  -text
   *.mat    *.anim                   -text
   *.controller *.overrideController  -text
   *.playable *.lighting *.preset    -text
   *.asmdef *.rsp                    -text
   ```
   此外图片/音频/视频/dll/字体等标了 `binary`；`*.cs / *.json / *.md` 等只标 `text` 但**不强制 eol**，避免 git 对已有文件做一次性重新归一化、产生大批无意义 diff。
6. **仓库已经 892 MB**（`.git` 里主要是 7 个 33 MB 的字体 SDF 和开场视频的多个版本）。GitHub 单文件上限 100 MB，你最大的 blob 是 33 MB，**目前没超限，可以不改**。以后如果还要进大文件，建议上 Git LFS（只对新文件启用即可，改造历史要用 `git filter-repo`，风险高、不划算）。
7. **`.gitignore` 已扩大为 `[Ll]ibrary*/`**（原为 `[Ll]ibrary/`）。所以 `SourceAssetDB` 与本次挪出的 `Library.bak-from-unity` 都不会被提交；另外新增忽略 `*.bak`、`*.unity-bak`、`Tools/MigrateToUnity/__pycache__/`。`Tools/MigrateToUnity/` 会被提交——迁移完删掉或留住都行。
8. 迁移完成后，`main` 建议只保留 Unity 版本。团结版本和 Unity 版本的 `.meta` 互不兼容，**两边分支不要互相 merge**，只能当两条平行历史。

---

## 5. 第二轮：Project 面板少了很多文件夹 / 全局 using 失效（2026-10-16 补充）

### 症状与根因

用户反馈两个现象：

1. Project 面板里只剩 `Animation` / `Arts` / `Settings` / `TextMesh Pro` / `csc.rsp`，其余文件夹在资源管理器里明明存在；
2. `DialoguePanelController.cs` 里 `MonoBehaviour` 报红，全局 using（`GlobalUsing.cs`）像没生效。

**这两个是同一个根因**：上一轮只归一化了 411 个"被 YAML 引用佐证"的资产，剩下 **5597 个 `.meta` 仍是团结 XGUID 格式**。

而标准 Unity 的行为不是"给非法 GUID 的资产重新分配 GUID"，而是**直接忽略这个资产**——不进 Project 面板、不参与导入、不参与脚本编译。于是：

| 现象 | 直接原因 |
|---|---|
| 10 个一级文件夹消失 | 这些文件夹的 `.meta` 仍是 XGUID（`Animation`/`Arts`/`Settings`/`TextMesh Pro` 恰好是 hex 的，所以只剩它们可见） |
| `MonoBehaviour` 报红 | `Assets/Scripts` 文件夹没被导入 → `Assets/Scripts/Utility/GlobalUsing.cs` 没参与编译 → 全项目的 `global using UnityEngine;` 失效 |
| 资源管理器里文件都在 | Unity 只是没导入，**没有删任何文件**，meta 也原样保留 |

实测证据：
- 当前 meta 分布 `56 位 XGUID: 5597 / 32 位 hex: 1999`；
- 可见的文件夹与"meta 是 hex"的文件夹**完全一一对应**；
- `Assets/Scripts/Battle/Config/BattleCinematicConfig_SO.cs.meta` 已变成 hex（它在 411 个里），但同目录的 `GlobalUsing.cs.meta` 还是 XGUID → 后者没被编译。

### 为什么第一轮会漏

第一轮的工具出于"不误伤"的保守设计，只改写**能被 YAML 引用反向佐证**的资产（411 个），未被引用的 5485 个保持原样——当时的判断是"没被引用就不影响"。
这个判断**在引用层面成立，在导入层面不成立**：Unity 宁可忽略资产，也不接受非法 guid。**正确做法是"全量归一化"，一个都不能留。**

另外还发现了两个此前没纳入扫描的引用来源（导致 139 处引用一度没被发现）：

- **场景后缀**：团结引擎的场景后缀是 **`.scene`**，标准 Unity 只认 **`.unity`**（官方文档："Unity 引擎使用 .unity 扩展名；Tuanjie 引擎使用 .scene 扩展名"）。本工程 9 个自研场景全部是 `.scene`（菜单/常驻/战斗/野外/地图），4 个第三方 Demo 才是 `.unity`。
- **`ProjectSettings/`**：`EditorBuildSettings.asset`、`GraphicsSettings.asset`、`QualitySettings.asset` 也在用 guid 引用工程资产（其中 URP 资产恰好已是 hex，渲染管线没受影响）。

### 修复路线

#### 路线 A（推荐·无损）：回团结引擎做一次全量转换

本机已装团结 **2022.3.62t12**（`D:\Unity\2022.3.62t12\Editor\Tuanjie.exe`，正是本工程原本的版本），所以可以无损修复。

> 本节步骤已于 **2026-10-16** 实际执行完毕，工程**已切回官方 Unity 2022.3.62f3c1**（团结已完成全量归一化、场景改名、%TAG 清零）。完整保留步骤，便于回退或在新机器上重做。

1. **关闭正在运行的编辑器**（Unity 或团结都要关，确认进程退干净，包括 Hub 里挂着的实例）。
2. 切换工程配置到团结版：
   ```bash
   python Tools/MigrateToUnity/switch_engine.py --tuanjie
   ```
   它做四件事，**全都不碰 `Assets/` 下的资产**：
   - `ProjectSettings/ProjectVersion.txt` → 团结 `2022.3.62t12` + `m_TuanjieEditorVersion: 1.10.0`；
   - `Packages/manifest.json` 与 `ProjectSettings/PackageManagerSettings.asset` → 从 git tag `pre-unity-migration` 取回**团结版包配置**（含 `inputsystem 1.14.4-t3`、`URP 14.2.0-t1`、`com.unity.modules.infinity`、`textmeshpro 3.0.10`）；
     ⚠️ **这一步不能省**：当前 manifest 是 Unity 版（URP 14.0.12 / InputSystem 1.14.0，且团结专有模块已被删除），不换回去，团结打开时会**包解析失败**；
   - `Packages/packages-lock.json` → 挪成 `.unity-bak`，让团结自己重新解析依赖；
   - 原 Unity 版配置备份到 `Tools/MigrateToUnity/_pkg_backup/`，供 `--unity` 时还原。
3. **把 Unity 建出来的 `Library/` 挪走**（关键保险，不要跳过）：
   ```bash
   mv Library Library.bak-from-unity
   ```
   原因：这个库是 **Unity 重建过的** —— `Library/SourceAssetDB` 已被截断（32MB → 16MB），里面存的是 Unity 视角的 GUID（Unity 还给 `DOTweenSettings.asset` 之类重新分配过 guid）。**团结若复用这个库，`AssetDatabase.AssetPathToGUID` 很可能返回错误的映射，菜单 2 就会把错误的 GUID 写进 `.meta`，越修越坏。** 必须让团结从零重建一份干净的资产数据库。
   - 库约 2.6GB，同盘改名不占额外空间；团结验收通过后可安全删除 `Library.bak-from-unity`。
   - `.gitignore` 已改为 `[Ll]ibrary*/`，不会再被 git 跟踪。
4. 用 Tuanjie Hub 打开工程，或直接命令行启动（**首次全量导入会很慢，耐心等 Console 平静**）：
   ```bash
   "D:\Unity\2022.3.62t12\Editor\Tuanjie.exe" -projectPath "D:\Unity\Project\CodePath Traveler"
   ```
   转换脚本已就位在 `Assets/Editor/TuanjieGuidConverter.cs`，团结编译完成后菜单栏出现 `Tools/迁移到Unity`。
5. 菜单 `Tools/迁移到Unity` 依次执行，**顺序不要乱**：
   - `0 导出对照表`（可选）→ 写出「资产路径 → 真实GUID」的 tsv，便于事后核对；
   - `1 体检` → 记录基线（此时应报 XGUID **5597** 个）；
   - `2 全量归一化 GUID` → 核心步骤，跳过已是 hex 的，把剩余 5597 个全部用引擎自己的 `AssetDatabase.AssetPathToGUID` 写回真实 GUID；
   - `3 把 .scene 场景改名为 .unity` → 用 `AssetDatabase.MoveAsset`，**GUID 与所有引用保持不变**（应为 9 个）；
   - `4 修正 YAML 的 %TAG 命名空间` → **必须放最后**，否则会被引擎重新写回 `yousandi.cn`；
   - `5 验收` → 断链数应降到"几十个以内"（剩下的都是第三方插件内部断链与 Unity 内置占位 GUID）。
6. **关闭团结**（确认进程退干净），然后在**引擎关闭状态**下做收尾——顺序很重要：
   1. **先复查 XGUID**，团结关闭时可能把你在外部改过的 meta 写回去（本次实测：补视频 GUID 那步是在团结开着时做的，它重导入后**又写回了 XGUID**）：
      ```bash
      python Tools/MigrateToUnity/fix_video_meta.py     # 会顺带打印全工程 XGUID 残留
      ```
      只要还有 56 位 guid，就重跑一次；`fix_video_meta.py` 的备份已改到 `Tools/MigrateToUnity/_bak/`，不会再污染 `Assets/`。
   2. 切回 Unity 的工程配置（会自动删掉团结重新生成的 `packages-lock.json`）：
      ```bash
      python Tools/MigrateToUnity/switch_engine.py --unity
      ```
   3. **清掉团结专有/重建物**：
      ```bash
      mv ProjectSettings/AutoStreamingSettings.asset       Tools/MigrateToUnity/_bak/   # 团结专有设置
      mv ProjectSettings/AutoStreamingSettings.asset.meta  Tools/MigrateToUnity/_bak/   # 若存在
      mv Library Library.bak-from-tuanjie                                              # 团结重建的库
      ```
      `Library.bak-from-tuanjie` 必须挪走：它是团结格式的 `SourceAssetDB`，Unity 读它有拿错 GUID 的风险。让 Unity 自己从零重建。两个 `Library.bak-*`（约 2.6GB）在验收通过后都可以删。
   4. 磁盘终检（**不依赖 Library**，先跑这个确认 XGUID/场景/%TAG 全部达标）：
      ```bash
      python Tools/MigrateToUnity/verify_after_convert.py --report Tools/MigrateToUnity/verify_report.txt
      ```
      ⚠️ 此时第【4】节的"断链"数字会**虚高**：`Library/` 不在位，脚本读不到 `Library/PackageCache`，会把包内引用一并算成断链（24 → 134）。脚本会显式打印这条警告；**真实值等 Unity 重建 `Library` 后**再跑一次即可。
7. 打开 Unity（首次会全量重建 `Library`，慢）。验收通过后，如果还想拿到可信的断链数字，再跑一次：
   ```bash
   python Tools/MigrateToUnity/verify_after_convert.py
   ```
   确认：Project 面板文件夹全部回来、`Assets/Scripts` 参与编译、Console 无编译错误。最后把 `Assets/Editor/TuanjieGuidConverter.cs` 与 `Tools/MigrateToUnity/` 删掉或留着都行。

> 万一想中途放弃：`python Tools/MigrateToUnity/switch_engine.py --restore` 可把 `ProjectVersion.txt` 与包配置一次性还原回 Unity 版。

> 为什么非得回团结：`.meta` 里的 XGUID 是 base64 **加密**格式（官方原话"为了与 Unity 彻底割裂而设计"），实测穷举"字节/半字节/位反转/固定 XOR 掩码"都推不出真实 GUID——**真实 GUID 只存在于团结的资产数据库里**。而 Unity 打开工程时已经把 `Library/SourceAssetDB` 重写掉了，离线反推的路已经断了。

#### 路线 B（快·有损）：直接给剩余 meta 分配新的唯一 GUID

```bash
python Tools/MigrateToUnity/fix_remaining_guids.py            # 试运行，看报告
python Tools/MigrateToUnity/fix_remaining_guids.py --apply    # 真正写
```

- 未归一化的 5597 个资产**绝大部分没有任何引用指向它们**，随便给个唯一 GUID 完全无害 → 文件夹立刻可见、脚本立刻参与编译。
- **代价**：约 **139 处"只被场景文件引用"的引用会永久丢失**（示例：`Assets/Scenes/Persistent/Persistent.scene`、`Battle01.scene` 等引用的一批资产），打开场景会出现 Missing，需要手工重连。因为这些 GUID 对应的真实资产无法再确定，**做了这一步就再也回不去路线 A**。
- 脚本自带保护：断链数必须与补齐前完全一致，否则拒绝执行；新 GUID 绝不占用任何被引用过的 GUID。

### 路线 A 执行结果（2026-10-16 实测，已完成）

| 检查项 | 迁移前 | 执行后 | 结论 |
|---|---|---|---|
| XGUID `.meta` | 5597 个（另有 2001 个已是 hex） | **0 个** | ✅ 全部归一化 |
| `.scene` 场景 | 9 个 | **0 个**（全部变成 `.unity`） | ✅ 改名成功且 GUID 保留 |
| `%TAG yousandi.cn` | 185 个文件 | **0 个**（1043 个 `unity3d.com`） | ✅ 已清零 |
| YAML 断链 GUID | 436（迁移前预估） | **24 个**（另 3 个 Unity 内置占位） | ✅ 且 24 个全是历史遗留 |
| `ProjectSettings/` 引用 | — | 全部可兑现 | ✅ 无断链 |

**唯一需要手工收尾的 1 处**：`Assets/Arts/Video/开场动画_low.mp4.meta`。菜单 2 在 `AssetDatabase.StartAssetEditing()` 期间调用 `AssetPathToGUID`，对这个 15MB 视频返回了空（此时它尚未导入完），于是被记为「跳过·无GUID」。

已经用 `Tools/MigrateToUnity/fix_video_meta.py` 修好，真实 GUID `d7152faeb93d385469c7be09e514fcf1` 由**两条独立证据交叉确认**：

- (a) `Assets/Scenes/Menu/Menu.unity` 的 `VideoPlayer.m_VideoClip` 引用的就是它；
- (b) 团结重建的 `Library/SourceAssetDB` 中，该路径前的 16 字节经 `nibble_swap` 后正是同一个值。

> 若以后还要处理"被引擎跳过的资产"，可以照这个套路：**引用方 + SourceAssetDB 双向确认 GUID**，再直接改写 `.meta`。注意跑完要把备份移出 `Assets/`（否则会被引擎当资产导入）。

**那 24 个断链全部是迁移前就存在的历史遗留**，已逐条用 `git show pre-unity-migration:<路径>` 比对确认引用值一字未变：

| 位置 | 断的东西 | 说明 |
|---|---|---|
| `Assets/Prefabs/VFX/fire_red.prefab`、`star_yellow.prefab` | sprite `9c475053...` | 源插件 Lana Studio 里该 sprite 已不存在 |
| `Persistent.unity` | `m_LightingDataAsset 655f5c63...`、`m_LightingSettings 8aeea57b...` | 迁移前 Persistent 目录下就没有 `LightingData.asset` |
| `Battle03`、`Field03` 场景 | `m_LightingDataAsset 655f5c63...` | 这两个场景目录从未有过 `LightingData.asset` |
| `Town01_Day.unity` | `1205127b...` | 已知历史失效引用 |
| 其余 18 个 | — | 全在第三方插件内部（JMO Assets / Lana Studio / Hovl Studio） |

**顺带修掉一个工具缺陷**：`TuanjieGuidConverter.cs` 里菜单 3（场景改名）与菜单 4（%TAG）的 `[MenuItem]` 标签与方法体**错位**了 —— 点"3 场景改名"实际执行的是 %TAG，点"4 %TAG"才是场景改名。功能都正常（用户当时两个都点了），只是名字对不上，已把两个标签互换纠正，`Assets/Editor/` 与 `Tools/MigrateToUnity/` 两份保持一致。

### 顺带确认的两件事

- `Assets/csc.rsp` 内容为 `-langversion:10.0`。Unity 2022.3.62 系列**编译侧支持**（社区已有同版本 2022.3.62f3c1 的实践），所以 Unity 编译不会报 CS1617。若 IDE 里仍报红，那是 Unity 生成的 `.csproj` 里 `<LangVersion>9.0</LangVersion>` 导致的（Unity 与 IDE 的语言版本不同步），与本次迁移无关，可用 CsprojModifier 或改写 csproj 解决。
- `Assets/Resources/DOTweenSettings.asset.meta` 的 guid 已被 Unity 换成新值（`557ab6fb...`）。该资产走 `Resources.Load` 加载，不依赖 GUID，无需处理。

### 收尾：团结关闭时会写回 XGUID（2026-10-16 实测，已修）

补视频 GUID 那一步是在团结**仍开着**的时候做的，团结重导入该资产后按自己数据库重新序列化 meta，**又把它的 guid 写回了 XGUID**（关闭后再查：7598 个 meta 里有 1 个是 56 位）。所以：

> **凡是在团结之外改写过的 `.meta`，必须在团结完全退出后复查一次；补写步骤一定要放在"关掉团结"之后。**

本次处理（引擎已关闭状态下完成）：
1. 重跑 `fix_video_meta.py`，值仍为 `d7152faeb93d385469c7be09e514fcf1`（脚本从 `Menu.unity` 的 `m_VideoClip` 读权威值；我在 `Library/SourceAssetDB` 里也再次交叉验证了同一值）。备份改到 `Tools/MigrateToUnity/_bak/`，不再落进 `Assets/`。
2. `switch_engine.py --unity`：ProjectVersion 回 `2022.3.62f3c1`、包配置恢复 Unity 版、删掉团结重新生成的 `packages-lock.json`。
3. 清掉团结专有/重建物：
   - `ProjectSettings/AutoStreamingSettings.asset`（+meta）→ `Tools/MigrateToUnity/_bak/`
   - 团结重建的 `Library/` → `Library.bak-from-tuanjie`（**必须挪走**，否则 Unity 读团结格式的 `SourceAssetDB` 有拿错 GUID 的风险；同盘改名不占空间，验收通过后可删）
4. 终检结果：`meta 7598 个全部 32 位 hex（XGUID 0）`、`.scene 0 / .unity 19`、`%TAG yousandi 0`、manifest 无 `-t1/-t3` 与团结专有模块。

> ⚠️ 验收脚本的已知假警报：`Library/` 被挪走后，`verify_after_convert.py` 读不到 `Library/PackageCache`，第【4】节的"断链"会把**包内引用一并算进去**（24 → 134）。脚本现在会显式打印这条警告。真实值以 `Library` 在场时为准（本工程实测 **24 处，且全为迁移前历史遗留**）。



## 6. 迁移后实测：两个真实问题与修复（2026-10-16）

用 Unity 打开工程后暴露了两个问题，都已定位并修复。

### 6.1 `Wipe Shader` 在场景里显示洋红（团结私有的 Canvas 子目标）

**现象**：`Assets/Martials/Wipe Shader.mat` 在 Persistent 场景的 `RawImage` 上是洋红。

**证据**（`%LOCALAPPDATA%\Unity\Editor\Editor.log`）：
```
Assets/Martials/Wipe Shader.shadergraph: Import Error: Expected active subtarget not found, defaulting to first available.
WARNING: Shader Unsupported: 'Shader Graphs/Wipe Shader' - All subshaders removed
```

**根因**：这个 ShaderGraph 的 target 是
`UnityEditor.Rendering.Universal.ShaderGraph.UniversalTarget` + **`UniversalCanvasSubTarget`**，
另带 `UnityEditor.Rendering.Canvas.ShaderGraph.CanvasData`。
`UniversalCanvasSubTarget` 是**团结引擎 URP（`14.2.0-t1`）私有**的 UI/Canvas 子目标；
标准 Unity 的 URP 14.0.12 里只有 Lit / Unlit / Decal / Fullscreen 四个子目标，

```bash
grep -rn "class Universal.*SubTarget" Library/PackageCache/com.unity.render-pipelines.universal@14.0.12/Editor/ShaderGraph/
# UniversalDecalSubTarget / UniversalFullscreenSubTarget / UniversalLitSubTarget / UniversalUnlitSubTarget —— 没有 Canvas
```

ShaderGraph 14.0.12 里也**没有** `UnityEditor.Rendering.Canvas` 命名空间。
所以导入时找不到子目标 → 生成的 Shader 没有可用 SubShader → 材质显示洋红。**Unity 侧无解**（除非整体升到 Unity 6 / URP 17，不划算）。

**修复**：把这个图用手写 HLSL **等价复刻**成 UGUI 兼容的 UI 着色器
`Assets/Martials/Wipe Shader.shader`（`Shader "UI/Wipe Shader"`），材质改指向它。

原图的逻辑（从 shadergraph JSON 里逐条解析出来的连接）：

```
Mask (Texture2D) ──R──┐
                       ├─ Step ─► One Minus ─► Alpha
_Progress ──Edge ──────┘
_OverlayColor ─────────────────────────────► Base Color
```

即 `alpha = 1 - step(_Progress, tex2D(_Mask, uv).r)`、`rgb = _OverlayColor.rgb`：
`_Progress` 0→1 时遮罩红通道较小处先显形 —— 擦拭式转场。
`FadeController.cs` 就是靠 `material.SetFloat("_Progress", 0..1)` 驱动它的
（`FadeOut` → 1 盖住屏幕，`FadeIn` → 0 揭开）。

**关键点**：属性名与原图完全一致（`_Mask` / `_Progress` / `_OverlayColor`，
外加 UGUI 标准的 `_Stencil*` / `_ColorMask` / `_ClipRect` / `_UIMaskSoftnessX,Y`），
所以材质里已有的取值全部沿用，**不需要在 Inspector 里重连任何东西**。
新 shader 的 guid `1c8b799afaeb12cd71f05005bc3c565e`，材质 `m_Shader` 已指向 `fileID 4800000` 的它。

> 若擦拭方向上下相反：把 `_Mask` 的 `Tiling.y` 设为 `-1`、`Offset.y` 设为 `1`。
> 旧的 `.shadergraph` 已无人引用，但它仍会在 Console 报一句 import error —— 可以放心删除（本次没有替你删）。

### 6.2 Map 场景选人后 `NullReferenceException`（引用指向了文件夹）

**现象**：Map 场景选人 →
```
NullReferenceException
  AllyDefinition_SO.GetStatForLevel (Int32 level) (at AllyDefinition_SO.cs:64)
  CharacterRuntimeData.GetBaseStats ()            (CharacterRuntimeData.cs:44)
  CharacterRuntimeData..ctor (CharacterDefinition_SO definition)
  PartyManager.SetLeader (CharacterDefinition_SO definition)   (PartyManager.cs:67)
```
第 64 行第一句就是 `globalGrowthConfig_SO.GetCurveByRank(...)` → `globalGrowthConfig_SO` 为 null。

**根因**：9 个角色资产（8 个 Ally + NPC 的 Gareth）里
`globalGrowthConfig_SO: {fileID: 11400000, guid: 9748ad3663f984547a76962a3dde3995}`，
而 `9748ad36…` 是**文件夹** `Assets/Game Data/Growth Config` 的 guid
（它的 meta 里有 `folderAsset: yes`）。引用 `fileID 11400000` 是 ScriptableObject 的主对象 id，
却挂在一个文件夹的 guid 上 → 解析为 null。

真正的配置资产是 `Assets/Game Data/Growth Config/BaseGrowthConfig SO.asset`
（guid `182c10d97549557498e8fb3acc515bfc`，主对象确实就是 `&11400000`），
而它在全工程**没有任何引用**。

**修复**：把 9 个资产里该字段的 guid 改成 `182c10d97549557498e8fb3acc515bfc`（功能上等价且已验证有效）。
> ⚠️ **2026-10-16 晚更正**：当时把成因判为"迁移前就存在"是**错的**。
> 依据 `guid_report_mapping.tsv` 下这个结论属于循环论证 —— 那份映射表正是**有 bug 的
> 离线脚本**自己产出的（见 6.4），它写的"文件夹 → `9748ad36…`"就是 bug 的结果，不能用来证明
> "迁移前如此"。真实情况是：`9748ad36…` 本来是 `BaseGrowthConfig SO.asset` 的真实 GUID，
> 被脚本错写到了父文件夹上，9 个引用其实一直是对的。本例按"改 YAML 引用"修好了；
> 6.4 的 13 处则改用"把 GUID 还给子资产"修，两种做法效果等价。

### 6.3 顺带做的一次权威复核：GUID 归一化 100% 正确

借这次机会，用**记录长度字段**严格解析团结的 `SourceAssetDB`，把 7598 个 meta 全部核对了一遍
（`Tools/MigrateToUnity/audit_meta_vs_db2.py`）：

```
与 DB 一致 : 7598      不一致 : 0      DB 中无此路径 : 0
```

> ⚠️ **踩过的坑（写工具时注意）**：早期版本的审计脚本用「从 `00 00 10 00` 之后取可打印 ASCII 串」来读路径，
> 遇到**中文路径会在第一个汉字处截断**（如 `Assets/Arts/Video/开场动画_low.mp4` 被截成 `Assets/Arts/Video`），
> 于是把子文件的 guid 误判给父目录，**假报 11~14 个"目录 guid 不一致"**，看起来像"文件夹和文件 guid 被互换"。
> 正确做法：记录头前面还有 4 字节长度（`[4B LE 长度 = 路径字节数 + 1][00 00 10 00][16B GUID][路径]`），
> **按长度精确切路径**，并且用 `setdefault`（首个命中优先）时务必确保路径未被截断。

> ⚠️ **2026-10-16 晚更正（这条结论本身有盲区）**：这次"零差异"**不能证明文件夹 guid 正确**。
> 两个原因：
> 1. 比对对象是 `Library/SourceAssetDB`，而那份 DB 在跑完转换后已被引擎重写，
>    它记录的是"转换后的现状"，meta 与它自然自洽 —— 用现状验证现状。
> 2. 更隐蔽的一点：**文件夹确实被写错了 guid**（拿走了子资产的 GUID），
>    只是因为"文件夹 + 子资产"这组错误在 DB 与 meta 里同时存在，审计照不出来。
>
> 真正能定案的判据是 **YAML 引用**（转换器没动过 YAML，它是历史真值）：
> 「每个被引用的 hex GUID，必须能被某个 .meta 兑现，且兑现者不能是文件夹」。见 6.4。

### 6.4 进入游戏场景弹窗 `Opening file failed … 拒绝访问`（13 个文件夹抢走了子资产的 GUID）

**现象**：Map 场景选完人物、开始加载首个游戏场景（`Town01_Day`）时，弹出一个 Windows 对话框：

```
Opening file failed
Opening file D:/Unity/Project/CodePath Traveler/Assets/Scenes/Town: 拒绝访问。
                                          [Try Again] [Force Quit] [Cancel]
```

`Assets/Scenes/Town` 是**文件夹**。Windows 打开目录当文件读就是 `ACCESS_DENIED`，
所以本质是：**某处引用把文件夹当成资产去加载了**。

**证据链**（Editor.log，`%LOCALAPPDATA%/Unity/Editor/Editor.log`）：

```
Loaded scene 'Assets/Scenes/Map/Map.unity'
Cancelling DisplayDialog because it was run from a thread that is not the main thread:
    Opening file failed Opening file D:/.../Assets/Scenes/Town: 拒绝访问。
Could not open file D:/.../Assets/Scenes/Town for read
Assertion failed on expression: 'res'
Failed to load 'D:/.../Assets/Scenes/Town'. File may be corrupted ...
The referenced script (Unknown) on this Behaviour is missing!     ← ×10
Loaded scene 'Assets/Scenes/Town/Town01_Day/Town01_Day.unity'     ← 场景本身还是加载上了
```

场景文件里那一行是：

```yaml
Assets/Scenes/Town/Town01_Day/Town01_Day.unity:101
  m_LightingDataAsset: {fileID: 112000002, guid: 7de4bf1556e61f447bb8be02d46f6342, type: 2}
```

而 `7de4bf15…` 是**文件夹 `Assets/Scenes/Town` 的 .meta guid** → Unity 把"文件夹"当 Lightmap 数据资产去打开。

**根因：离线脚本的"子串路径匹配"把子资产的 GUID 写到了父文件夹身上。**
`Tools/MigrateToUnity/tuanjie_guid_normalize.py` 的 `db_candidates()`：

```python
i = data.find(rel_path.encode())      # ← 子串搜索，不是精确匹配
g = nibble_swap(data[i-16:i].hex())   # 取紧邻路径前的 16 字节当作该资产 GUID
```

文件夹路径**是它所有子路径的前缀**，所以查 `Assets/Scripts/Game/Party` 时，
会命中 `Assets/Scripts/Game/Party/FieldFollower.cs` 的记录，取到的是**子脚本的真实 GUID**。
脚本随后把该值写进了**父文件夹的 .meta**（`--convert`）。后果：

1. 子资产的真实 GUID 被文件夹占用 → 子资产自己只能拿一个引擎新分配的值；
2. 全工程原本**完全正确**的引用（`m_Script` / `Clip` / `m_LightingDataAsset` / `weakIconPrefab` …）
   现在指向文件夹 → 脚本丢失、音频丢失、光照数据打不开，最终就是那个弹窗。

**这不是"历史断链"**：脚本只会挑 `furnishable`（被 YAML 引用过、且尚无 hex meta 认领）的值，
所以每一处"碰撞"必然是"YAML 引用子资产 → 值被父文件夹抢走"，不是巧合、也不是作者写错。

**全工程扫描**（`Tools/MigrateToUnity/scan_ref_to_folder.py`）：共 **22 处** 引用指向文件夹，
去重后是 **13 个文件夹**：

| 被抢走 GUID 的文件夹 | 真正应该拥有它的子资产 |
|---|---|
| `Assets/Scenes/Town` | `Town01_Day/LightingData.asset` ← **本次弹窗的直接原因** |
| `Assets/Scripts/Game/Interaction` | `InteractionBase.cs`（Town 场景里 11 个组件的脚本） |
| `Assets/Scripts/Game/Party` | `FieldFollower.cs`（`Follower.prefab` 的脚本） |
| `Assets/Scripts/UI/Field Panel` | `ActionMenuButton.cs` |
| `Assets/Prefabs/UI/Battle` | `Weakness Prefab.prefab`（`Weakness Bar.prefab` 的 `weakIconPrefab`） |
| `Assets/Game Data/Audio/Skill` | `蹦蹦炸弹.asset`（技能的 `castSound`） |
| `Assets/Arts/Audio/BGM/Battle` `/Menu` `/Town` | `Battle_01.mp3` / `Menu.mp3` / `Town_01.mp3` |
| `Assets/Arts/Audio/SFX/Battle/Battle_Defeat` `/Battle_Victory`、`SFX/UI` | `超级马里奥 - gameover.mp3` / `多啦A梦开场.wav` / `按钮类6－mcx20070509.wav` |
| `Assets/AddressableAssetsData/AssetGroupTemplates` | `Packed Assets.asset`（Addressables 组模板） |

**修复**（`Tools/MigrateToUnity/fix_folder_guid_theft.py`，默认 dry-run，`--apply` 执行）：

* 把 GUID **从文件夹还给真正的子资产**（还原历史身份，YAML 一个字都不动）；
* 文件夹换一个全新的、没被任何 YAML 引用过的 GUID；
* 执行前逐个校验"子资产的旧 GUID 无人引用"，避免改出新的断链；
* 26 个被改的 `.meta` 全部备份到 `Tools/MigrateToUnity/_bak/`，`git checkout` 也能整批回滚。

**验收**：

```
scan_ref_to_folder.py          → 发现 0 处【指向文件夹】的引用
verify_refs.py                 → YAML 引用 1598 个 GUID，无法兑现 39 个
```

剩下 39 个全是**第三方插件内部**的历史断链（JMO Assets / Lana Studio / Hovl Studio 的
Effect 材质与 shader、URP Settings 的 Renderer Feature 脚本），与迁移前的
`guid_verify_report.txt` 完全一致，属于"迁移前既有、可忽略"的那一类。

> **顺带确认**：`Town01_Day.unity` 的 `m_LightingDataAsset` 写法（`fileID: 112000002`
> + 资产 meta 里 `mainObjectFileID: 6475696996188705980`）和工程里其它"健康"场景
> （Battle01/Battle02/Field02）完全同构，说明修好后它已经回到正常形态；
> 只有 `Town01_Night` 用的是旧的 `112000000` 写法，两种都能用，无需统一。

> **另外两处**：`Battle03` / `Field03` / `Persistent` 的 `m_LightingDataAsset` 指向
> `655f5c6340e1cce45bfd953c28cc6d7f`，工程里已经没有对应的 `LightingData.asset`
> （这三个场景当年烘焙数据被删过）—— 属迁移前既有断链，本次未动。

### 6.5 进入场景时 Console 报 `Invalid version provided … got t`（烘焙数据里写着团结版本号）

**现象**：**每次首次**进入某个场景时，Console 都会出现一条报错（同一次会话里第二次加载同一份烘焙数据会重复报）：

```
Invalid version provided - release type(a|b|f|p|x) or end of string expected, got t
```

它不影响游玩 —— 关掉暂停就能正常进场景 —— 纯粹是 Unity 解析版本串失败时打的日志。

**根因**：这几个场景的**烘焙数据资产**（Lighting Data / 场景烘焙主 asset）是**团结引擎**烘出来的，
它们的二进制头里写着团结的引擎版本 `2022.3.62t12`。而 Unity 解析版本号时只认 release type
`a|b|f|p|x`，读到 `t` 就报上面那句。

```
Invalid version provided - release type(a|b|f|p|x) or end of string expected, got t
LightingSettings: switching bake backend from 1 to 2.
Loaded scene 'Assets/Scenes/Town/Town01_Day/Town01_Day.unity'     ← 报错紧跟在加载之前
```

版本串出现的位置（两处，都要改）：

| 位置 | 字节形态 |
|---|---|
| SerializedFile 头 `0x30`（偏移固定） | `…\x00 2022.3.62t12 \x00\xfe\xff\xff\xff…` |
| `LightingData.asset` 内嵌的 UnityFS 子块 | `…\x00UnityFS\x00\x00\x00\x00\x08 5.x.x\x00 2022.3.62t12\x00…` |

> `UnityFS\0` 后面那个 `08` 是**大端** u32 的 version 字段，与版本串无关，别去动它。

**为什么必须"等长替换"**：版本串前面是头字段、后面紧跟 `\0` 与定长数据，而 header 里记录的是
**绝对偏移**。长度只要变一个字节，后面所有数据全部错位 → 资产直接损坏。
所以只能做「同长度、同位置」的字节替换。

**为什么只有首次进场景才报**：烘焙数据只在场景首次加载时被读入并解析一次；之后它就在内存里了，
不再走解析路径。

**受影响文件（6 个，共 12 处）**：

| 文件 | 处数 | 说明 |
|---|---|---|
| `Assets/Scenes/Town/Town01_Day/LightingData.asset` | 2 | 头 + 1 个 UnityFS 子块 |
| `Assets/Scenes/Battle/Battle01/Battle01/LightingData.asset` | 3 | 头 + 2 个 UnityFS 子块（`Field01` 复用它会二次报错） |
| `Assets/Scenes/Battle/Battle02/Battle02/LightingData.asset` | 3 | 头 + 2 个 UnityFS 子块 |
| `Assets/Scenes/Field/Field02/Field02/LightingData.asset` | 2 | 头 + 1 个 UnityFS 子块 |
| `Assets/Scenes/Battle/Battle01/Battle.asset` | 1 | 场景烘焙主 asset |
| `Assets/Scenes/Field/Field02/Field02.asset` | 1 | 场景烘焙主 asset |

> `Assets/Scenes/Town/Town01_Night/LightingData.asset` 写的是 `2022.3.8f1c1`
> —— `f` 是合法 release type（`c1` 是 Unity 中国版后缀，解析器认），**不报错，无需处理**。
> 同理 `Assets/Plugins/Hovl Studio/…/LightingData.asset`（`2020.3.18f1` / `2018.1.9f2`）也正常。

**修复**（`Tools/MigrateToUnity/fix_asset_version_string.py`，默认 dry-run，`--apply` 执行，`--restore` 回滚）：

```
2022.3.62t12  →  2022.3.62f03
└ y.m.p 原样 · release 换成合法的 f · build 零填充保持同宽(12→03)
```

* 选 `f03` 而不是 `f12`：`f03` 长度为 12（与原串等长）、build = 3 与编辑器 `2022.3.62f3c1` 一致，
  不会被 Unity 误判为「更新的 Unity 版本生成的资产」；
* 备份写到 `Tools/MigrateToUnity/_bak/assetver__*.bak`。

**验收**（`scan_asset_version.py` + 逐字节比对备份）：

```
全工程非法 release type 扫描   → 0 处（资产侧清零）
6 个文件长度全部不变
每处差异恰好 3 个字节：74(t)→66(f)、31(1)→30(0)、32(2)→33(3)
```

> 剩下的 `2022.3.62t12` 只出现在 `Docs/*.md`、`.workbuddy/memory/*.md` 和
> `Tools/MigrateToUnity/*.py` 的文字里，属正常记录。

### 6.6 迁移收尾清理（2026-10-16）

#### A. ProjectSettings 的团结 %TAG 残留（已修）

之前只处理了 `Assets/` 下的 185 个资产 YAML，**漏掉了 `ProjectSettings/`**。
那里有 **20 个文件**仍写着团结的 YAML 命名空间：

```yaml
%TAG !u! tag:yousandi.cn,2023:      →      %TAG !u! tag:unity3d.com,2011:
```

`AudioManager` / `ClusterInputManager` / `DynamicsManager` / `EditorSettings` / `GraphicsSettings` /
`InputManager` / `MemorySettings` / `NavMeshAreas` / `Physics2DSettings` / `PresetManager` /
`ProjectSettings` / `QualitySettings` / `TagManager` / `TextureManagerSettings` / `ThreadInfoSettings` /
`TimeManager` / `URPProjectSettings` / `UnityConnectSettings` / `VFXManager` / `VersionControlSettings`
各 1 处，等长替换（新旧都是 30 字节）。**修复后 `grep -r yousandi ProjectSettings/` = 0。**

#### B. 已清理的迁移临时文件（约 7.0 GB，移入回收站）

| 项目 | 体积 | 说明 |
|---|---|---|
| `Library.bak-from-unity/` | 2.6 GB | 切引擎前挪走的 Unity 库，Unity 会按需重建 |
| `Library.bak-from-tuanjie/` | 4.4 GB | 切引擎前挪走的团结库 |
| `Tools/MigrateToUnity/` | 5 MB | 全部迁移脚本、报告、`_bak/`（46 个备份）、`_pkg_backup/` |
| `Tools/`（空目录） | — | 随上一条一并清理 |
| `guid_convert_log.txt` / `guid_map.tsv` / `guid_normalize_report.txt` / `guid_verify_report.txt` | 2 MB | 根目录的迁移过程报告 |
| `ProjectSettings/ProjectVersion.txt.bak` | 90 B | 内容与当前文件完全相同，切换脚本残留 |
| `Assets/Editor/TuanjieGuidConverter.cs` (+meta)、`Assets/Editor/`、`Assets/Editor.meta` | 10 KB | 团结引擎内跑的转换器；Unity 中已无用，其自身注释亦写明"迁移完成后删除本文件" |

**保留了迁移成果**：`Assets/Martials/Wipe Shader.shader`、11 个 `.unity` 场景、
`Docs/Migrate to Unity.md`、`Packages/` 下的 Unity 版包配置。

**恢复方式**：对象都在 **D 盘回收站**（配额约 48.6 GB，删除时占用约 7 GB），可直接还原；
`.meta` / `ProjectSettings` / `.scene` 等改动过的文件也能从 git 恢复（tag `pre-unity-migration` / `pre-tuanjie-repair`）。

> ⚠️ 移到回收站**不释放磁盘空间** —— 要等清空回收站后空间才真正归还。

#### C. 仍保留的团结专有文件（无害，未动）

`ProjectSettings/ThreadInfoSettings.asset`（classID **9625**，含 `m_HMIAndroidPlatformThreadInfoSettings`、
`m_EmbeddedLinuxPlatformThreadInfoSettings`）与 `ProjectSettings/TextureManagerSettings.asset`
（classID **444**，含 `m_ifUseRemoteLoad` / `m_UnityCDNRoute` 等 CDN 远程加载配置）。

这两个文件名不在 Unity 的 ProjectSettings 白名单里，**Unity 直接忽略、不读、不报错**，故保留。

---

## 7. 风险与工作量

| 风险 | 严重度 | 应对 |
|---|---|---|
| **只归一化"被引用的资产"→ 大量文件夹在 Unity 里不可见、脚本不参与编译** | **高（本次已踩）** | **必须全量归一化，一个 XGUID 都不能留**；用路线 A 或 B |
| **团结场景后缀 `.scene`，Unity 不认** | **高（本次已踩）** | 菜单 4 一键改名 `.unity`（保留 GUID） |
| GUID 归一化写错变体 → 引用依旧断 | 高 | 用"验收菜单 / 体检脚本"立刻能看出来；git 可回滚 |
| 少量历史遗留断链（本工程实测 24 处，全为迁移前既有） | 低 | 迁移后在 Unity 里手动 Reassign |
| **团结私有 ShaderGraph 子目标**（`UniversalCanvasSubTarget` 等 UI/Canvas target）在 Unity 里无解 | **中（本次已踩）** | 改写成等价的手写 UGUI 着色器（见 6.1） |
| URP 14.2.0-t1 → 14.0.12 的 shader 差异 | 中 | 迁移后逐一检查材质，尤其 Toony Colors Pro / KinoBloom / 水体 shader |
| Input System 降级导致的输入配置差异 | 低 | 重设 `Active Input Handling` 与 Input Actions Asset |
| Addressables 分组/模板断链 | 低 | 迁移后 Build 一次，报错就重新指定模板 |
| 丢失微信小游戏 / 鸿蒙等团结特有管线 | 中 | 如果这是刚需，需要重新评估是否真要迁回 Unity |

**工作量预估**：全量归一化 + 配置约 1–2 小时（团结首次全量导入另需等待）；Unity 首次导入 + 验收半天。**代码本身预计 0 行改动。**

---

## 附：本次生成的文件

> ⚠️ **2026-10-16 收尾清理**：`Tools/MigrateToUnity/` 整个目录（含下列全部脚本与报告）
> 已移入回收站，详见 6.6。下表保留为迁移过程的完整记录。

| 文件 | 用途 |
|---|---|
| `Tools/MigrateToUnity/TuanjieGuidConverter.cs` | 在团结引擎内执行的转换器（0 导出对照表 / 1 体检 / 2 全量归一化 / 3 场景改名 / 4 修 %TAG / 5 验收）；已拷一份到 `Assets/Editor/` 备用 |
| `Tools/MigrateToUnity/switch_engine.py` | 团结 ↔ Unity 切换助手（`--tuanjie` / `--unity` / `--status` / `--restore`）：切 `ProjectVersion.txt`、切包配置（manifest + PackageManagerSettings）、挪走/删除 `packages-lock.json` |
| `.gitattributes`（仓库根目录） | 禁止 git 对 Unity 资产（`*.meta/*.unity/*.scene/*.prefab/...`）做换行转换，防止 YAML 与 meta 被 CRLF 改坏 |
| `Tools/MigrateToUnity/tuanjie_guid_normalize.py` | 离线体检 + 归一化工具（带 dry-run、报告、映射表导出；⚠️ 只覆盖被引用的资产） |
| `Tools/MigrateToUnity/fix_remaining_guids.py` | 补齐剩余 XGUID 为唯一新 GUID（路线 B 用；含断链来源分布报告，默认 dry-run） |
| `Tools/MigrateToUnity/guid_report.txt` | 第一轮体检报告 |
| `Tools/MigrateToUnity/guid_report_mapping.tsv` | 第一轮可确定归一化的「资产路径 → 原 GUID → 新 GUID」对照表（411 条） |
| `Tools/MigrateToUnity/fix_guids_report.txt` | 第二轮体检报告（剩余 5597 个 XGUID 与断链来源分布） |
| `Tools/MigrateToUnity/verify_after_convert.py` | 转换后**纯磁盘**独立验收（meta 长度分布 / 场景后缀 / %TAG / 断链明细）。⚠️ `Library` 不在位时第【4】节数字会虚高，脚本会自己提示 |
| `Tools/MigrateToUnity/verify_db_vs_meta.py` | 用团结 `SourceAssetDB` 反推「引擎权威 GUID」与 meta 现值交叉核对 |
| `Tools/MigrateToUnity/audit_meta_vs_db2.py` | **权威审计（推荐用这个）**：按记录长度字段严格解析 DB，中文路径不会截断 → 本次结果「7598 一致 / 0 不一致」 |
| `Tools/MigrateToUnity/audit_meta_vs_db.py` | 早期版本，保留作对照：⚠️ 读路径时会**在中文处截断**，会假报"目录 guid 不一致"，不要用它下结论 |
| `Tools/MigrateToUnity/fix_video_meta.py` | 补 `开场动画_low.mp4` 被跳过的 guid（备份写到 `_bak/`，不落进 `Assets/`） |
| `Tools/MigrateToUnity/fix_wipe_and_growth.py` | 本轮两个修复脚本（建 shader meta + 改材质指向；改 9 个角色资产的 Growth Config 引用），默认 dry-run |
| `Tools/MigrateToUnity/dump_shadergraph.py` / `dump_sg_edges.py` | 解析 `.shadergraph`：打印节点、插槽、属性、**完整连接**、target/subtarget |
| `Tools/MigrateToUnity/scan_ref_to_folder.py` | **扫描"引用指向文件夹"**（正是"拒绝访问"弹窗的元凶）：输出每一处「谁引用了哪个文件夹 guid」 |
| `Tools/MigrateToUnity/list_folder_collisions.py` | 把上述碰撞逐条铺开：列出文件夹内容 + 每个子资产的 guid，便于判断作者真正想引用谁 |
| `Tools/MigrateToUnity/fix_folder_guid_theft.py` | **修复器**：把被文件夹抢走的 GUID 还给子资产、文件夹换新 GUID；默认 dry-run，`--apply` 落地，备份写 `_bak/` |
| `Tools/MigrateToUnity/verify_refs.py` | 修复后验收：统计 YAML 引用里无法兑现的 GUID（断链）并给明细（比只能看工程内资产的早期版本更准，含 PackageCache） |
| `Tools/MigrateToUnity/db_parse.py` / `db_lookup.py` | 严格解析团结 `SourceAssetDB`：`--path` 查路径真实 GUID、`--guid` 反查 guid 属于哪个路径（按长度字段切路径，中文安全） |
| `Tools/MigrateToUnity/diag_scene_guid.py` | 诊断：核对 Addressables 组条目 / Build Settings 里的 GUID 到底解析到哪个资产 |
| `Tools/MigrateToUnity/scan_asset_version.py` | 全工程扫描资产二进制里的引擎版本串，挑出 release type 非法（团结 `t/t1/t3`）的那些 |
| `Tools/MigrateToUnity/fix_asset_version_string.py` | **修复器**：把 `2022.3.62t12` **等长**替换为 `2022.3.62f03`（消除 `Invalid version provided … got t`）；默认 dry-run，`--apply` 落地，`--restore` 回滚 |
| `Assets/Martials/Wipe Shader.shader` | **替换洋红 ShaderGraph 的手写 UGUI 擦拭着色器**（`Shader "UI/Wipe Shader"`，属性名与原图一致） |
