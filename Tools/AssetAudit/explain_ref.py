# -*- coding: utf-8 -*-
"""
引用解释器：给定资产路径，说明它的 guid、谁引用了它、它引用了谁、
以及它是否在可达集合里（若是，给出可达原因）。
只读。
"""
import os
import re
import sys
from collections import defaultdict

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SKIP_DIRS = {"Library", "Temp", "obj", "Logs", "UserSettings", ".git", ".vs", ".idea", "Build", "Builds", "Tools"}
TEXT_EXT = {
    ".unity", ".prefab", ".asset", ".mat", ".anim", ".controller",
    ".overridecontroller", ".terrainlayer", ".shadergraph", ".shadersubgraph",
    ".rendertexture", ".inputactions", ".asmdef", ".json", ".txt", ".cginc",
    ".shader", ".meta", ".lighting", ".guiskin", ".fontsettings", ".physicsmaterial2d",
    ".mixer", ".playable", ".signal", ".preset", ".brush", ".giparams",
}
GUID_REF = re.compile(rb"(?:guid|m_GUID|m_AssetGUID)\s*:\s*([0-9a-fA-F]{32})")
META_GUID = re.compile(rb"^guid:\s*([0-9a-fA-F]{32})", re.M)


def iter_files():
    for dirpath, dirnames, filenames in os.walk(ROOT):
        dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS and not d.startswith("Library.bak")]
        for fn in filenames:
            yield os.path.join(dirpath, fn).replace("\\", "/")


def main(queries, depth=4):
    # 建 guid 映射
    guid2path = {}
    path2guid = {}
    for p in iter_files():
        if not p.endswith(".meta"):
            continue
        asset = p[:-5]
        try:
            head = open(p, "rb").read(400)
        except OSError:
            continue
        m = META_GUID.search(head)
        if not m:
            continue
        g = m.group(1).decode("ascii").lower()
        rel = os.path.relpath(asset, ROOT).replace("\\", "/")
        guid2path[g] = rel
        path2guid[rel] = g

    # 建出边
    edges = defaultdict(set)
    rev = defaultdict(set)
    for p in iter_files():
        ext = os.path.splitext(p)[1].lower()
        rel = os.path.relpath(p, ROOT).replace("\\", "/")
        if ext not in TEXT_EXT:
            continue
        try:
            data = open(p, "rb").read()
        except OSError:
            continue
        own = path2guid.get(rel)
        for m in GUID_REF.finditer(data):
            g = m.group(1).decode("ascii").lower()
            if g == own:
                continue
            edges[rel].add(g)
            rev[g].add(rel)

    for q in queries:
        q = q.replace("\\", "/")
        g = path2guid.get(q)
        print("=" * 78)
        print("查询: %s" % q)
        if not g:
            # 可能是 guid
            if q.lower() in guid2path:
                g = q.lower()
                q = guid2path[g]
                print("  (按 guid 解析) -> %s" % q)
            else:
                print("  找不到该资产")
                continue
        print("guid: %s" % g)
        print("\n  【谁引用了我】(下游→上游) 共 %d 个：" % len(rev.get(g, ())))
        if not rev.get(g):
            print("     （无 —— 零被引用）")
        for r in sorted(rev.get(g, ()))[:15]:
            print("     %s" % r)
        print("\n  【我引用了谁】共 %d 个：" % len(edges.get(q, ())))
        for e in list(edges.get(q, ()))[:15]:
            tgt = guid2path.get(e, "<未知>")
            print("     %s" % tgt)
        print()


if __name__ == "__main__":
    main(sys.argv[1:])
