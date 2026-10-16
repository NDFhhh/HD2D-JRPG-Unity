# -*- coding: utf-8 -*-
"""汇总：场景引用明细、URP 资产分布、Wipe Shader 管线标签、Synty 管线标签。"""
import os, re, collections

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SCENE = "Assets/Scenes/Demo_SanXingDui.unity"
GUID_IN_META = re.compile(rb"^guid:\s*([0-9a-fA-F]{32})\s*$", re.M)
GUID_REF = re.compile(rb"guid:\s*([0-9a-fA-F]{32})")
SKIP = {"Library", "Temp", "obj", "Logs"}

guid2path = {}
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
        if m:
            guid2path[m.group(1).decode().lower()] = os.path.relpath(mp[:-5], ROOT).replace("\\", "/")

# --- 场景引用全明细 ---
with open(os.path.join(ROOT, SCENE), "rb") as f:
    sdata = f.read()
refs = sorted(set(g.decode().lower() for g in GUID_REF.findall(sdata)))
buckets = collections.defaultdict(list)
for g in refs:
    p = guid2path.get(g)
    if p is None:
        buckets["(包内/内置)"].append("guid=" + g)
    else:
        buckets[os.path.splitext(p)[1].lower()].append(p)

print("=" * 70)
print("场景 %s 引用明细（%d 个 guid）" % (SCENE, len(refs)))
print("=" * 70)
for ext in sorted(buckets, key=lambda e: -len(buckets[e])):
    print("\n[%s] %d 个" % (ext, len(buckets[ext])))
    for p in buckets[ext][:14]:
        print("   ", p)
    if len(buckets[ext]) > 14:
        print("    ... 另 %d 个" % (len(buckets[ext]) - 14))

# --- 顶置目录占用 ---
print("\n" + "=" * 70)
print("场景引用的顶层目录分布")
print("=" * 70)
top = collections.Counter()
for ext in buckets:
    if ext == "(包内/内置)":
        continue
    for p in buckets[ext]:
        parts = p.split("/")
        top["/".join(parts[:2]) if len(parts) > 1 else parts[0]] += 1
for t, c in top.most_common(20):
    print("   %3d  %s" % (c, t))
