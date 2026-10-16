# -*- coding: utf-8 -*-
"""
反向自洽验证（优化版）：读取 unused_assets.tsv，
把所有孤儿的 guid 收集为集合，单次正则扫全工程每个文件，
检查是否有文件引用了孤儿 guid。

速度：一次正则 finditer（O(文件大小)），集合判重 O(1)。
只读。
"""
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SKIP_DIRS = {"Library", "Temp", "obj", "Logs", "UserSettings", ".git", ".vs", ".idea",
             "Build", "Builds", "Tools"}
TSV = os.path.join(os.path.dirname(os.path.abspath(__file__)), "unused_assets.tsv")
META_GUID = re.compile(rb"^guid:\s*([0-9a-fA-F]{32})", re.M)
HEX32 = re.compile(rb"[0-9a-fA-F]{32}")
HEX32_BIN = re.compile(rb"(?:[\x00-\xff]{16})")  # 不用，占位


def iter_all():
    for dirpath, dirnames, filenames in os.walk(ROOT):
        dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS and not d.startswith("Library.bak")]
        for fn in filenames:
            yield os.path.join(dirpath, fn).replace("\\", "/")


def main():
    orphans = []
    with open(TSV, encoding="utf-8") as f:
        next(f)
        for line in f:
            parts = line.rstrip("\n").split("\t")
            if len(parts) >= 4:
                orphans.append(parts[3])
    print("孤儿清单：%d 个" % len(orphans))
    orphan_set = set(orphans)

    og = []
    for p in orphans:
        mp = os.path.join(ROOT, p + ".meta")
        try:
            head = open(mp, "rb").read(400)
        except OSError:
            continue
        m = META_GUID.search(head)
        if m:
            og.append((p, m.group(1).decode("ascii").lower()))
    guid_set = {g for _, g in og}
    guid_bytes = {g.encode("ascii") for g in guid_set}
    guid2orphan = {g: pp for pp, g in og}
    print("解析出 guid：%d 个" % len(guid_set))

    violations = []
    nfile = 0
    total_bytes = 0
    for p in iter_all():
        rel = os.path.relpath(p, ROOT).replace("\\", "/")
        try:
            with open(p, "rb") as f:
                data = f.read()
        except OSError:
            continue
        nfile += 1
        total_bytes += len(data)
        if not data:
            continue
        # 文本：单次正则提取所有 32-hex（工程为 Force Text，引用全是文本形式）
        for m in HEX32.finditer(data):
            gb = m.group(0).lower()
            if gb in guid_bytes:
                violations.append((rel, guid2orphan[gb.decode("ascii")], "text"))

    print("扫描 %d 个文件（%.1f MB），发现引用 %d 处\n" % (nfile, total_bytes / 1048576.0, len(violations)))

    if not violations:
        print(">>> 完全自洽：没有任何文件引用孤儿资产。")
        return

    from collections import Counter
    kinds = Counter()
    detail = []
    for src, tgt, kind in violations:
        if src == tgt + ".meta":
            kinds["自己 meta"] += 1
            continue
        # .meta 的引用归给属主资产（FBX 材质重映射等）
        owner = src[:-5] if src.endswith(".meta") else src
        if owner in orphan_set:
            kinds["另一个孤儿引用"] += 1
        else:
            kinds["【真矛盾】非孤儿引用孤儿"] += 1
            detail.append((src, tgt, kind))
    for k, v in kinds.most_common():
        print("  %-26s %d" % (k, v))

    if detail:
        print("\n【真矛盾】明细（前 60）：")
        seen = set()
        for src, tgt, kind in detail:
            key = (src, tgt)
            if key in seen:
                continue
            seen.add(key)
            print("  [%s] %s\n         -> %s" % (kind, src, tgt))
            if len(seen) >= 60:
                break
    else:
        print("\n>>> 无「非孤儿引用孤儿」的矛盾 —— 扫描结果自洽。")


if __name__ == "__main__":
    main()
