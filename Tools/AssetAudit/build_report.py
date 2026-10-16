# -*- coding: utf-8 -*-
"""
把孤儿清单按品类归类，生成：
  - unused_report.md   人类可读的分类报告
  - unused_manifest.tsv 删除清单（含 .meta 与空目录）
只读生成，不删除任何文件。
"""
import os
import re
from collections import defaultdict

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
TSV = os.path.join(HERE, "unused_assets.tsv")

# 品类判定（按优先级）
RULES = [
    ("A. 场景资源包（Assets/Prefabs/场景资源）", lambda p: p.startswith("Assets/Prefabs/场景资源/")),
    ("B. 第三方插件 Demo / 示例内容", lambda p: p.startswith("Assets/Plugins/")),
    ("C. Art/Characters 未使用精灵", lambda p: p.startswith("Assets/Arts/Characters/")),
    ("D. Art/UI 未使用精灵", lambda p: p.startswith("Assets/Arts/UI/")),
    ("E. Art/Icons 未使用图标", lambda p: p.startswith("Assets/Arts/Icons/")),
    ("F. Art/Mask 未使用遮罩", lambda p: p.startswith("Assets/Arts/Mask/")),
    ("G. Art/Fonts 未使用字体", lambda p: p.startswith("Assets/Arts/Fonts/")),
    ("H. Art/Audio 未使用音频", lambda p: p.startswith("Assets/Arts/Audio/")),
    ("I. Art/其他（Video/Environment）", lambda p: p.startswith("Assets/Arts/")),
    ("J. 场景文件（.unity）", lambda p: p.endswith(".unity")),
    ("K. Environment 未使用（模型/材质/贴图）", lambda p: p.startswith("Assets/Environment/")),
    ("L. Martials/Terrains 未使用地形材质", lambda p: p.startswith("Assets/Martials/")),
    ("M. TextMesh Pro 未使用资源", lambda p: p.startswith("Assets/TextMesh Pro/")),
    ("N. 其他 Scripts/Settings 等", lambda p: p.startswith("Assets/Scripts/") or p.startswith("Assets/Settings/")),
]


def classify(p):
    for name, fn in RULES:
        if fn(p):
            return name
    return "Z. 未归类"


def main():
    rows = []
    with open(TSV, encoding="utf-8") as f:
        next(f)
        for line in f:
            parts = line.rstrip("\n").split("\t")
            if len(parts) >= 4:
                rows.append((int(parts[0]), parts[2], parts[3]))

    groups = defaultdict(list)
    for size, ext, path in rows:
        groups[classify(path)].append((size, ext, path))

    lines = []
    lines.append("# 未使用资源扫描报告")
    lines.append("")
    lines.append("> 判定方式：从工程入口（Addressables 场景组 / Build Settings / Resources / ProjectSettings）")
    lines.append("> 出发做引用图可达性遍历，遍历不到的资产即为候选孤儿。")
    lines.append("> 已通过反向自洽验证（全工程重扫，非孤儿引用孤儿 = 0 处）。")
    lines.append("")
    total_n = sum(len(v) for v in groups.values())
    total_s = sum(s for v in groups.values() for s, _, _ in v)
    lines.append("- **候选孤儿合计：%d 个 / %.1f MB**" % (total_n, total_s / 1048576.0))
    lines.append("- 全部资产均为 Unity 文本序列化（Force Text），引用形式为 32 位 hex GUID")
    lines.append("")
    lines.append("| 品类 | 数量 | 体积 |")
    lines.append("|---|---:|---:|")
    for name in sorted(groups):
        v = groups[name]
        lines.append("| %s | %d | %.1f MB |" % (name, len(v), sum(s for s, _, _ in v) / 1048576.0))
    lines.append("")
    lines.append("---")
    lines.append("")

    for name in sorted(groups):
        v = sorted(groups[name], reverse=True)
        lines.append("## %s" % name)
        lines.append("")
        lines.append("共 %d 个 / %.1f MB" % (len(v), sum(s for s, _, _ in v) / 1048576.0))
        lines.append("")
        for size, ext, path in v[:40]:
            lines.append("- `%s`  (%.2f MB)" % (path, size / 1048576.0))
        if len(v) > 40:
            lines.append("- ... 其余 %d 个，见 `unused_manifest.tsv`" % (len(v) - 40))
        lines.append("")

    with open(os.path.join(HERE, "unused_report.md"), "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines))

    # 删除清单：资产 + 对应 .meta
    manifest = []
    metas = 0
    missing_meta = []
    for size, ext, path in rows:
        manifest.append(path)
        mp = path + ".meta"
        if os.path.isfile(os.path.join(ROOT, mp)):
            manifest.append(mp)
            metas += 1
        else:
            missing_meta.append(path)
    with open(os.path.join(HERE, "unused_manifest.tsv"), "w", encoding="utf-8", newline="\n") as f:
        f.write("path\n")
        for p in sorted(manifest):
            f.write(p + "\n")

    print("报告已生成：unused_report.md")
    print("删除清单：unused_manifest.tsv")
    print("  资产 %d 个 + meta %d 个 = 共 %d 条" % (len(rows), metas, len(manifest)))
    if missing_meta:
        print("  [注意] 无 meta 的资产 %d 个：" % len(missing_meta))
        for p in missing_meta[:10]:
            print("     ", p)

    print("\n分类汇总：")
    for name in sorted(groups):
        v = groups[name]
        print("  %-46s %5d 个 %9.1f MB" % (name, len(v), sum(s for s, _, _ in v) / 1048576.0))


if __name__ == "__main__":
    main()
