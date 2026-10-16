# -*- coding: utf-8 -*-
"""
补齐二进制盲区：工程里有 11 个「非文本 Unity 资产」（LightingData / Terrain / Battle 等），
它们的引用以「16 字节原始 GUID」存储，文本扫不到。

本脚本：
  1. 找出所有非文本 Unity 资产
  2. 在其中扫描 16 字节窗口，与孤儿 GUID 的二进制形式比对（字节序 + 反序都试）
  3. 输出「因二进制引用而应剔除」的孤儿清单
只读。
"""
import os
import re

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
SKIP = {"Library", "Temp", "obj", "Logs", "UserSettings", ".git", ".vs", ".idea",
        "Build", "Builds", "Tools"}
UNITY_EXT = {".asset", ".unity", ".prefab", ".mat", ".controller", ".anim",
             ".overridecontroller", ".shadergraph", ".rendertexture", ".mixer",
             ".playable", ".signal", ".lighting", ".terrainlayer", ".guiskin"}
META_GUID = re.compile(rb"^guid:\s*([0-9a-fA-F]{32})", re.M)


def main():
    # 1. 读孤儿
    orphans = []
    with open(os.path.join(HERE, "unused_assets.tsv"), encoding="utf-8") as f:
        next(f)
        for line in f:
            p = line.rstrip("\n").split("\t")
            if len(p) >= 4:
                orphans.append(p[3])
    orphan_set = set(orphans)

    # 2. 找非文本 Unity 资产
    bin_assets = []
    for dp, dns, fns in os.walk(ROOT):
        dns[:] = [d for d in dns if d not in SKIP and not d.startswith("Library.bak")]
        for fn in fns:
            if fn.endswith(".meta"):
                continue
            if os.path.splitext(fn)[1].lower() not in UNITY_EXT:
                continue
            p = os.path.join(dp, fn)
            try:
                d = open(p, "rb").read(80)
            except OSError:
                continue
            if b"YAML" not in d:
                bin_assets.append(p.replace("\\", "/"))
    print("非文本 Unity 资产：%d 个" % len(bin_assets))
    for b in bin_assets:
        print("   %.2f MB  %s" % (os.path.getsize(b) / 1048576.0, os.path.relpath(b, ROOT).replace("\\", "/")))

    # 3. 孤儿 guid -> 16 字节形式
    guid_of = {}
    pat = {}
    for o in orphans:
        mp = os.path.join(ROOT, o + ".meta")
        try:
            head = open(mp, "rb").read(400)
        except OSError:
            continue
        m = META_GUID.search(head)
        if not m:
            continue
        g = m.group(1).decode("ascii").lower()
        b = bytes.fromhex(g)
        guid_of[g] = o
        pat[b] = g
        pat[b[::-1]] = g
    print("孤儿 guid：%d 个" % len(guid_of))

    # 4. 滑动窗口扫描
    hit = {}
    for bp in bin_assets:
        try:
            data = open(bp, "rb").read()
        except OSError:
            continue
        rel = os.path.relpath(bp, ROOT).replace("\\", "/")
        n = len(data)
        for i in range(0, n - 16 + 1):
            g = pat.get(data[i:i + 16])
            if g is not None:
                hit.setdefault(g, set()).add(rel)

    print("\n被二进制资产引用的孤儿：%d 个" % len(hit))
    total = 0
    for g in sorted(hit, key=lambda k: guid_of[k]):
        o = guid_of[g]
        sz = os.path.getsize(os.path.join(ROOT, o))
        total += sz
        print("   %9.2f MB  %s" % (sz / 1048576.0, o))
        for src in sorted(hit[g]):
            print("                  <- %s" % src)
    print("\n合计 %.2f MB 应剔除" % (total / 1048576.0))

    with open(os.path.join(HERE, "binary_referenced.txt"), "w", encoding="utf-8", newline="\n") as f:
        for g in sorted(hit, key=lambda k: guid_of[k]):
            f.write(guid_of[g] + "\n")
    print("已写入 binary_referenced.txt")


if __name__ == "__main__":
    main()
