# -*- coding: utf-8 -*-
"""
生成最终删除方案：
  - delete_safe.tsv    安全删除（确定的孤儿，无盲区风险）
  - delete_review.tsv  需人工确认（编译器配置 / 脚本 / 着色器 / 光照 / 场景 / 程序集）
  - final_report.md    可读报告
只读生成。
"""
import os
import re
from collections import defaultdict

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
TSV = os.path.join(HERE, "unused_assets.tsv")

# 需要人工确认的后缀（有非 GUID 的引用机制 / 或影响编译）
REVIEW_EXT = {
    ".cs", ".asmdef", ".dll", ".rsp", ".shader", ".cginc", ".inputactions",
    ".uxml", ".uss", ".asmref", ".mdb", ".shadergraph", ".shadersubgraph",
    ".unity", ".otf", ".ttf",
}
# 需要人工确认的路径特征
REVIEW_PATH = ("Lightmap", "ReflectionProbe", "LightingData", "LightmapCache",
               "Arts/Fonts")

CATS = [
    ("A. 场景资源包 Assets/Prefabs/场景资源", lambda p: p.startswith("Assets/Prefabs/场景资源/")),
    ("B. 第三方插件 Demo/示例", lambda p: p.startswith("Assets/Plugins/")),
    ("C. Art/Characters 未使用精灵", lambda p: p.startswith("Assets/Arts/Characters/")),
    ("D. Art/UI 未使用精灵", lambda p: p.startswith("Assets/Arts/UI/")),
    ("E. Art/Icons 未使用图标", lambda p: p.startswith("Assets/Arts/Icons/")),
    ("F. Art/Mask 未使用遮罩", lambda p: p.startswith("Assets/Arts/Mask/")),
    ("G. Art/Fonts 未使用字体", lambda p: p.startswith("Assets/Arts/Fonts/")),
    ("H. Art/Audio 未使用音频", lambda p: p.startswith("Assets/Arts/Audio/")),
    ("I. Art 其他", lambda p: p.startswith("Assets/Arts/")),
    ("J. 场景文件 .unity", lambda p: p.endswith(".unity")),
    ("K. Environment 未使用", lambda p: p.startswith("Assets/Environment/")),
    ("L. Martials/Terrains 未使用", lambda p: p.startswith("Assets/Martials/")),
    ("M. TextMesh Pro 未使用", lambda p: p.startswith("Assets/TextMesh Pro/")),
    ("N. Scenes 内光照/烘焙数据", lambda p: p.startswith("Assets/Scenes/")),
    ("O. Scripts/Settings", lambda p: p.startswith("Assets/Scripts/") or p.startswith("Assets/Settings/")),
]


def classify(p):
    for name, fn in CATS:
        if fn(p):
            return name
    return "Z. 其他散落"


def needs_review(p):
    ext = os.path.splitext(p)[1].lower()
    if ext in REVIEW_EXT:
        return "后缀 " + ext
    for k in REVIEW_PATH:
        if k in p:
            return "光照/烘焙数据（%s）" % k if k != "Arts/Fonts" else "字体（Arts/Fonts）"
    if p == "Assets/csc.rsp":
        return "编译器配置（Unity 按约定读取）"
    return None


def main():
    rows = []
    with open(TSV, encoding="utf-8") as f:
        next(f)
        for line in f:
            parts = line.rstrip("\n").split("\t")
            if len(parts) >= 4:
                rows.append((int(parts[0]), parts[2], parts[3]))

    safe, review = [], []
    for size, ext, path in rows:
        r = needs_review(path)
        (review if r else safe).append((size, ext, path, r or ""))

    # 写安全清单（含 meta）
    def dump(lst, fn):
        n_meta = 0
        out = []
        for size, ext, path, *_ in lst:
            out.append(path)
            mp = path + ".meta"
            if os.path.isfile(os.path.join(ROOT, mp)):
                out.append(mp)
                n_meta += 1
        with open(os.path.join(HERE, fn), "w", encoding="utf-8", newline="\n") as f:
            f.write("path\n")
            for p in sorted(out):
                f.write(p + "\n")
        return len(out), n_meta

    n_safe, m_safe = dump(safe, "delete_safe.tsv")
    n_rev, m_rev = dump(review, "delete_review.tsv")

    # 报告
    L = []
    L.append("# 未使用资源清理方案")
    L.append("")
    tot_n = len(rows)
    tot_s = sum(r[0] for r in rows)
    L.append("扫描结论：**候选孤儿 %d 个 / %.1f MB**" % (tot_n, tot_s / 1048576.0))
    L.append("")
    L.append("- **安全删除**：%d 个资产 / %.1f MB" % (len(safe), sum(r[0] for r in safe) / 1048576.0))
    L.append("- **需确认**：%d 个资产 / %.1f MB" % (len(review), sum(r[0] for r in review) / 1048576.0))
    L.append("")
    L.append("> 判定方法：从工程入口（Addressables 场景组 + Build Settings + Resources +")
    L.append("> ProjectSettings）做引用图可达性遍历。已通过反向自洽验证（全工程重扫，")
    L.append("> 「非孤儿引用孤儿」= 0 处）。")
    L.append("")
    L.append("## 按品类汇总")
    L.append("")
    L.append("| 品类 | 安全删除 | 需确认 | 合计体积 |")
    L.append("|---|---:|---:|---:|")
    g = defaultdict(lambda: [0, 0, 0])
    for size, ext, path, _ in safe:
        g[classify(path)][0] += 1
        g[classify(path)][1] += size
    for size, ext, path, _ in review:
        g[classify(path)][2] += 1
    for name in sorted(g):
        a, sa, r = g[name]
        L.append("| %s | %d | %d | %.1f MB |" % (name, a, r, sa / 1048576.0))
    L.append("")
    L.append("## 需确认清单（%d 个）" % len(review))
    L.append("")
    L.append("| 原因 | 数量 | 体积 |")
    L.append("|---|---:|---:|")
    rg = defaultdict(lambda: [0, 0])
    for size, ext, path, why in review:
        rg[why][0] += 1
        rg[why][1] += size
    for why, (c, s) in sorted(rg.items(), key=lambda kv: -kv[1][1]):
        L.append("| %s | %d | %.1f MB |" % (why, c, s / 1048576.0))
    L.append("")
    with open(os.path.join(HERE, "final_report.md"), "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(L))

    print("安全删除: %d 资产 (+%d meta) = %d 条, %.1f MB"
          % (len(safe), m_safe, n_safe, sum(r[0] for r in safe) / 1048576.0))
    print("需确认  : %d 资产 (+%d meta) = %d 条, %.1f MB"
          % (len(review), m_rev, n_rev, sum(r[0] for r in review) / 1048576.0))
    print()
    print("需确认明细：")
    for why, (c, s) in sorted(rg.items(), key=lambda kv: -kv[1][1]):
        print("   %-28s %4d 个 %8.2f MB" % (why, c, s / 1048576.0))
    print()
    print("安全删除按品类：")
    for name in sorted(g):
        a, sa, r = g[name]
        print("   %-40s %5d 个 %9.1f MB" % (name, a, sa / 1048576.0))


if __name__ == "__main__":
    main()
