# -*- coding: utf-8 -*-
"""
决定性验证：判定一批 guid 是否真的全工程零引用。
同时搜「32 位 hex 文本」和「16 字节原始二进制」（Unity 二进制资产用后者）。
只读。
"""
import os
import sys
import re

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SKIP_DIRS = {"Library", "Temp", "obj", "Logs", "UserSettings", ".git", ".vs", ".idea", "Build", "Builds", "Tools"}


def iter_all():
    for dirpath, dirnames, filenames in os.walk(ROOT):
        dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS and not d.startswith("Library.bak")]
        for fn in filenames:
            yield os.path.join(dirpath, fn).replace("\\", "/")


def main(guids):
    targets = []
    for t in guids:
        if os.path.isfile(os.path.join(ROOT, t)):
            mp = os.path.join(ROOT, t + ".meta")
            d = open(mp, "rb").read(400)
            m = re.search(rb"^guid: ([0-9a-fA-F]{32})", d, re.M)
            g = m.group(1).decode("ascii").lower() if m else None
            targets.append((t, g))
        else:
            targets.append((t, t.lower()))
    targets = [(p, g) for p, g in targets if g]

    text_hits = {g: [] for _, g in targets}
    bin_hits = {g: [] for _, g in targets}
    bytes_map = {g: bytes.fromhex(g)[::-1] for _, g in targets}  # Unity 二进制用小端存储
    bytes_map2 = {g: bytes.fromhex(g) for _, g in targets}       # 保底大端
    text_pat = re.compile("|".join(g for _, g in targets).encode("ascii"), re.I)

    nfile = 0
    for p in iter_all():
        rel = os.path.relpath(p, ROOT).replace("\\", "/")
        if rel.endswith(".meta"):
            continue
        try:
            with open(p, "rb") as f:
                data = f.read()
        except OSError:
            continue
        nfile += 1
        for m in text_pat.finditer(data):
            g = m.group(0).decode("ascii").lower()
            if g in text_hits:
                text_hits[g].append(rel)
        for g in targets:
            _, gg = g
            if bytes_map[gg] in data or bytes_map2[gg] in data:
                bin_hits[gg].append(rel)

    print("扫描 %d 个文件\n" % nfile)
    for path, g in targets:
        th = sorted(set(x for x in text_hits[g] if x != path))
        bh = sorted(set(bin_hits[g]))
        print("=" * 76)
        print("资产: %s" % path)
        print("guid: %s" % g)
        print("  文本引用: %d 处" % len(th))
        for x in th[:10]:
            print("      %s" % x)
        print("  二进制引用: %d 处" % len(bh))
        for x in bh[:10]:
            print("      %s" % x)
        if th or bh:
            print("  >>> 有引用，不是孤儿")
        else:
            print("  >>> 确认零引用")


if __name__ == "__main__":
    main(sys.argv[1:])
