# -*- coding: utf-8 -*-
"""
未使用资源扫描器（只读，不修改任何文件）

思路：从「入口资产」出发做可达性遍历。
  入口 = Addressables 组的场景条目 + EditorBuildSettings 场景 + Assets/Resources/**
         + AddressableAssetSettings 本身
  边   = 任意文本资产里的 guid 引用（guid: / m_GUID: / m_AssetGUID:）+ .cs 里的
         GUIDToAssetPath("hex") / 字符串路径

遍历不到的资产 = 候选孤儿。

⚠️ 已知盲区（脚本会显式列出，需人工复核）：
  - 代码里用拼接/变量构造的路径（非字面量）
  - 二进制资产内部引用（fbx 内嵌材质、psd 图层等）
  - 编辑器脚本按路径字符串加载的资源
"""
import os
import re
import sys
import json
from collections import defaultdict, deque

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))

# 跳过目录
SKIP_DIRS = {
    "Library", "Temp", "obj", "Logs", "UserSettings", ".git", ".vs", ".idea",
    "Build", "Builds", "Tools",
}

# 文本资产扩展名（按内容扫 guid 引用）
TEXT_EXT = {
    ".unity", ".prefab", ".asset", ".mat", ".anim", ".controller",
    ".overridecontroller", ".terrainlayer", ".shadergraph", ".shadersubgraph",
    ".rendertexture", ".inputactions", ".asmdef", ".json", ".txt", ".cginc",
    ".shader", ".meta", ".lighting", ".guiskin", ".fontsettings", ".physicsmaterial2d",
    ".mixer", ".playable", ".signal", ".preset", ".brush", ".giparams",
}

# 这些扩展名本身是资产（会被引用）
ASSET_EXT = {
    ".unity", ".prefab", ".asset", ".mat", ".anim", ".controller",
    ".overridecontroller", ".terrainlayer", ".shadergraph", ".shadersubgraph",
    ".rendertexture", ".inputactions", ".asmdef", ".png", ".jpg", ".jpeg", ".tga",
    ".psd", ".exr", ".hdr", ".fbx", ".obj", ".blend", ".wav", ".mp3", ".ogg",
    ".otf", ".ttf", ".shader", ".cginc", ".cs", ".dll", ".mp4", ".mov",
    ".guiskin", ".fontsettings", ".spriteatlas", ".spriteatlasv2", ".mixer",
    ".playable", ".signal", ".preset", ".brush", ".giparams", ".unitypackage",
    ".lighting", ".terrainlayer", ".physicmaterial", ".physicsmaterial2d",
    ".cubemap", ".flare", ".mask", ".rsp", ".xml", ".uss", ".uxml", ".pdf", ".mdb",
}

GUID_REF = re.compile(rb"(?:guid|m_GUID|m_AssetGUID)\s*:\s*([0-9a-fA-F]{32})")
CS_GUID = re.compile(rb"([0-9a-fA-F]{32})")
META_GUID = re.compile(rb"^guid:\s*([0-9a-fA-F]{32})", re.M)


def iter_files():
    for dirpath, dirnames, filenames in os.walk(ROOT):
        rel_dir = os.path.relpath(dirpath, ROOT).replace("\\", "/")
        dirnames[:] = [d for d in dirnames
                       if d not in SKIP_DIRS and not d.startswith("Library.bak")]
        for fn in filenames:
            if rel_dir == "." and fn.startswith("guid_"):
                continue
            yield os.path.join(dirpath, fn).replace("\\", "/")


def to_rel(p):
    return os.path.relpath(p, ROOT).replace("\\", "/")


def main():
    # ---------- 1. 建立 guid -> 资产路径 映射 ----------
    guid2path = {}
    path2guid = {}
    metas = defaultdict(list)  # 资产路径 -> 自己的 meta 路径

    all_files = []
    for p in iter_files():
        all_files.append(p)
        if p.endswith(".meta"):
            continue

    # 先扫 meta
    for dirpath, dirnames, filenames in os.walk(ROOT):
        dirnames[:] = [d for d in dirnames
                       if d not in SKIP_DIRS and not d.startswith("Library.bak")]
        for fn in filenames:
            if not fn.endswith(".meta"):
                continue
            mp = os.path.join(dirpath, fn).replace("\\", "/")
            asset = mp[:-5]
            try:
                head = open(mp, "rb").read(400)
            except OSError:
                continue
            m = META_GUID.search(head)
            if not m:
                continue
            g = m.group(1).decode("ascii").lower()
            rel = to_rel(asset)
            if g in guid2path:
                print("!! GUID 冲突: %s  %s  vs  %s" % (g, guid2path[g], rel))
            guid2path[g] = rel
            path2guid[rel] = g
            metas[rel].append(mp)

    print("资产总览：%d 个文件（%d 个有 meta 的资产）" % (len(all_files), len(guid2path)))

    # ---------- 2. 收集引用边 ----------
    edges = defaultdict(set)      # 资产路径 -> {被引用的 guid}
    refs_by_guid = defaultdict(set)  # guid -> {引用它的资产路径}
    text_files = 0
    meta_files = 0
    cs_files = 0

    for p in all_files:
        ext = os.path.splitext(p)[1].lower()
        rel = to_rel(p)
        is_meta = rel.endswith(".meta")
        # .meta 的引用（如 FBX 的 externalObjects 材质重映射）归给属主资产
        owner = rel[:-5] if is_meta else rel
        try:
            data = open(p, "rb").read()
        except OSError:
            continue
        found = set()
        if ext in TEXT_EXT:
            if is_meta:
                meta_files += 1
            else:
                text_files += 1
            own = path2guid.get(owner)
            for m in GUID_REF.finditer(data):
                g = m.group(1).decode("ascii").lower()
                if g == own:
                    continue
                found.add(g)
        if ext == ".cs":
            cs_files += 1
            # GUIDToAssetPath("hex") 这类硬编码引用
            for m in re.finditer(rb"GUIDToAssetPath\s*\(\s*\"([0-9a-fA-F]{32})\"", data):
                found.add(m.group(1).decode("ascii").lower())
        if found:
            edges[owner] |= found
            for g in found:
                refs_by_guid[g].add(owner)

    print("文本资产 %d 个，meta %d 个，C# %d 个；有出边的资产 %d 个"
          % (text_files, meta_files, cs_files, len(edges)))

    # ---------- 3. 入口 ----------
    roots = set()
    root_reason = {}

    def add_root(path, why):
        r = path.replace("\\", "/")
        if r not in root_reason:
            root_reason[r] = why
            roots.add(r)

    # 3.1 Addressables 组条目（m_GUID: xxx 紧跟 m_Address）
    grp_dir = "Assets/AddressableAssetsData/AssetGroups"
    if os.path.isdir(os.path.join(ROOT, grp_dir)):
        for fn in sorted(os.listdir(os.path.join(ROOT, grp_dir))):
            if not fn.endswith(".asset"):
                continue
            gp = os.path.join(ROOT, grp_dir, fn)
            try:
                txt = open(gp, "rb").read()
            except OSError:
                continue
            add_root(os.path.join(grp_dir, fn), "Addressables 组定义")
            # 条目形如：- m_GUID: <hex>\n    m_Address: <addr>
            for m in re.finditer(
                rb"-\s*m_GUID:\s*([0-9a-fA-F]{32})\s*\n\s*m_Address:\s*([^\n]*)", txt):
                g = m.group(1).decode("ascii").lower()
                addr = m.group(2).decode("utf-8", "replace").strip()
                if g in guid2path:
                    add_root(guid2path[g], "Addressables 地址: %s" % addr)
                elif g.lower() in ("editorscenelist", "resources"):
                    pass
                else:
                    print("  [警告] Addressables 条目 guid 无法解析: %s (%s)" % (g, addr))

    # 3.2 EditorBuildSettings
    ebs = "ProjectSettings/EditorBuildSettings.asset"
    if os.path.isfile(os.path.join(ROOT, ebs)):
        txt = open(os.path.join(ROOT, ebs), "rb").read()
        for m in re.finditer(rb"path:\s*([^\n]+)", txt):
            pth = m.group(1).decode("utf-8", "replace").strip()
            if pth.startswith("Assets/"):
                add_root(pth, "Build Settings")

    # 3.3 Resources 目录（代码动态加载，整体保留）
    res_dir = os.path.join(ROOT, "Assets", "Resources")
    if os.path.isdir(res_dir):
        for dp, dns, fns in os.walk(res_dir):
            for fn in fns:
                fp = os.path.join(dp, fn).replace("\\", "/")
                if fp.endswith(".meta"):
                    continue
                add_root(to_rel(fp), "Resources 目录")

    # 3.4 AddressableAssetSettings 资产本身
    aas = "Assets/AddressableAssetsData/AddressableAssetSettings.asset"
    if os.path.isfile(os.path.join(ROOT, aas)):
        add_root(aas, "Addressables 设置")

    # 3.5 ProjectSettings 引用的资产（GraphicsSettings→URP、Quality→URP、Input、TMP 等）
    ps_dir = os.path.join(ROOT, "ProjectSettings")
    ps_ref_count = 0
    if os.path.isdir(ps_dir):
        for fn in sorted(os.listdir(ps_dir)):
            fp = os.path.join(ps_dir, fn)
            if not os.path.isfile(fp):
                continue
            if os.path.splitext(fn)[1].lower() not in (".asset", ".txt"):
                continue
            try:
                data = open(fp, "rb").read()
            except OSError:
                continue
            for m in GUID_REF.finditer(data):
                g = m.group(1).decode("ascii").lower()
                p = guid2path.get(g)
                if p is not None:
                    add_root(p, "ProjectSettings/%s" % fn)
                    ps_ref_count += 1
    print("   （ProjectSettings 引用解析出 %d 条）" % ps_ref_count)

    # 3.6 Packages 清单不引用 Assets，跳过

    print("\n入口资产 %d 个：" % len(roots))
    for r in sorted(roots):
        print("   %-70s  <- %s" % (r, root_reason[r]))

    # ---------- 4. BFS 可达性 ----------
    reachable = {}
    q = deque()
    for r in roots:
        reachable[r] = root_reason[r]
        q.append(r)

    while q:
        cur = q.popleft()
        for g in edges.get(cur, ()):  # 出边是 guid
            p = guid2path.get(g)
            if p is None or p in reachable:
                continue
            reachable[p] = "被引用: %s" % cur
            q.append(p)

    print("\n可达资产：%d 个" % len(reachable))

    # ---------- 5. 孤儿资产 ----------
    orphans = []
    for g, rel in guid2path.items():
        if rel in reachable:
            continue
        if rel in roots:
            continue
        ext = os.path.splitext(rel)[1].lower()
        if ext not in ASSET_EXT:
            continue
        try:
            size = os.path.getsize(os.path.join(ROOT, rel))
        except OSError:
            size = 0
        orphans.append((size, rel, ext))

    print("\n[DEBUG] 候选孤儿 %d 个 / 总资产 %d 个" % (len(orphans), len(guid2path)))

    orphans.sort(reverse=True)

    # ---------- 6. 输出 ----------
    out_dir = os.path.dirname(os.path.abspath(__file__))
    out_tsv = os.path.join(out_dir, "unused_assets.tsv")
    with open(out_tsv, "w", encoding="utf-8", newline="\n") as f:
        f.write("size_bytes\tsize_MB\text\tpath\n")
        for size, rel, ext in orphans:
            f.write("%d\t%.3f\t%s\t%s\n" % (size, size / 1048576.0, ext, rel))

    # 按扩展名汇总
    by_ext = defaultdict(lambda: [0, 0])
    for size, rel, ext in orphans:
        by_ext[ext][0] += 1
        by_ext[ext][1] += size

    print("\n" + "=" * 72)
    print("候选孤儿资产：%d 个，合计 %.1f MB" % (len(orphans), sum(o[0] for o in orphans) / 1048576.0))
    print("=" * 72)
    print("%-14s %8s %12s" % ("扩展名", "数量", "体积(MB)"))
    for ext, (n, sz) in sorted(by_ext.items(), key=lambda kv: -kv[1][1]):
        print("%-14s %8d %12.2f" % (ext, n, sz / 1048576.0))

    # 按顶层目录汇总
    by_dir = defaultdict(lambda: [0, 0])
    for size, rel, ext in orphans:
        parts = rel.split("/")
        key = "/".join(parts[:3]) if len(parts) > 3 else "/".join(parts[:-1])
        by_dir[key][0] += 1
        by_dir[key][1] += size

    print("\n按目录 TOP30：")
    print("%-58s %6s %12s" % ("目录", "数量", "体积(MB)"))
    for d, (n, sz) in sorted(by_dir.items(), key=lambda kv: -kv[1][1])[:30]:
        print("%-58s %6d %12.2f" % (d[:58], n, sz / 1048576.0))

    # 体积 TOP40 孤儿
    print("\n体积最大的 40 个孤儿：")
    print("%10s  %s" % ("MB", "路径"))
    for size, rel, ext in orphans[:40]:
        print("%10.2f  %s" % (size / 1048576.0, rel))

    print("\n完整清单已写入：%s" % out_tsv)

    # ---------- 7. 盲区自检 ----------
    print("\n" + "=" * 72)
    print("盲区自检（这些资产即使零引用也需人工确认）")
    print("=" * 72)
    risky = [o for o in orphans
             if "/Plugins/" in o[1] or "/Editor/" in o[1] or o[2] in (".cs", ".dll", ".asmdef")]
    print("落在 Plugins / Editor / 代码类 的孤儿：%d 个" % len(risky))
    for size, rel, ext in risky[:20]:
        print("   %9.2f MB  %s" % (size / 1048576.0, rel))
    if len(risky) > 20:
        print("   ... 其余 %d 个见 tsv" % (len(risky) - 20))


if __name__ == "__main__":
    main()
