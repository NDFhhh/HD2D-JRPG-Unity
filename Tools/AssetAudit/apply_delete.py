# -*- coding: utf-8 -*-
"""
删除执行器：把清单里的文件移入 Windows 回收站（可恢复）。

用法：
  python apply_delete.py --list delete_safe.tsv            # 干跑（默认），只统计
  python apply_delete.py --list delete_safe.tsv --apply    # 真正执行

语义：
  - 移入回收站（SHFileOperationW + FOF_ALLOWUNDO），不是永久删除
  - 分批（每批 400 个），每批校验
  - 执行后清理因此变空的目录（只删空目录，不动有内容的）
"""
import os
import sys
import ctypes
from ctypes import wintypes, Structure, c_uint, c_void_p

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))


class SHFILEOPSTRUCTW(Structure):
    _fields_ = [
        ("hwnd", wintypes.HWND),
        ("wFunc", c_uint),
        ("pFrom", ctypes.c_wchar_p),
        ("pTo", ctypes.c_wchar_p),
        ("fFlags", wintypes.WORD),
        ("fAnyOperationsAborted", wintypes.BOOL),
        ("hNameMappings", c_void_p),
        ("lpszProgressTitle", ctypes.c_wchar_p),
    ]


FO_DELETE = 3
FOF_ALLOWUNDO = 0x40
FOF_NOCONFIRMATION = 0x10
FOF_SILENT = 0x4
FOF_NOERRORUI = 0x400
FOF_NOCONFIRMMKDIR = 0x200


def recycle(paths):
    if not paths:
        return 0
    src = "\0".join(os.path.abspath(p) for p in paths) + "\0\0"
    op = SHFILEOPSTRUCTW()
    op.hwnd = None
    op.wFunc = FO_DELETE
    op.pFrom = src
    op.pTo = None
    op.fFlags = (FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT
                 | FOF_NOERRORUI | FOF_NOCONFIRMMKDIR)
    op.fAnyOperationsAborted = False
    op.hNameMappings = None
    op.lpszProgressTitle = None
    return ctypes.windll.shell32.SHFileOperationW(ctypes.byref(op))


def prune_empty_dirs(start_dirs):
    """自底向上删除空目录（从 Assets 起，绝不越过 Assets）"""
    assets = os.path.join(ROOT, "Assets")
    removed = 0
    for d in sorted(set(start_dirs), key=len, reverse=True):
        cur = d
        while cur.startswith(assets) and cur != assets:
            try:
                if os.path.isdir(cur) and not os.listdir(cur):
                    os.rmdir(cur)
                    removed += 1
                    cur = os.path.dirname(cur)
                else:
                    break
            except OSError:
                break
    return removed


def main():
    args = sys.argv[1:]
    apply = "--apply" in args
    tsv = None
    for i, a in enumerate(args):
        if a == "--list" and i + 1 < len(args):
            tsv = args[i + 1]
    if not tsv:
        tsv = "delete_safe.tsv"
    tsv_path = tsv if os.path.isabs(tsv) else os.path.join(HERE, tsv)

    paths = []
    with open(tsv_path, encoding="utf-8") as f:
        next(f)
        for line in f:
            p = line.strip()
            if p:
                paths.append(p)

    exist = []
    missing = []
    total = 0
    for p in paths:
        fp = os.path.join(ROOT, p)
        if os.path.exists(fp):
            exist.append(p)
            try:
                total += os.path.getsize(fp)
            except OSError:
                pass
        else:
            missing.append(p)

    print("清单：%s" % os.path.basename(tsv_path))
    print("  条目 %d，存在 %d (%.1f MB)，缺失 %d"
          % (len(paths), len(exist), total / 1048576.0, len(missing)))
    if missing:
        for m in missing[:10]:
            print("    缺失: %s" % m)

    if not apply:
        print("\n[干跑] 未执行删除。加 --apply 才会移入回收站。")
        return

    BATCH = 400
    done = 0
    dirs = set()
    for i in range(0, len(exist), BATCH):
        batch = [os.path.join(ROOT, p) for p in exist[i:i + BATCH]]
        for p in exist[i:i + BATCH]:
            dirs.add(os.path.dirname(os.path.join(ROOT, p)))
        res = recycle(batch)
        remain = sum(1 for b in batch if os.path.exists(b))
        ok = len(batch) - remain
        done += ok
        n = i + len(batch)
        print("  批 %d: 目标 %d, 成功 %d, 残留 %d (ret=%d) [%d/%d]"
              % (i // BATCH + 1, len(batch), ok, remain, res, n, len(exist)))
        if remain:
            print("    !! 有残留，可能被占用；已停止")
            break

    print("\n已移入回收站：%d 项" % done)
    nd = prune_empty_dirs(dirs)
    print("清理空目录：%d 个" % nd)


if __name__ == "__main__":
    main()
