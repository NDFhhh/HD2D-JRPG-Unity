# -*- coding: utf-8 -*-
"""
准备删除 Assets/Environment/Materials 下 43 个洋红材质（URP Lit，着色器已删）。

做两件事：
  1. 把 .mat + .mat.meta 原件复制到 _bak_env_materials/（可恢复）
  2. 生成 delete_env_materials.tsv（给 apply_delete.py 用）

用法：
  python prep_env_mat_delete.py            # 只备份 + 出清单
"""
import io
import os
import re
import shutil

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
MAT_DIR = os.path.join(ROOT, "Assets", "Environment", "Materials")
BAK = os.path.join(ROOT, "_bak_env_materials")
TSV = os.path.join(HERE, "delete_env_materials.tsv")

URP_LIT = "933532a4fcc9baf4fa0491de14d08ed7"


def main():
    mats = sorted(f for f in os.listdir(MAT_DIR) if f.endswith(".mat"))
    print("=== 目标目录 %s" % MAT_DIR)
    print("    .mat 文件 %d 个" % len(mats))

    # 逐个确认确实是 URP Lit（着色器 guid 就是被删掉的 URP Lit）
    ok, odd = [], []
    for f in mats:
        t = io.open(os.path.join(MAT_DIR, f), encoding="utf-8", errors="replace").read(900)
        m = re.search(r"m_Shader: \{fileID: -?\d+, guid: ([0-9a-f]{32})", t)
        g = m.group(1) if m else "(无)"
        (ok if g == URP_LIT else odd).append((f, g))
    print("    其中 URP Lit %d 个" % len(ok))
    if odd:
        print("    !! 不是 URP Lit 的（需人工确认）:")
        for f, g in odd:
            print("       %s -> %s" % (f, g))

    # 1) 备份
    os.makedirs(BAK, exist_ok=True)
    n = 0
    for f in mats:
        for p in (f, f + ".meta"):
            src = os.path.join(MAT_DIR, p)
            if os.path.exists(src):
                shutil.copy2(src, os.path.join(BAK, p))
                n += 1
    extra = os.path.join(ROOT, "Assets", "Environment", "Materials.meta")
    if os.path.exists(extra):
        shutil.copy2(extra, os.path.join(BAK, "_Materials.folder.meta.bak"))
    print("\n[备份] %d 个文件 -> %s" % (n, os.path.relpath(BAK, ROOT)))

    # 2) 清单
    paths = []
    for f in mats:
        paths.append("Assets/Environment/Materials/" + f)
        paths.append("Assets/Environment/Materials/" + f + ".meta")
    paths.append("Assets/Environment/Materials.meta")

    with io.open(TSV, "w", encoding="utf-8", newline="\n") as fh:
        fh.write("path\n")
        for p in paths:
            fh.write(p + "\n")
    print("[清单] %d 条 -> %s" % (len(paths), os.path.relpath(TSV, ROOT)))


if __name__ == "__main__":
    main()
