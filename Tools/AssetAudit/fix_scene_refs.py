# -*- coding: utf-8 -*-
"""
删除 Battle/Field/Town 场景前的引用清理：
  1. 从 3 个 VFX prefab 里摘掉已失效的 BoostVFX1/2/3 节点（Volume 组件 + 空 GameObject）
  2. Addressables Scenes 组：删掉 7 条待删场景的条目，补上 Demo_SanXingDui
  3. Persistent.unity 的 SceneLoadManager：activeScene / _startupGameplayScene 改指 Demo_SanXingDui

默认干跑，加 --apply 才落盘。备份统一放 _bak_scene_del/。
"""
import io
import os
import re
import shutil
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
BAK = os.path.join(ROOT, "_bak_scene_del")

demo_meta = os.path.join(ROOT, "Assets/Scenes/Demo_SanXingDui.unity.meta")
DEMO_GUID = re.search(r"guid: ([0-9a-f]{32})",
                      io.open(demo_meta, encoding="utf-8").read()).group(1)

# 要清掉的场景地址
DEAD_ADDRS = {"Field01", "Field02", "Field03",
              "Battle01", "Battle02", "Battle03", "Town01_Day"}
# Persistent 里指向 Town01_Day 的旧 guid
OLD_GUID = "cda5df0599d29a74593beb7ddd1490be"

# 3 个 VFX prefab：(路径, Volume 组件 id, 宿主 GameObject id, 宿主 Transform id, 父 Transform id)
VFX = [
    ("Assets/Prefabs/VFX/circles_blue.prefab",
     "3515979479255958541", "1718674956428739323",
     "5016433462444620712", "4823518202205099632"),
    ("Assets/Prefabs/VFX/star_yellow.prefab",
     "3803911183339611176", "6398281264866277030",
     "3380656243777791280", "6532625474870085666"),
    ("Assets/Prefabs/VFX/fire_red.prefab",
     "1856390197121315149", "8894002755858071793",
     "7971808022864879145", "3559932527940391457"),
]

ENTRY = re.compile(r"  - m_GUID: ([0-9a-f]{32})\n((?:    .*\n)+)")


def read(p):
    return io.open(os.path.join(ROOT, p), encoding="utf-8").read()


def span_of(s, fid):
    """返回 &fid 那个 YAML 块的 [start, end)（start 指向 '--- ' 开头）"""
    m = re.search(r"\n--- !u!\d+ &" + fid + r"\n", s)
    if not m:
        return None
    st = m.start() + 1
    en = s.find("\n--- ", m.end())
    return (st, len(s) if en < 0 else en + 1)


def fix_prefab(rel, volid, goid, trid, parentid, apply):
    """把 BoostVFX 节点整个摘掉（Volume + GameObject + Transform + 父节点 m_Children 引用）"""
    s = read(rel)
    orig = s
    log = []

    # 1) Volume 组件块
    sp = span_of(s, volid)
    if sp and "172515602e62fb746b5d573b38a5fe58" in s[sp[0]:sp[1]]:
        s = s[:sp[0]] + s[sp[1]:]
        log.append("删 Volume 组件 &%s" % volid)
    else:
        log.append("!! 找不到 Volume 组件 &%s" % volid)

    # 2) GameObject 块
    sp = span_of(s, goid)
    if sp and "\nGameObject:\n" in s[sp[0]:sp[1]]:
        s = s[:sp[0]] + s[sp[1]:]
        log.append("删 GameObject &%s" % goid)
    else:
        log.append("!! 找不到 GameObject &%s" % goid)

    # 3) Transform 块
    sp = span_of(s, trid)
    if sp and "\nTransform:\n" in s[sp[0]:sp[1]]:
        s = s[:sp[0]] + s[sp[1]:]
        log.append("删 Transform &%s" % trid)
    else:
        log.append("!! 找不到 Transform &%s" % trid)

    # 4) 父 Transform 的 m_Children 条目
    line = "  - {fileID: %s}\n" % trid
    if line in s:
        s = s.replace(line, "", 1)
        log.append("从父 Transform &%s 的 m_Children 摘掉" % parentid)
    else:
        log.append("!! 父 m_Children 里没有 &%s" % trid)

    # 自检：三个 id 都不应再出现
    left = [x for x in (volid, goid, trid) if x in s]
    if left:
        log.append("!! 自检未过，仍残留 %s" % left)
        return None
    if s == orig:
        log.append("（无改动）")
        return None
    if apply:
        os.makedirs(BAK, exist_ok=True)
        shutil.copy2(os.path.join(ROOT, rel),
                     os.path.join(BAK, rel.replace("/", "__") + ".bak"))
        io.open(os.path.join(ROOT, rel), "w", encoding="utf-8", newline="").write(s)
    print("  [prefab] %s" % rel)
    for l in log:
        print("      " + l)
    return True


def fix_addressables(apply):
    rel = "Assets/AddressableAssetsData/AssetGroups/Scenes.asset"
    s = read(rel)
    orig = s
    removed, kept = [], []

    def repl(m):
        guid, body = m.group(1), m.group(2)
        am = re.search(r"m_Address: (\S+)", body)
        addr = am.group(1) if am else "?"
        if addr in DEAD_ADDRS:
            removed.append(addr)
            return ""
        kept.append(addr)
        return m.group(0)

    s = ENTRY.sub(repl, s)

    # 补上 Demo_SanXingDui
    if "Demo_SanXingDui" not in kept:
        new = ("  - m_GUID: %s\n"
               "    m_Address: Demo_SanXingDui\n"
               "    m_ReadOnly: 0\n"
               "    m_SerializedLabels: []\n"
               "    FlaggedDuringContentUpdateRestriction: 0\n" % DEMO_GUID)
        s = s.replace("  m_SerializeEntries:\n",
                      "  m_SerializeEntries:\n" + new, 1)

    print("  [addressables] %s" % rel)
    print("      删除条目: %s" % ", ".join(sorted(removed)))
    print("      保留条目: %s" % ", ".join(sorted(kept)))
    print("      新增条目: Demo_SanXingDui (%s)" % DEMO_GUID)

    if apply:
        os.makedirs(BAK, exist_ok=True)
        shutil.copy2(os.path.join(ROOT, rel),
                     os.path.join(BAK, "Scenes.asset.bak"))
        io.open(os.path.join(ROOT, rel), "w", encoding="utf-8", newline="").write(s)
    return s != orig


def fix_persistent(apply):
    rel = "Assets/Scenes/Persistent/Persistent.unity"
    s = read(rel)
    n = s.count(OLD_GUID)
    print("  [persistent] %s" % rel)
    print("      指向 Town01_Day 的引用 %d 处 → 改指 Demo_SanXingDui" % n)
    if n == 0:
        return False
    s2 = s.replace(OLD_GUID, DEMO_GUID)
    # 自检
    if OLD_GUID in s2 or s2.count(DEMO_GUID) - s.count(DEMO_GUID) != n:
        print("      !! 自检未过")
        return False
    if apply:
        os.makedirs(BAK, exist_ok=True)
        shutil.copy2(os.path.join(ROOT, rel),
                     os.path.join(BAK, "Persistent.unity.bak"))
        io.open(os.path.join(ROOT, rel), "w", encoding="utf-8", newline="").write(s2)
    return True


def main():
    apply = "--apply" in sys.argv
    print("Demo_SanXingDui guid = %s" % DEMO_GUID)
    print()
    for rel, volid, goid, trid, parentid in VFX:
        fix_prefab(rel, volid, goid, trid, parentid, apply)
    print()
    fix_addressables(apply)
    print()
    fix_persistent(apply)
    print()
    print("=== %s ===" % ("已落盘" if apply else "干跑结束，加 --apply 落盘"))


if __name__ == "__main__":
    main()
