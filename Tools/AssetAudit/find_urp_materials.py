# -*- coding: utf-8 -*-
"""列出工程内所有使用 URP / Blur 包着色器的材质（切到 Built-in 后会变洋红）。"""
import os, re, collections

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
GUID_IN_META = re.compile(rb"^guid:\s*([0-9a-fA-F]{32})\s*$", re.M)
SHADER_REF = re.compile(rb"m_Shader:\s*\{fileID:\s*(-?\d+),\s*guid:\s*([0-9a-fA-F]{32})")

PKG_DIRS = [
    os.path.join(ROOT, "Library/PackageCache/com.unity.render-pipelines.universal@14.0.12"),
    os.path.join(ROOT, "Library/PackageCache/com.unify.unified-universal-blur@4a22db9640"),
]

# 1) 收集包内 shader guid -> 名字
pkg_shader = {}
for pd in PKG_DIRS:
    for dirpath, _, filenames in os.walk(pd):
        for fn in filenames:
            if not fn.endswith(".shader.meta"):
                continue
            mp = os.path.join(dirpath, fn)
            with open(mp, "rb") as f:
                m = GUID_IN_META.search(f.read(4096))
            if m:
                pkg_shader[m.group(1).decode().lower()] = os.path.relpath(mp[:-10], ROOT).replace("\\", "/")

print("包内着色器 guid 收集：%d 个（URP + Blur）\n" % len(pkg_shader))

# 2) 扫 Assets 下所有 .mat
hits = collections.defaultdict(list)
total = 0
for dirpath, dirnames, filenames in os.walk(os.path.join(ROOT, "Assets")):
    for fn in filenames:
        if not fn.endswith(".mat"):
            continue
        total += 1
        p = os.path.join(dirpath, fn)
        with open(p, "rb") as f:
            d = f.read(65536)
        m = SHADER_REF.search(d)
        if not m:
            continue
        g = m.group(2).decode().lower()
        if g in pkg_shader:
            rel = os.path.relpath(p, ROOT).replace("\\", "/")
            hits[pkg_shader[g]].append(rel)

print("Assets 下材质总数：%d" % total)
print("使用 URP/Blur 着色器的材质：%d\n" % sum(len(v) for v in hits.values()))

for sh in sorted(hits, key=lambda s: -len(hits[s])):
    lst = hits[sh]
    print("=" * 66)
    print("%s   (%d 个材质)" % (sh, len(lst)))
    print("=" * 66)
    for p in sorted(lst):
        print("   ", p)
