#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
清理移除 URP 后遗留在场景/prefab 上的失效组件（Missing Script）。

命中三类：
  1. Persistent.unity   -> Main Camera 上的 UniversalAdditionalCameraData
  2. 3 个 VFX prefab    -> Point Light 上的 UniversalAdditionalLightData
  3. Demo_SanXingDui    -> Quest_01 x4 上从未提交过、资产也已丢失的 PickupAction

判定方式统一为「m_Script 的 guid 在工程内无法兑现」，不是靠写死组件 id，
所以以后再跑一遍也能顺手抓出新的残留。

用法：
    python fix_urp_leftovers.py            # dry-run，只打印
    python fix_urp_leftovers.py --apply    # 实际写入（先备份到 _bak_pipeline_switch/urp_leftover/）
"""

import io
import json
import os
import re
import shutil
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
BAK = os.path.join(ROOT, "_bak_pipeline_switch", "urp_leftover")

# 明确要清理的 guid -> 人类可读的名字（只作日志用；真正判据是「无法兑现」）
KNOWN = {
    "a79441f348de89743a2939f4d699eac1": "URP UniversalAdditionalCameraData",
    "474bcb49853aa07438625e644c072ee6": "URP UniversalAdditionalLightData",
    "b5a58dbb78814965a8b5b71e8406b5c6": "PickupAction（脚本与资产均已丢失）",
}

TARGETS = [
    "Assets/Scenes/Persistent/Persistent.unity",
    "Assets/Scenes/Demo_SanXingDui.unity",
    "Assets/Prefabs/VFX/circles_blue.prefab",
    "Assets/Prefabs/VFX/star_yellow.prefab",
    "Assets/Prefabs/VFX/fire_red.prefab",
    # 游戏技能在用的 Hovl 特效 prefab（被 Game Data/Skill/Skill Data/{落冰,冰暴}.asset 引用）
    "Assets/Plugins/Hovl Studio/Magic effects pack/Prefabs/AoE effects/Crystals crossfade 2.prefab",
    "Assets/Plugins/Hovl Studio/Magic effects pack/Prefabs/AoE effects/Crystals front attack.prefab",
]


def load_guid_index():
    """复用之前建好的 guid -> 资产路径 索引"""
    p = os.path.join(ROOT, "Tools", "AssetAudit", "_guid_index.json")
    with io.open(p, encoding="utf-8") as f:
        idx = json.load(f)

    # 再补一遍 Packages 与 PackageCache，保证索引是新的
    for base in ("Packages", "Library/PackageCache"):
        d = os.path.join(ROOT, base)
        if not os.path.isdir(d):
            continue
        for dp, _dns, fns in os.walk(d):
            for fn in fns:
                if not fn.endswith(".meta"):
                    continue
                try:
                    t = io.open(os.path.join(dp, fn), encoding="utf-8", errors="replace").read(400)
                except OSError:
                    continue
                m = re.search(r"^guid: ([0-9a-f]{32})", t, re.M)
                if m:
                    idx.setdefault(m.group(1), os.path.join(dp, fn)[:-5])
    return idx


BLOCK_HEAD = re.compile(r"(?m)^--- !u!\d+ &")
NEXT_HEAD = re.compile(r"(?m)^--- ")


def block_span(s, fid):
    """返回 &fid 那个 YAML 块在 s 里的 [start, end)；注意块头可能带 ' stripped'"""
    m = re.search(r"(?m)^--- !u!\d+ &" + re.escape(fid) + r"(?: stripped)?[ \t]*\n", s)
    if not m:
        return None
    # 注意：re.search 的第三个位置参数是 flags 不是 pos，必须用 pattern.search(s, pos)
    nxt = NEXT_HEAD.search(s, m.end())
    return (m.start(), nxt.start() if nxt else len(s))


def drop_block(s, fid):
    sp = block_span(s, fid)
    if sp is None:
        return s, False
    return s[: sp[0]] + s[sp[1] :], True


def go_name_of(s, go_fid):
    b = block_span(s, go_fid)
    if b is None:
        return "?"
    m = re.search(r"(?m)^  m_Name: (.*)$", s[b[0] : b[1]])
    return m.group(1).strip() if m else "?"


def process(path, idx, apply_):
    full = os.path.join(ROOT, path)
    s = io.open(full, encoding="utf-8", errors="replace").read()
    orig = s

    # 找出所有 m_Script guid 无法兑现的 MonoBehaviour 块
    stale = []
    for m in re.finditer(r"(?m)^--- !u!114 &(-?\d+)(?: stripped)?[ \t]*\n(.*?)(?=^--- |\Z)", s, re.S | re.M):
        fid, body = m.group(1), m.group(2)
        g = re.search(r"m_Script: \{fileID: -?\d+, guid: ([0-9a-f]{32})", body)
        if not g or g.group(1) in idx:
            continue
        go = re.search(r"m_GameObject: \{fileID: (-?\d+)\}", body)
        stale.append((fid, g.group(1), go.group(1) if go else None))

    print("\n=== %s ===" % path)
    if not stale:
        print("  无失效组件")
        return False

    for fid, g, gofid in stale:
        who = KNOWN.get(g, "未知脚本 guid %s" % g)
        owner = go_name_of(s, gofid) if gofid else "?"
        print("  摘除 块 &%s  <- %s   挂在 GameObject「%s」上" % (fid, who, owner))
        if gofid:
            # 同步摘掉宿主 GameObject 的 m_Component 条目，否则留下一条指向空块的引用
            before = s
            s = re.sub(
                r"(?m)^  - component: \{fileID: " + re.escape(fid) + r"\}\n", "", s, count=1
            )
            if s == before:
                print("     !! 警告：在 GameObject &%s 的 m_Component 里没找到该条目" % gofid)
        s, ok = drop_block(s, fid)
        if not ok:
            print("     !! 警告：块 &%s 删除失败" % fid)

    if apply_:
        if not os.path.isdir(BAK):
            os.makedirs(BAK)
        bf = os.path.join(BAK, path.replace("/", "__"))
        if not os.path.exists(bf):
            shutil.copy2(full, bf)
        io.open(full, "w", encoding="utf-8", newline="\n").write(s)
        print("  已写入（备份 %s）" % os.path.relpath(bf, ROOT).replace("\\", "/"))
    else:
        print("  [dry-run] 减少 %d 字节" % (len(orig) - len(s)))
    return True


def main():
    apply_ = "--apply" in sys.argv
    idx = load_guid_index()
    print("guid 索引：%d 条" % len(idx))
    changed = 0
    for p in TARGETS:
        if not os.path.exists(os.path.join(ROOT, p)):
            print("\n=== %s ===\n  文件不存在，跳过" % p)
            continue
        if process(p, idx, apply_):
            changed += 1
    print("\n受影响文件：%d 个%s" % (changed, "" if apply_ else "（dry-run，未写入）"))


if __name__ == "__main__":
    main()
