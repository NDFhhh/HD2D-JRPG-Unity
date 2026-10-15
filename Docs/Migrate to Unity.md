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
4. **不要 `rebase` / `force push`**。保留团结版本的历史，`pre-unity-migration` 这个 tag 就是你随时能回去的存档点。
5. **补一个 `.gitattributes`**（仓库里目前没有，建议加）：
   ```
   *.meta     -text
   *.unity    -text
   *.prefab   -text
   *.asset    -text
   *.mat      -text
   *.controller -text
   *.anim     -text
   ```
   `-text` = 禁止 git 做 CRLF/LF 转换。Unity 的 YAML 资产一旦行尾被 git 悄悄改掉，会出现"资源反复被标记修改""合并冲突难解"等经典坑。
6. **仓库已经 892 MB**（`.git` 里主要是 7 个 33 MB 的字体 SDF 和开场视频的多个版本）。GitHub 单文件上限 100 MB，你最大的 blob 是 33 MB，**目前没超限，可以不改**。以后如果还要进大文件，建议上 Git LFS（只对新文件启用即可，改造历史要用 `git filter-repo`，风险高、不划算）。
7. **`Library/` 已经在 `.gitignore` 里**，所以 `SourceAssetDB`（迁移工具要读的那个文件）不会被提交，`Tools/MigrateToUnity/` 会被提交——迁移完删掉或留住都行。
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

本机已装团结 **2022.3.62t12**（`D:\Unity\2022.3.62t12\Editor\Tuanjie.exe`，正是本工程原本的版本），所以可以无损修复：

1. **关闭 Unity**（确认进程退干净）。
2. 切换工程版本（脚本会自动备份并挪走 `packages-lock.json`）：
   ```bash
   python Tools/MigrateToUnity/switch_engine.py --tuanjie
   ```
   等价于把 `ProjectSettings/ProjectVersion.txt` 手工恢复成团结那版（即 commit `f317c0c` 的内容）：
   ```
   m_EditorVersion: 2022.3.62t12
   m_EditorVersionWithRevision: 2022.3.62t12 (ab02e98c9779)
   m_TuanjieEditorVersion: 1.10.0
   ```
3. 用 Tuanjie Hub 打开工程（**首次会全量重建导入，慢，耐心等**）。转换脚本已经放好在 `Assets/Editor/TuanjieGuidConverter.cs`。
4. 菜单 `Tools/迁移到Unity` 依次执行：
   - `1 体检` → 记录基线（此时应报 XGUID 5597 个）
   - `2 全量归一化 GUID` → 核心步骤，跳过已是 hex 的，把剩余 5597 个全部用引擎自己的 `AssetDatabase.AssetPathToGUID` 写回真实 GUID
   - `3 把 .scene 场景改名为 .unity` → 用 `AssetDatabase.MoveAsset`，**GUID 与所有引用保持不变**
   - `4 修正 YAML 的 %TAG 命名空间` → 放最后，避免被引擎重新写回
   - `5 验收` → 断链数应降到"几十个以内"（剩下的都是第三方插件内部 + Unity 内置占位 GUID）
5. 关闭团结，切回 Unity 并做最终校验：
   ```bash
   python Tools/MigrateToUnity/switch_engine.py --unity
   python Tools/MigrateToUnity/tuanjie_guid_normalize.py     # 交叉验证引用健康度
   ```
   之后用 Unity Hub 打开工程。
6. 打开 Unity 后确认：Project 面板文件夹全部回来、`Assets/Scripts` 参与编译、Console 无编译错误。最后把 `Assets/Editor/TuanjieGuidConverter.cs` 与 `Tools/MigrateToUnity/` 删掉或留着都行。

> 为什么非得回团结：`.meta` 里的 XGUID 是 base64 **加密**格式（官方原话"为了与 Unity 彻底割裂而设计"），实测穷举"字节/半字节/位反转/固定 XOR 掩码"都推不出真实 GUID——**真实 GUID 只存在于团结的资产数据库里**。而 Unity 打开工程时已经把 `Library/SourceAssetDB` 重写掉了，离线反推的路已经断了。

#### 路线 B（快·有损）：直接给剩余 meta 分配新的唯一 GUID

```bash
python Tools/MigrateToUnity/fix_remaining_guids.py            # 试运行，看报告
python Tools/MigrateToUnity/fix_remaining_guids.py --apply    # 真正写
```

- 未归一化的 5597 个资产**绝大部分没有任何引用指向它们**，随便给个唯一 GUID 完全无害 → 文件夹立刻可见、脚本立刻参与编译。
- **代价**：约 **139 处"只被场景文件引用"的引用会永久丢失**（示例：`Assets/Scenes/Persistent/Persistent.scene`、`Battle01.scene` 等引用的一批资产），打开场景会出现 Missing，需要手工重连。因为这些 GUID 对应的真实资产无法再确定，**做了这一步就再也回不去路线 A**。
- 脚本自带保护：断链数必须与补齐前完全一致，否则拒绝执行；新 GUID 绝不占用任何被引用过的 GUID。

### 顺带确认的两件事

- `Assets/csc.rsp` 内容为 `-langversion:10.0`。Unity 2022.3.62 系列**编译侧支持**（社区已有同版本 2022.3.62f3c1 的实践），所以 Unity 编译不会报 CS1617。若 IDE 里仍报红，那是 Unity 生成的 `.csproj` 里 `<LangVersion>9.0</LangVersion>` 导致的（Unity 与 IDE 的语言版本不同步），与本次迁移无关，可用 CsprojModifier 或改写 csproj 解决。
- `Assets/Resources/DOTweenSettings.asset.meta` 的 guid 已被 Unity 换成新值（`557ab6fb...`）。该资产走 `Resources.Load` 加载，不依赖 GUID，无需处理。



## 6. 风险与工作量

| 风险 | 严重度 | 应对 |
|---|---|---|
| **只归一化"被引用的资产"→ 大量文件夹在 Unity 里不可见、脚本不参与编译** | **高（本次已踩）** | **必须全量归一化，一个 XGUID 都不能留**；用路线 A 或 B |
| **团结场景后缀 `.scene`，Unity 不认** | **高（本次已踩）** | 菜单 4 一键改名 `.unity`（保留 GUID） |
| GUID 归一化写错变体 → 引用依旧断 | 高 | 用"验收菜单 / 体检脚本"立刻能看出来；git 可回滚 |
| 少量历史遗留断链（本工程已知 2 处） | 低 | 迁移后在 Unity 里手动 Reassign |
| URP 14.2.0-t1 → 14.0.12 的 shader 差异 | 中 | 迁移后逐一检查材质，尤其 Toony Colors Pro / KinoBloom / 水体 shader |
| Input System 降级导致的输入配置差异 | 低 | 重设 `Active Input Handling` 与 Input Actions Asset |
| Addressables 分组/模板断链 | 低 | 迁移后 Build 一次，报错就重新指定模板 |
| 丢失微信小游戏 / 鸿蒙等团结特有管线 | 中 | 如果这是刚需，需要重新评估是否真要迁回 Unity |

**工作量预估**：全量归一化 + 配置约 1–2 小时（团结首次全量导入另需等待）；Unity 首次导入 + 验收半天。**代码本身预计 0 行改动。**

---

## 附：本次生成的文件

| 文件 | 用途 |
|---|---|
| `Tools/MigrateToUnity/TuanjieGuidConverter.cs` | 在团结引擎内执行的转换器（0 导出对照表 / 1 体检 / 2 全量归一化 / 3 场景改名 / 4 修 %TAG / 5 验收）；已拷一份到 `Assets/Editor/` 备用 |
| `Tools/MigrateToUnity/switch_engine.py` | 团结 ↔ Unity 版本切换助手（改写 ProjectVersion、挪走 packages-lock） |
| `Tools/MigrateToUnity/tuanjie_guid_normalize.py` | 离线体检 + 归一化工具（带 dry-run、报告、映射表导出；⚠️ 只覆盖被引用的资产） |
| `Tools/MigrateToUnity/fix_remaining_guids.py` | 补齐剩余 XGUID 为唯一新 GUID（路线 B 用；含断链来源分布报告，默认 dry-run） |
| `Tools/MigrateToUnity/guid_report.txt` | 第一轮体检报告 |
| `Tools/MigrateToUnity/guid_report_mapping.tsv` | 第一轮可确定归一化的「资产路径 → 原 GUID → 新 GUID」对照表（411 条） |
| `Tools/MigrateToUnity/fix_guids_report.txt` | 第二轮体检报告（剩余 5597 个 XGUID 与断链来源分布） |
