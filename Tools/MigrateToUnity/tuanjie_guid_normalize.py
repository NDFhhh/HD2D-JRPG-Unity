#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
团结引擎(Tuanjie) -> Unity 迁移 · GUID 体检 / 归一化工具

为什么需要它
------------
团结引擎 1.x 会把自己的资产 GUID 写成 56 位 base64 的 "XGUID" 格式，例如:

    guid: Cy5LvXmpUHqrQkZAUQP9Tl6HAf9UGnqf8CYglX1+PB5YGl6kK718Kr4=

标准 Unity 不认这种格式，会给资产重新分配 GUID，于是场景 / prefab / ScriptableObject
里所有引用（它们用的仍是 32 位 hex GUID）全部断链。

本工具做两件事:
  1) --scan（默认）  体检：统计 XGUID 数量、YAML 引用健康度、迁移后会剩多少断链。
  2) --convert       把能**被 YAML 引用反向佐证**的 .meta guid 改写成 32 位 hex。

原理
----
真实 GUID 的来源是 Library/SourceAssetDB 里的资产记录:
    [..][4B 长度][00 00 10 00][16B GUID][路径]
其中那 16 字节 GUID 与真实 GUID 是"每个字节高低半字节互换"关系(nibble swap)，
互换回去即得到 YAML 中正在使用的 32 位 hex 值。

为了不误伤，脚本**不使用**任何猜测：只改写"该资产在 DB 中的候选 GUID 恰好等于
YAML 里真实引用着的某个 GUID"的资产；未被引用 / 有歧义 / 与已有 hex meta 冲突的
资产一律保持原样并写进报告，交由人工或团结引擎处理。

用法
----
    python tuanjie_guid_normalize.py                # 体检，输出报告
    python tuanjie_guid_normalize.py --convert      # 在体检基础上真正改写 meta
    python tuanjie_guid_normalize.py --convert --fix-tag

注意
----
* 运行前请**关闭团结引擎**。
* 建议先用 git 提交/备份当前改动，并在单独分支上操作。
* --fix-tag 会把 YAML 头部的 `tag:yousandi.cn,2023` 换成 `tag:unity3d.com,2011`。
"""

import argparse
import glob
import os
import re
import sys
from collections import Counter

GUID_LINE = re.compile(rb'guid:\s*(\S+)')
YAML_TAG = re.compile(rb'%TAG !u! tag:yousandi\.cn,2023:')
HEX32 = re.compile(rb'guid: ([0-9a-f]{32})')

YAML_EXTS = ("unity", "prefab", "asset", "controller", "mat", "playable",
             "anim", "overrideController", "lighting", "preset")


def nibble_swap(h: str) -> str:
    return "".join(h[i + 1] + h[i] for i in range(0, len(h), 2))


def db_candidates(data: bytes, rel_path: str):
    """该资产在 SourceAssetDB 中所有可能的真实 GUID（去重）"""
    needle = rel_path.encode()
    seen, out, pos = set(), [], 0
    while True:
        i = data.find(needle, pos)
        if i < 0:
            break
        pos = i + 1
        if i >= 16:
            g = nibble_swap(data[i - 16:i].hex())
            if g not in seen:
                seen.add(g)
                out.append(g)
    return out


def gather(project):
    """收集 meta 与 YAML 引用"""
    hex_guids, xguid_metas = set(), []
    for m in glob.glob(os.path.join(project, "Assets", "**", "*.meta"), recursive=True):
        mm = GUID_LINE.search(open(m, "rb").read(400))
        if not mm:
            continue
        g = mm.group(1).decode()
        if len(g) == 32:
            hex_guids.add(g)
        else:
            xguid_metas.append((m, g))

    refs, yaml_files = {}, []
    for ext in YAML_EXTS:
        yaml_files += glob.glob(os.path.join(project, "Assets", "**", "*." + ext), recursive=True)
    for f in yaml_files:
        try:
            txt = open(f, "rb").read()
        except OSError:
            continue
        for g in set(HEX32.findall(txt)):
            refs.setdefault(g.decode(), set()).add(f)

    pkg_guids = set()
    for m in glob.glob(os.path.join(project, "Library", "PackageCache", "**", "*.meta"), recursive=True):
        mm = GUID_LINE.search(open(m, "rb").read(200))
        if mm:
            pkg_guids.add(mm.group(1).decode())
    return hex_guids, xguid_metas, refs, pkg_guids, yaml_files


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--project", default=".")
    ap.add_argument("--convert", action="store_true", help="真正改写 .meta（默认只体检）")
    ap.add_argument("--fix-tag", action="store_true", help="同时修正 YAML 头部的 yousandi.cn tag")
    ap.add_argument("--report", default=os.path.join("Tools", "MigrateToUnity", "guid_report.txt"))
    args = ap.parse_args()
    project = os.path.abspath(args.project)

    db = os.path.join(project, "Library", "SourceAssetDB")
    if not os.path.exists(db):
        sys.exit("[x] 找不到 Library/SourceAssetDB。请先用团结引擎打开一次工程。")
    data = open(db, "rb").read()

    hex_guids, xguid_metas, refs, pkg_guids, yaml_files = gather(project)
    furnishable = {g for g in refs if g not in hex_guids and g not in pkg_guids}

    # ---- 约束求解：为每个 XGUID 资产找出它真正对应的那个被引用 GUID ----
    # 规则1：某资产只剩一个候选 -> 确定；规则2：某 GUID 只被一个资产候选包含 -> 归它。
    # 两条规则交替执行直到收敛，可自动消解绝大多数歧义，不靠猜。
    remaining = {}
    for m, _ in xguid_metas:
        rel = os.path.relpath(m[:-5], project).replace("\\", "/")
        hit = [c for c in db_candidates(data, rel) if c in furnishable]
        if hit:
            remaining[rel] = set(hit)
    mapping, stuck = {}, []          # rel -> (meta 路径, 原 guid, 新 guid)
    meta_of = {os.path.relpath(m[:-5], project).replace("\\", "/"): (m, g) for m, g in xguid_metas}
    while True:
        changed = False
        for rel, c in list(remaining.items()):
            if len(c) == 1:
                g = next(iter(c))
                mapping[rel] = (meta_of[rel][0], meta_of[rel][1], g)
                del remaining[rel]
                changed = True
                for other in remaining.values():
                    other.discard(g)
        by_guid = {}
        for rel, c in remaining.items():
            for g in c:
                by_guid.setdefault(g, []).append(rel)
        for g, owners in by_guid.items():
            if len(owners) == 1:
                rel = owners[0]
                mapping[rel] = (meta_of[rel][0], meta_of[rel][1], g)
                remaining.pop(rel, None)
                changed = True
                for other in remaining.values():
                    other.discard(g)
        if not changed:
            break
    stuck = sorted(remaining)
    unreferenced = len(xguid_metas) - len(mapping) - len(stuck)

    mapped_guids = {v[2] for v in mapping.values()}
    still_open = furnishable - mapped_guids

    OWN_DIRS = ("Assets/Scenes", "Assets/Prefabs", "Assets/Game Data", "Assets/Scripts",
                "Assets/Arts", "Assets/Settings", "Assets/Martials")

    def owned(paths):
        """引用了该 GUID 的文件里，属于你自研内容的那些"""
        return [p for p in paths
                if os.path.relpath(p, project).replace("\\", "/").startswith(OWN_DIRS)]

    lines = []
    A = lines.append
    A("=" * 74)
    A("团结引擎 -> Unity 迁移体检报告")
    A("=" * 74)
    A(f"meta 总数                       : {len(hex_guids) + len(xguid_metas)}")
    A(f"  已是 Unity hex 格式           : {len(hex_guids)}")
    A(f"  团结 XGUID 格式(需处理)        : {len(xguid_metas)}")
    A(f"YAML 文件数                     : {len(yaml_files)}")
    A(f"YAML 中被引用的不同 GUID          : {len(refs)}")
    A(f"  其中需要 XGUID 资产来兑现的     : {len(furnishable)}")
    A("")
    A(f"[迁移前] 用标准 Unity 直接打开，会断链 {len(furnishable)} 个引用"
      f"（当前 hex meta 无法兑现它们）")
    A(f"[迁移后] 本工具可确定修复 {len(mapping)} 个资产 -> 预计残余断链 {len(still_open)} 个")
    A("")
    own_open = sorted((g for g in still_open
                       if not g.startswith("0000000000000000") and owned(refs[g])),
                      key=lambda g: -len(refs[g]))
    A(f"残余断链中波及你自己工程目录的      : {len(own_open)}"
      f"  (另有内置资源占位 GUID 0000... 属正常，不计入)")
    for g in own_open[:15]:
        A(f"   ! {g} -> {len(refs[g])} 个文件, 例: {os.path.relpath(sorted(owned(refs[g]))[0], project)}")
    A("")
    A(f"未被引用的 XGUID 资产(保持原样无影响): {unreferenced}")
    A(f"有引用但候选仍有歧义(跳过待人工处理)  : {len(stuck)}")
    for rel in stuck[:10]:
        A(f"   ? {rel}")
    A("")
    A("-- 其余残余断链(基本是第三方特效包内部本身的断链/Unity 内置 dummy) --")
    for g in sorted((x for x in still_open if x not in set(own_open)), key=lambda x: -len(refs[x]))[:10]:
        A(f"   {g} -> {len(refs[g])} 个文件, 例: {sorted(refs[g])[0]}")
    report = "\n".join(lines)
    print(report)
    open(os.path.join(project, args.report), "w", encoding="utf-8").write(report + "\n")
    open(os.path.join(project, args.report.replace(".txt", "_mapping.tsv")), "w", encoding="utf-8").write(
        "\n".join(f"{rel}\t{old}\t{new}" for rel, (m, old, new) in sorted(mapping.items())))

    if not args.convert:
        print(f"\n[i] 这是体检模式，未改动任何文件。报告: {args.report}")
        return

    n = 0
    for rel, (m, old, new) in mapping.items():
        raw = open(m, "rb").read(400)
        head = GUID_LINE.sub(b"guid: " + new.encode(), raw, count=1)
        rest = open(m, "rb").read()[len(raw):]
        open(m, "wb").write(head + rest)
        n += 1
    print(f"[√] 已把 {n} 个 .meta 的 guid 改写为 Unity 32 位 hex 格式")

    if args.fix_tag:
        t = 0
        for f in yaml_files:
            raw = open(f, "rb").read()
            if YAML_TAG.search(raw):
                open(f, "wb").write(YAML_TAG.sub(b"%TAG !u! tag:unity3d.com,2011:", raw))
                t += 1
        print(f"[√] 已修正 {t} 个 YAML 的 %TAG 命名空间")

    print("\n下一步: 重新跑一次体检（应看到'预计残余断链'大幅下降），再用官方 Unity 2022.3.62f1 打开工程。")


if __name__ == "__main__":
    main()
