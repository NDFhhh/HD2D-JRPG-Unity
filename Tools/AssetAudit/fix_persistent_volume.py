# -*- coding: utf-8 -*-
"""
把 Persistent.unity 里失效的 URP Volume 组件摘掉，并刷新 BattleHitPostFX 的序列化字段。
默认干跑，加 --apply 才落盘。
"""
import io
import os
import re
import shutil
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SCENE = os.path.join(ROOT, "Assets/Scenes/Persistent/Persistent.unity")
BAK = os.path.join(ROOT, "_bak_pipeline_switch")

VOLUME_SCRIPT = "172515602e62fb746b5d573b38a5fe58"   # SRP Core Volume.cs（包已移除）
VOLUME_COMP = "261066788"                            # Persistent 里的 Volume 组件 fileID
GO = "261066787"                                     # 挂着它的 GameObject "Break Volume"
OLD_FIELDS = """  breakVolume: {fileID: 261066788}
  disableWhenIdle: 1
  flashWeight: 1
  flashDuration: 0.1"""
NEW_FIELDS = """  flashColor: {r: 1, g: 1, b: 1, a: 0.85}
  flashWeight: 1
  flashDuration: 0.1
  disableWhenIdle: 1
  flashOverlay: {fileID: 0}"""


def block_span(s, anchor):
    """返回包含 anchor 的那个 YAML 块的 [start, end)。"""
    i = s.find(anchor)
    if i < 0:
        return None
    st = s.rfind("\n--- ", 0, i)
    st = 0 if st < 0 else st + 1
    en = s.find("\n--- ", i)
    en = len(s) if en < 0 else en + 1
    return st, en


def main():
    apply = "--apply" in sys.argv
    s = io.open(SCENE, encoding="utf-8").read()
    orig = s
    report = []

    # 1) 删掉 Volume 的 MonoBehaviour 块
    span = block_span(s, "&%s\n" % VOLUME_COMP)
    if span is None:
        report.append("[跳过] 找不到 Volume 组件块 &%s" % VOLUME_COMP)
    else:
        chunk = s[span[0]:span[1]]
        if VOLUME_SCRIPT not in chunk:
            report.append("[警告] &%s 块里没有 Volume 脚本 guid，放弃删除" % VOLUME_COMP)
        else:
            s = s[:span[0]] + s[span[1]:]
            report.append("[1] 删除 Volume 组件块 &%s（%d 字节）" % (VOLUME_COMP, len(chunk)))

    # 2) 从 GameObject 的 m_Component 列表里摘掉它
    line = "  - component: {fileID: %s}\n" % VOLUME_COMP
    if line in s:
        s = s.replace(line, "", 1)
        report.append("[2] 从 GameObject &%s 的 m_Component 摘掉该组件" % GO)
    else:
        report.append("[跳过] m_Component 里没有 %s 的条目" % VOLUME_COMP)

    # 3) 刷新 BattleHitPostFX 的序列化字段
    if OLD_FIELDS in s:
        s = s.replace(OLD_FIELDS, NEW_FIELDS, 1)
        report.append("[3] 刷新 BattleHitPostFX 字段（去掉 breakVolume，补 flashColor / flashOverlay）")
    else:
        report.append("[跳过] 没找到预期中的旧字段块")

    # 4) 自检
    for bad, why in ((VOLUME_SCRIPT, "仍有 Volume 脚本引用"),
                     ("&%s\n" % VOLUME_COMP, "仍有组件块")):
        if bad in s:
            report.append("[!!] 自检未过：%s" % why)

    for r in report:
        print("  " + r)

    if s == orig:
        print("  （无改动）")
        return

    if not apply:
        print("\n  === 干跑结束，加 --apply 落盘 ===")
        return

    os.makedirs(BAK, exist_ok=True)
    shutil.copy2(SCENE, os.path.join(BAK, "Persistent.unity.bak"))
    io.open(SCENE, "w", encoding="utf-8", newline="").write(s)
    print("\n  已落盘。备份：_bak_pipeline_switch/Persistent.unity.bak")


if __name__ == "__main__":
    main()
