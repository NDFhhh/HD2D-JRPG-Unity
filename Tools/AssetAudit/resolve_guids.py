# -*- coding: utf-8 -*-
"""解析指定 guid -> 资产路径。"""
import os, re, sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
GUID_IN_META = re.compile(rb"^guid:\s*([0-9a-fA-F]{32})\s*$", re.M)
SKIP = {"Library", "Temp", "obj", "Logs"}

targets = [a.lower() for a in sys.argv[1:]]
want = set(targets)

found = {}
extra = {}
for dirpath, dirnames, filenames in os.walk(ROOT):
    dirnames[:] = [d for d in dirnames if d not in SKIP and not d.startswith("Library.bak")]
    for fn in filenames:
        if not fn.endswith(".meta"):
            continue
        mp = os.path.join(dirpath, fn)
        try:
            with open(mp, "rb") as f:
                head = f.read(4096)
        except OSError:
            continue
        m = GUID_IN_META.search(head)
        if not m:
            continue
        g = m.group(1).decode().lower()
        rel = os.path.relpath(mp[:-5], ROOT).replace("\\", "/")
        if g in want:
            found[g] = rel
        # 顺便收集所有 URP 相关资产
        low = rel.lower()
        if "universalrenderpipeline" in low or "renderer" in low or "/settings/" in low.lower():
            extra.setdefault(rel, g)

print("=== 目标 guid 解析 ===")
for g in targets:
    print("  %s -> %s" % (g, found.get(g, "!!! 未找到")))

print("\n=== 疑似 URP/设置 相关资产 ===")
for rel, g in sorted(extra.items()):
    print("  %s   [%s]" % (rel, g))
