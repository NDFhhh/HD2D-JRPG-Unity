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
   - `2 - 把 .meta 的 GUID 归一化为 Unity 32 位 hex`
   - `3 - 修正 YAML 的 %TAG 命名空间`
   - `4 - 验收：列出所有仍然断链的引用` ← **这一步是成败判据**
3. 验收标准：断链数从 ~436 掉到"几十个以内"，且剩下的都是第三方特效包内部的历史断链和 Unity 内置资源占位 GUID（`0000...f000...`）。
4. 若菜单 4 显示断链数**没有下降**，说明引擎 API 返回的是另一个变体：把每个 hex 做一次"每字节高低半字节互换"再重跑菜单 2 即可（工具注释里写了）。

**方案 B（离线·不用开团结）**
```bash
python Tools/MigrateToUnity/tuanjie_guid_normalize.py            # 只看报告，不动文件
python Tools/MigrateToUnity/tuanjie_guid_normalize.py --convert --fix-tag
```
这套算法**不用猜**：它从 `Library/SourceAssetDB` 反推真实 GUID，再用"YAML 里确实有人在引用这个 GUID"作为反向佐证，只改写能被唯一确定的资产，其余全部跳过并写进清单。
本次实测：**可确定修复 411 个资产，断链 436 → 25**，且剩下 25 个里只有 2 处落在自研目录（`Assets/Prefabs/VFX/fire_red.prefab`、`Town01_Day.unity`），而这 2 处**在迁移前就已经是断的了**（历史遗留的失效引用）。

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

## 5. 风险与工作量

| 风险 | 严重度 | 应对 |
|---|---|---|
| GUID 归一化写错变体 → 引用依旧断 | 高 | 用"验收菜单 / 体检脚本"立刻能看出来；git 可回滚 |
| 少量历史遗留断链（本工程已知 2 处） | 低 | 迁移后在 Unity 里手动 Reassign |
| URP 14.2.0-t1 → 14.0.11 的 shader 差异 | 中 | 迁移后逐一检查材质，尤其 Toony Colors Pro / KinoBloom / 水体 shader |
| Input System 降级导致的输入配置差异 | 低 | 重设 `Active Input Handling` 与 Input Actions Asset |
| Addressables 分组/模板断链 | 低 | 迁移后 Build 一次，报错就重新指定模板 |
| 丢失微信小游戏 / 鸿蒙等团结特有管线 | 中 | 如果这是刚需，需要重新评估是否真要迁回 Unity |

**工作量预估**：归一化 + 配置约 1–2 小时；Unity 首次导入 + 验收半天。**代码本身预计 0 行改动。**

---

## 附：本次生成的文件

| 文件 | 用途 |
|---|---|
| `Tools/MigrateToUnity/TuanjieGuidConverter.cs` | 在团结引擎内执行的转换器（体检 / 归一化 / 修 %TAG / 验收 四个菜单） |
| `Tools/MigrateToUnity/tuanjie_guid_normalize.py` | 离线体检 + 归一化工具（带 dry-run、报告、映射表导出） |
| `Tools/MigrateToUnity/guid_report.txt` | 本次体检报告（已在当前工程上跑过） |
| `Tools/MigrateToUnity/guid_report_mapping.tsv` | 可确定归一化的「资产路径 → 原 GUID → 新 GUID」对照表（411 条） |
