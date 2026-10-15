#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
引擎切换助手：在「团结引擎 2022.3.62t12」与「官方 Unity 2022.3.62f3c1」之间切换工程。

只做三件事，绝不碰资产：
  1) 改写 / 还原 ProjectSettings/ProjectVersion.txt
  2) 切换时把 Packages/packages-lock.json 挪走（避免它把工程继续指向另一套包注册表）
  3) 打印当前状态

用法（务必先关掉正在运行的编辑器）：
    python switch_engine.py --tuanjie     # 切到团结引擎（回团结补做 GUID 转换时用）
    python switch_engine.py --unity       # 切回官方 Unity
    python switch_engine.py --status      # 只看当前状态
"""

import argparse
import os
import re
import shutil
import sys

TUANJIE = ("m_EditorVersion: 2022.3.62t12\n"
           "m_EditorVersionWithRevision: 2022.3.62t12 (ab02e98c9779)\n"
           "m_TuanjieEditorVersion: 1.10.0\n")

UNITY = ("m_EditorVersion: 2022.3.62f3c1\n"
         "m_EditorVersionWithRevision: 2022.3.62f3c1 (1623fc0bbb97)\n")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--project", default=".")
    ap.add_argument("--tuanjie", action="store_true")
    ap.add_argument("--unity", action="store_true")
    ap.add_argument("--status", action="store_true")
    args = ap.parse_args()
    os.chdir(os.path.abspath(args.project))

    ver = os.path.join("ProjectSettings", "ProjectVersion.txt")
    lock = os.path.join("Packages", "packages-lock.json")
    if not os.path.exists(ver):
        sys.exit("[x] 找不到 ProjectSettings/ProjectVersion.txt，请确认工程目录")

    cur = open(ver, encoding="utf-8").read()
    cur_ver = re.search(r"m_EditorVersion:\s*(\S+)", cur)
    cur_ver = cur_ver.group(1) if cur_ver else "未知"

    if args.status or not (args.tuanjie or args.unity):
        print(f"当前工程版本 : {cur_ver}")
        print(f"packages-lock: {'存在' if os.path.exists(lock) else '不存在'}")
        print("\n提示：--tuanjie 切到团结 / --unity 切回官方 Unity（切换前请先关掉编辑器）")
        return

    if not os.path.exists(ver + ".bak"):
        shutil.copy2(ver, ver + ".bak")
        print(f"已备份原 ProjectVersion.txt -> {ver}.bak")

    if args.tuanjie:
        open(ver, "w", encoding="utf-8", newline="\n").write(TUANJIE)
        if os.path.exists(lock):
            shutil.move(lock, lock + ".unity-bak")
            print(f"已把 packages-lock.json 移到 {lock}.unity-bak（让团结自己重新解析依赖）")
        print(f"工程已切到 团结引擎 2022.3.62t12（原 {cur_ver}）")
        print("下一步：用 Tuanjie Hub / D:\\Unity\\2022.3.62t12\\Editor\\Tuanjie.exe 打开本工程，"
              "等它全量导入完成，菜单 Tools/迁移到Unity 依次跑 1 -> 2 -> 3 -> 4 -> 5。")
    else:
        open(ver, "w", encoding="utf-8", newline="\n").write(UNITY)
        if os.path.exists(lock):
            os.remove(lock)
            print("已删除 packages-lock.json（让 Unity 重新解析）")
        print(f"工程已切回 官方 Unity 2022.3.62f3c1（原 {cur_ver}）")
        print("下一步：用 Unity Hub 打开本工程。")


if __name__ == "__main__":
    main()
