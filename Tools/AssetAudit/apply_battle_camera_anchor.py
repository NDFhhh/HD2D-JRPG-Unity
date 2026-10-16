# -*- coding: utf-8 -*-
"""
给战斗相机接入「场景锚点」的定向 patch。

改造内容（三处）：
  A. Persistent.unity
     A1. 把 Battle Camera(vcam) 的 local 位姿归零 —— 相机不再自带构图，
         位姿统一由 Battle Camera Root 决定（演出补间写的也是 Root）。
     A2. 给 Battle Camera Root 挂 BattleCameraRig，并接上:
         - cameraRoot        -> 自己的 Transform
         - cinematicService  -> 场景里既有的 BattleCinematicService
         - fallback          -> 改造前的固定机位 (0,3.5,-17)/8°，找不到锚点时回落
  B. Demo_SanXingDui.unity
     在战斗场地根 Battle Layout 下新建 "Battle Camera Anchor"，
     局部位姿 = 改造前相机的世界位姿在该父节点下的等价表达（保证首次运行画面不变）。

设计约定：
  * 锚点用普通 Transform（不是 RectTransform）—— RectTransform 的值会被 anchor/pivot/size
    重算，当世界坐标锚点会静默漂移。
  * 只做「块级定向替换」：每个改动都限定在唯一的 !u!N &fid 块内，绝不全文子串替换。
  * 默认干跑；--apply 才落盘，落盘前先备份到 _bak_camera_anchor/。

用法:
    python apply_battle_camera_anchor.py            # 干跑，只看改动与校验
    python apply_battle_camera_anchor.py --apply    # 落盘
"""

import argparse
import os
import re
import random
import shutil
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SCENES = os.path.join(ROOT, "Assets", "Scenes")
PERSISTENT = os.path.join(SCENES, "Persistent", "Persistent.unity")
DEMO = os.path.join(SCENES, "Demo_SanXingDui.unity")
ANCHOR_META = os.path.join(ROOT, "Assets", "Scripts", "Game", "Camera", "BattleCameraAnchor.cs.meta")
RIG_META = os.path.join(ROOT, "Assets", "Scripts", "Game", "Camera", "BattleCameraRig.cs.meta")
BACKUP_DIR = os.path.join(ROOT, "_bak_camera_anchor")

# ---- 目标 fileID（来自场景实测）--------------------------------------------------
P_VCAM_TR = "381737310"              # Persistent: Battle Camera 的 Transform
P_ROOT_GO = "300037648"              # Persistent: Battle Camera Root 的 GameObject
P_ROOT_TR = "300037649"              # Persistent: Battle Camera Root 的 Transform
P_CINEMATIC_MB = "1981802464"        # Persistent: BattleCinematicService 实例
D_LAYOUT_TR = "3224848827432313426"  # Demo: Battle Layout 的 Transform（战斗场地根）
D_LAYOUT_LAST_CHILD = "8875293103242018683"  # Demo: 场地根 m_Children 的末项

ANCHOR_NAME = "Battle Camera Anchor"
# 改造前相机世界位姿 pos(0,3.5,-17) / euler(8,0,0)；
# 场地根 (-6.359999, 2.37, -6.47) 且自身无旋转 -> 局部量如下
ANCHOR_LOCAL_POS = ("6.359999", "1.13", "-10.53")
ANCHOR_LOCAL_ROT = ("0.06975647", "0", "0", "0.9975641")
ANCHOR_EULER_HINT = ("8", "0", "0")


def log(msg):
    print(msg)


def read_guid(meta_path):
    with open(meta_path, encoding="utf-8") as f:
        for line in f:
            m = re.match(r"guid: ([0-9a-f]{32})", line.strip())
            if m:
                return m.group(1)
    raise RuntimeError("meta 里找不到 guid: %s" % meta_path)


def load(path):
    with open(path, encoding="utf-8") as f:
        return f.read()


def save(path, text):
    with open(path, "w", encoding="utf-8", newline="") as f:
        f.write(text)


def backup(paths):
    os.makedirs(BACKUP_DIR, exist_ok=True)
    for p in paths:
        dst = os.path.join(BACKUP_DIR, os.path.basename(p) + ".bak")
        n = 2
        while os.path.exists(dst):
            dst = os.path.join(BACKUP_DIR, os.path.basename(p) + ".bak.%d" % n)
            n += 1
        shutil.copy2(p, dst)
        log("  备份 -> %s" % os.path.relpath(dst, ROOT))


# ---- 块级解析 -------------------------------------------------------------------
DOC_RE = re.compile(r"^--- !u!\d+ &(\d+).*?(?=^--- !u!|\Z)", re.M | re.S)


def docs(text):
    """返回 [(fid, start, end)]"""
    return [(m.group(1), m.start(), m.end()) for m in DOC_RE.finditer(text)]


def doc_text(text, fid):
    for f, s, e in docs(text):
        if f == fid:
            return text[s:e]
    return None


def all_fids(text):
    return [f for f, _, _ in docs(text)]


class FidAllocator:
    def __init__(self, text):
        self.used = set(all_fids(text))

    def next(self):
        while True:
            v = str(random.getrandbits(62))
            if v not in self.used:
                self.used.add(v)
                return v


def insert_doc(text, fid, block):
    """按 fileID 升序插入新块（Unity 自身的书写顺序）"""
    target = int(fid)
    for f, s, _ in docs(text):
        if int(f) > target:
            return text[:s] + block + text[s:]
    if not text.endswith("\n"):
        text += "\n"
    return text + block


def replace_in_doc(text, fid, pattern, repl, expect=1):
    """只在指定块内做替换，避免误伤同名字段的其他对象"""
    d = doc_text(text, fid)
    if d is None:
        raise RuntimeError("找不到块 %s" % fid)
    new_d, n = re.subn(pattern, repl, d, flags=re.M)
    if n != expect:
        raise RuntimeError("块 %s 内匹配 %d 次，期望 %d 次：%s" % (fid, n, expect, pattern))
    return text.replace(d, new_d, 1)


# ---- YAML 块构造 ---------------------------------------------------------------
def build_gameobject(fid, comp_fids, name):
    lines = [
        "--- !u!1 &%s" % fid,
        "GameObject:",
        "  m_ObjectHideFlags: 0",
        "  m_CorrespondingSourceObject: {fileID: 0}",
        "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}",
        "  serializedVersion: 6",
        "  m_Component:",
    ]
    lines += ["  - component: {fileID: %s}" % cf for cf in comp_fids]
    lines += [
        "  m_Layer: 0",
        "  m_Name: %s" % name,
        "  m_TagString: Untagged",
        "  m_Icon: {fileID: 0}",
        "  m_NavMeshLayer: 0",
        "  m_StaticEditorFlags: 0",
        "  m_IsActive: 1",
    ]
    return "\n".join(lines) + "\n"


def build_transform(fid, go_fid, parent_fid, local_pos, local_rot, euler):
    lines = [
        "--- !u!4 &%s" % fid,
        "Transform:",
        "  m_ObjectHideFlags: 0",
        "  m_CorrespondingSourceObject: {fileID: 0}",
        "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}",
        "  m_GameObject: {fileID: %s}" % go_fid,
        "  serializedVersion: 2",
        "  m_LocalRotation: {x: %s, y: %s, z: %s, w: %s}" % local_rot,
        "  m_LocalPosition: {x: %s, y: %s, z: %s}" % local_pos,
        "  m_LocalScale: {x: 1, y: 1, z: 1}",
        "  m_ConstrainProportionsScale: 0",
        "  m_Children: []",
        "  m_Father: {fileID: %s}" % parent_fid,
        "  m_LocalEulerAnglesHint: {x: %s, y: %s, z: %s}" % euler,
    ]
    return "\n".join(lines) + "\n"


def build_monobehaviour(fid, go_fid, guid, fields):
    lines = [
        "--- !u!114 &%s" % fid,
        "MonoBehaviour:",
        "  m_ObjectHideFlags: 0",
        "  m_CorrespondingSourceObject: {fileID: 0}",
        "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}",
        "  m_GameObject: {fileID: %s}" % go_fid,
        "  m_Enabled: 1",
        "  m_EditorHideFlags: 0",
        "  m_Script: {fileID: 11500000, guid: %s, type: 3}" % guid,
        "  m_Name: ",
        "  m_EditorClassIdentifier: ",
    ]
    lines += ["  %s: %s" % (k, v) for k, v in fields]
    return "\n".join(lines) + "\n"


# ---- 改造 A：Persistent --------------------------------------------------------
def patch_persistent(text, rig_guid):
    if "guid: %s" % rig_guid in text:
        log("[Persistent] 已存在 BattleCameraRig，跳过")
        return text, False

    text = replace_in_doc(
        text, P_VCAM_TR,
        r"^  m_LocalRotation: \{x: 0\.06975647, y: -0, z: -0, w: 0\.9975641\}",
        "  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}")
    text = replace_in_doc(
        text, P_VCAM_TR,
        r"^  m_LocalPosition: \{x: 0, y: 3\.5, z: -17\}",
        "  m_LocalPosition: {x: 0, y: 0, z: 0}")
    text = replace_in_doc(
        text, P_VCAM_TR,
        r"^  m_LocalEulerAnglesHint: \{x: 8, y: 0, z: 0\}",
        "  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}")
    log("[Persistent] A1 Battle Camera 归零: localPos->0  localRot->identity  eulerHint->0")

    mb_fid = FidAllocator(text).next()
    text = replace_in_doc(
        text, P_ROOT_GO,
        r"^(  - component: \{fileID: %s\})" % P_ROOT_TR,
        r"\1\n  - component: {fileID: %s}" % mb_fid)
    text = insert_doc(text, mb_fid, build_monobehaviour(
        mb_fid, P_ROOT_GO, rig_guid,
        [("cameraRoot", "{fileID: %s}" % P_ROOT_TR),
         ("fallbackPosition", "{x: 0, y: 3.5, z: -17}"),
         ("fallbackEuler", "{x: 8, y: 0, z: 0}"),
         ("cinematicService", "{fileID: %s}" % P_CINEMATIC_MB),
         ("logResolve", "0")]))
    log("[Persistent] A2 挂 BattleCameraRig: MB=%s -> Battle Camera Root(%s)" % (mb_fid, P_ROOT_GO))
    return text, True


# ---- 改造 B：Demo_SanXingDui ---------------------------------------------------
def patch_demo(text, anchor_guid):
    if "guid: %s" % anchor_guid in text:
        log("[Demo] 已存在 BattleCameraAnchor，跳过")
        return text, False

    alloc = FidAllocator(text)
    go_fid, tr_fid, mb_fid = alloc.next(), alloc.next(), alloc.next()

    text = replace_in_doc(
        text, D_LAYOUT_TR,
        r"^(  - \{fileID: %s\})" % D_LAYOUT_LAST_CHILD,
        r"\1\n  - {fileID: %s}" % tr_fid)

    text = insert_doc(text, go_fid, build_gameobject(go_fid, [tr_fid, mb_fid], ANCHOR_NAME))
    text = insert_doc(text, tr_fid, build_transform(
        tr_fid, go_fid, D_LAYOUT_TR, ANCHOR_LOCAL_POS, ANCHOR_LOCAL_ROT, ANCHOR_EULER_HINT))
    text = insert_doc(text, mb_fid, build_monobehaviour(
        mb_fid, go_fid, anchor_guid,
        [("positionOffset", "{x: 0, y: 0, z: 0}"),
         ("eulerOffset", "{x: 0, y: 0, z: 0}")]))
    log("[Demo] B 新建 %s: GO=%s TR=%s MB=%s 父=Battle Layout(%s)"
        % (ANCHOR_NAME, go_fid, tr_fid, mb_fid, D_LAYOUT_TR))
    return text, True


# ---- 自校验 -------------------------------------------------------------------
def verify(label, text, expect_blocks, must_exist_refs, guid_once):
    fids = all_fids(text)
    dup = sorted({f for f in fids if fids.count(f) > 1})
    problems = []
    if dup:
        problems.append("fileID 重复: %s" % dup)
    known = set(fids)
    for ref in must_exist_refs:
        if ref not in known:
            problems.append("引用孤儿: %s 在文件里没有对应块" % ref)
    for g in guid_once:
        n = text.count("guid: %s" % g)
        if n != 1:
            problems.append("guid %s 出现 %d 次（期望 1）" % (g, n))
    log("  [%s] 块数=%d 新增=%d 重复fileID=%d -> %s"
        % (label, len(fids), len(fids) - expect_blocks[0], len(dup), problems if problems else "OK"))
    return not problems


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--apply", action="store_true", help="实际落盘（默认为干跑）")
    args = ap.parse_args()

    for p in (PERSISTENT, DEMO, ANCHOR_META, RIG_META):
        if not os.path.exists(p):
            log("缺少文件: %s" % p)
            return 1

    anchor_guid = read_guid(ANCHOR_META)
    rig_guid = read_guid(RIG_META)
    log("脚本 guid: BattleCameraAnchor=%s  BattleCameraRig=%s" % (anchor_guid, rig_guid))
    log("模式: %s" % ("APPLY（会写盘）" if args.apply else "DRY-RUN（只读）"))

    p_text, d_text = load(PERSISTENT), load(DEMO)
    base_counts = (len(all_fids(p_text)), len(all_fids(d_text)))

    if args.apply:
        backup([PERSISTENT, DEMO])

    p_new, _ = patch_persistent(p_text, rig_guid)
    d_new, _ = patch_demo(d_text, anchor_guid)

    if args.apply:
        save(PERSISTENT, p_new)
        save(DEMO, d_new)
        # 写盘后以磁盘内容为准复校，避免"内存对、落盘错"
        p_new, d_new = load(PERSISTENT), load(DEMO)
        log("已写入 2 个场景")

    log("--- 自校验 ---")
    ok = verify("Persistent", p_new, (base_counts[0],), [P_ROOT_TR, P_CINEMATIC_MB], [rig_guid])
    ok &= verify("Demo", d_new, (base_counts[1],), [D_LAYOUT_TR], [anchor_guid])

    log("--- Battle Camera 归零复核 ---")
    for line in doc_text(p_new, P_VCAM_TR).splitlines():
        if "m_Local" in line:
            log("  " + line.strip())

    log("结果: %s" % ("通过" if ok else "有问题，请检查"))
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
