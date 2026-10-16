# -*- coding: utf-8 -*-
"""探测某个场景实际用到哪些材质/着色器，判断它属于 Built-in 还是 URP 管线。"""
import os, re, sys, collections

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SCENE = sys.argv[1] if len(sys.argv) > 1 else "Assets/Scenes/Demo_SanXingDui.unity"

GUID_IN_META = re.compile(rb"^guid:\s*([0-9a-fA-F]{32})\s*$", re.M)
GUID_REF = re.compile(rb"guid:\s*([0-9a-fA-F]{32})")
SHADER_REF = re.compile(rb"m_Shader:\s*\{fileID:\s*(-?\d+),\s*guid:\s*([0-9a-fA-F]{32})")

SKIP = {"Library", "Library.bak-from-tuanjie", "Library.bak-from-unity", "Temp", "obj", "Logs"}

# 1) 建 guid -> 资产路径 映射
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
            asset = mp[:-5]
            rel = os.path.relpath(asset, ROOT).replace("\\", "/")
            guid2path[m.group(1).decode("ascii").lower()] = rel

print("meta 索引：%d 个资产" % len(guid2path))

# 2) 场景引用到的 guid
scene_abs = os.path.join(ROOT, SCENE.replace("/", os.sep))
with open(scene_abs, "rb") as f:
    sdata = f.read()
scene_guids = set(g.decode("ascii").lower() for g in GUID_REF.findall(sdata))
print("场景引用 GUID：%d 个（去重）" % len(scene_guids))

# 3) 分类解析
mats, other, unresolved = [], collections.Counter(), 0
for g in scene_guids:
    p = guid2path.get(g)
    if p is None:
        unresolved += 1
        continue
    ext = os.path.splitext(p)[1].lower()
    if ext == ".mat":
        mats.append(p)
    else:
        other[ext] += 1

print("\n无法兑现的 guid（多为内置资源/包内）：%d 个" % unresolved)
print("\n非材质引用按扩展名：")
for ext, c in other.most_common():
    print("   %-14s %d" % (ext or "(无扩展名)", c))

# 4) 材质 -> 着色器
print("\n场景用到的材质（%d 个）：" % len(mats))
shader_counter = collections.Counter()
shader_of = {}
for p in sorted(mats):
    ap = os.path.join(ROOT, p.replace("/", os.sep))
    try:
        with open(ap, "rb") as f:
            d = f.read(65536)
    except OSError:
        continue
    m = SHADER_REF.search(d)
    if not m:
        shader_of[p] = "(无 m_Shader)"
        shader_counter["(无 m_Shader)"] += 1
        continue
    sg = m.group(2).decode("ascii").lower()
    sp = guid2path.get(sg, "内置/包内 shader guid=%s" % sg)
    shader_of[p] = sp
    shader_counter[sp] += 1

print("\n材质使用的着色器分布：")
for s, c in shader_counter.most_common():
    print("   %3d x  %s" % (c, s))

print("\n材质明细：")
for p in sorted(mats):
    print("   %s\n        -> %s" % (p, shader_of.get(p, "?")))
