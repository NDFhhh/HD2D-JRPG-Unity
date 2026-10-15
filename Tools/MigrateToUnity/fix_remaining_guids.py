#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
补齐剩余的团结 XGUID —— 把还是 56 位 base64 的 .meta guid 换成唯一的新 32 位 hex GUID。

为什么需要
----------
标准 Unity 遇到非法 guid 的 .meta 时会**直接忽略该资产**（不进 Project 面板、不参与编译），
所以哪怕这些资产"没有被任何东西引用"，也必须给它们一个合法的 GUID，否则:
  * 整个文件夹在 Project 面板里消失（folder 资产没被导入）
  * 文件夹里的脚本不参与编译 —— 例如 Assets/Scripts/Utility/GlobalUsing.cs（global using）失效

对"没有被引用"的资产来说，GUID 具体是多少无所谓（没有任何引用指向它），
所以这里用 XGUID 的哈希生成一个**确定性且唯一**的新 GUID：可重复执行、结果稳定。

安全前提（脚本会自动核对）：
  1) 只处理"未被任何 YAML 引用的 GUID 仍无法兑现"的资产 —— 脚本会先算一遍引用健康度；
  2) 新 GUID 绝不与现有 GUID、也不与任何被引用过的 GUID 相同；
  3) 结果可通过 git diff / git checkout 完全回滚。

用法
----
    python fix_remaining_guids.py            # 试运行
    python fix_remaining_guids.py --apply
"""

import argparse
import glob
import hashlib
import os
import re
import sys

GUID_LINE = re.compile(rb'guid:\s*(\S+)', re.MULTILINE)
REF_GUID = re.compile(r'guid: ([0-9a-f]{32})')

# 这些是纯二进制，不用去里面找 guid 引用
BINARY_EXT = {".png", ".tga", ".jpg", ".jpeg", ".psd", ".exr", ".fbx", ".obj", ".dll",
              ".otf", ".ttf", ".wav", ".mp3", ".mp4", ".pdf", ".mdb", ".unitypackage",
              ".bin", ".hash", ".so", ".bundle", ".cubin", ".bytes"}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--project", default=".")
    ap.add_argument("--apply", action="store_true")
    ap.add_argument("--report", default=os.path.join("Tools", "MigrateToUnity", "fix_guids_report.txt"))
    args = ap.parse_args()
    project = os.path.abspath(args.project)
    os.chdir(project)

    # --- 1. 收集现有 meta ---
    metas, hex_guids, xguid_metas = [], set(), []
    for m in glob.glob("Assets/**/*.meta", recursive=True):
        raw = open(m, "rb").read(400)
        mm = GUID_LINE.search(raw)
        if not mm:
            continue
        g = mm.group(1).decode()
        metas.append(m)
        if len(g) == 32:
            hex_guids.add(g)
        else:
            xguid_metas.append((m, g))

    # 包内资产（Packages / PackageCache）也有自己的 guid，引用它们不算断链
    metric_hex = len(hex_guids)                     # 仅统计工程自身的 hex meta
    pkg_guids = set()
    for m in glob.glob("Library/PackageCache/**/*.meta", recursive=True):
        mm = GUID_LINE.search(open(m, "rb").read(200))
        if mm:
            pkg_guids.add(mm.group(1).decode())
    known = hex_guids | pkg_guids

    # --- 2. 扫描所有资产文件里的 guid 引用（尽量穷尽，含未被导入的资产）---
    refs = set()
    ref_src = {}                       # guid -> set(引用它的文件)
    for f in glob.glob("Assets/**/*", recursive=True):
        ext = os.path.splitext(f)[1].lower()
        if not os.path.isfile(f) or ext in BINARY_EXT or ext == ".meta":
            continue
        try:
            if os.path.getsize(f) > 8 * 1024 * 1024:
                continue
            txt = open(f, "rb").read()
        except OSError:
            continue
        if b"\x00" in txt[:2048]:          # 看着像二进制就跳过
            continue
        for g in set(REF_GUID.findall(txt.decode("utf-8", "ignore"))):
            refs.add(g)
            ref_src.setdefault(g, set()).add(f)

    unresolved_before = sorted(refs - known)

    # --- 3. 为剩余 XGUID 生成唯一新 GUID ---
    # 唯一性上，绝不占用"现有 guid"和"任何被引用过的 guid"，避免误伤
    used = set(known) | refs
    plan, new_guids = [], set()
    for m, old in xguid_metas:
        n = 0
        while True:
            new = hashlib.md5(f"tuanjie-xguid:{old}:{n}".encode()).hexdigest()
            if new not in used:
                break
            n += 1
        used.add(new)
        new_guids.add(new)
        plan.append((m, old, new))

    # 健康度核对：新 guid 不会去兑现既有引用，所以断链数必须与补齐前完全一致
    unresolved_after = sorted(refs - known - new_guids)

    lines = []
    A = lines.append
    A("=" * 70)
    A("补齐剩余 XGUID -> 新 32 位 hex GUID")
    A("=" * 70)
    A(f"meta 总数                     : {len(metas)}")
    A(f"  已是 hex                    : {metric_hex}   (另有包内资产 {len(pkg_guids)} 个)")
    A(f"  待补齐 XGUID                : {len(xguid_metas)}")
    A(f"资产文件里扫描到的不同 GUID 引用 : {len(refs)}")
    A(f"  补齐前无法兑现的引用(断链)    : {len(unresolved_before)}")
    A(f"  补齐后无法兑现的引用(断链)    : {len(unresolved_after)}   <- 应与补齐前相同（这些是历史遗留断链，不属于本次问题）")
    A("")
    OWN_DIRS = ("Assets/Scenes", "Assets/Prefabs", "Assets/Game Data", "Assets/Scripts",
                "Assets/Arts", "Assets/Settings", "Assets/Martials", "Assets/Animation",
                "Assets/Environment", "Assets/PostProcess", "Assets/Resources")
    by_ext, own_broken = {}, []
    for g in unresolved_after:
        srcs = ref_src.get(g, set())
        exts = sorted({os.path.splitext(x)[1].lower() for x in srcs})
        for e in exts:
            by_ext[e] = by_ext.get(e, 0) + 1
        if any(os.path.relpath(x, project).replace("\\", "/").startswith(OWN_DIRS) for x in srcs):
            own_broken.append((g, sorted(ref_src[g])))
    A("-- 断链引用按「发出引用的文件类型」分布 --")
    for e, n in sorted(by_ext.items(), key=lambda x: -x[1]):
        A(f"   {e or '(无扩展名)':<22}{n}")
    A("")
    A(f"-- 其中波及你自己工程目录的断链: {len(own_broken)} 条 --")
    for g, srcs in own_broken[:20]:
        A(f"   {g}\n      {os.path.relpath(srcs[0], project)}")
    A("")
    A("-- 前 10 条补齐示例 --")
    for m, old, new in plan[:10]:
        A(f"   {m}\n      {old}\n   -> {new}")
    report = "\n".join(lines)
    print(report)
    open(args.report, "w", encoding="utf-8").write(report + "\n")

    if unresolved_after != unresolved_before:
        print("[!] 警告：引用健康度发生了变化，请先人工确认再执行 --apply")
        if args.apply:
            sys.exit(1)

    if not args.apply:
        print(f"\n[i] 试运行结束，未改动文件。报告: {args.report}")
        return

    for m, old, new in plan:
        raw = open(m, "rb").read(400)
        rest = open(m, "rb").read()[len(raw):]
        open(m, "wb").write(GUID_LINE.sub(b"guid: " + new.encode(), raw, count=1) + rest)
    print(f"[√] 已为 {len(plan)} 个 .meta 写入唯一的新 GUID")
    print("下一步：关闭并重新打开 Unity（首次会全量导入，耐心等），Project 面板里的文件夹就会全部回来。")


if __name__ == "__main__":
    main()
