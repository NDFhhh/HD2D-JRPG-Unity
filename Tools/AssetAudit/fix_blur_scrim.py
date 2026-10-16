#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
把丢着色器的模糊材质改造成 UGUI 安全的「半透明深色遮罩」材质。

做四件事：
  1. 给新建的 Assets/Martials/UI Scrim.shader 生成 .meta（新 guid）
  2. 用 Assets/Martials/UniversalBlurUI.mat 的**原 guid** 改写出一份 UI Scrim.mat，
     这样 Persistent.unity 里 ShopAction Panel 的引用不用动也依然有效
  3. 删掉改名后的旧 UniversalBlurUI.mat / .meta；把 ShopAction Panel 那个 Image 的
     m_Color 归一成白色（让遮罩深浅只由材质的 _Color 决定，避免两处相乘）
  4. 顺手把 Assets/PostProcess/SGS_VolumeProfile.asset（SRP Core 的 VolumeProfile 残留）
     挪到 _bak_pipeline_switch/

用法：python fix_blur_scrim.py [--apply]
"""

import io
import os
import re
import shutil
import sys
import uuid

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
BAK = os.path.join(ROOT, "_bak_pipeline_switch")

SHADER = "Assets/Martials/UI Scrim.shader"
OLD_MAT = "Assets/Martials/UniversalBlurUI.mat"
NEW_MAT = "Assets/Martials/UI Scrim.mat"
SCENE = "Assets/Scenes/Persistent/Persistent.unity"
SGS = "Assets/PostProcess/SGS_VolumeProfile.asset"

# ShopAction Panel 上那个 Image 的组件 id（前面已排查确认）
IMAGE_COMP = "795452619"

META_TEMPLATE = """fileFormatVersion: 2
guid: %s
ShaderImporter:
  externalObjects: {}
  defaultTextures: []
  nonModifiableTextures: []
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

MAT_TEMPLATE = """%%YAML 1.1
%%TAG !u! tag:unity3d.com,2011:
--- !u!21 &2100000
Material:
  serializedVersion: 8
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name: UI Scrim
  m_Shader: {fileID: 4800000, guid: %s, type: 3}
  m_Parent: {fileID: 0}
  m_ModifiedSerializedProperties: 0
  m_ValidKeywords: []
  m_InvalidKeywords: []
  m_LightmapFlags: 4
  m_EnableInstancingVariants: 0
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: -1
  stringTagMap: {}
  disabledShaderPasses: []
  m_LockedProperties: 
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs:
    - _MainTex:
        m_Texture: {fileID: 0}
        m_Scale: {x: 1, y: 1}
        m_Offset: {x: 0, y: 0}
    - unity_Lightmaps:
        m_Texture: {fileID: 0}
        m_Scale: {x: 1, y: 1}
        m_Offset: {x: 0, y: 0}
    - unity_LightmapsInd:
        m_Texture: {fileID: 0}
        m_Scale: {x: 1, y: 1}
        m_Offset: {x: 0, y: 0}
    - unity_ShadowMasks:
        m_Texture: {fileID: 0}
        m_Scale: {x: 1, y: 1}
        m_Offset: {x: 0, y: 0}
    m_Ints: []
    m_Floats:
    - _ColorMask: 15
    - _Stencil: 0
    - _StencilComp: 8
    - _StencilOp: 0
    - _StencilReadMask: 255
    - _StencilWriteMask: 255
    - _UIMaskSoftnessX: 1
    - _UIMaskSoftnessY: 1
    - _UseUIAlphaClip: 0
    m_Colors:
    - _ClipRect: {r: 0, g: 0, b: 0, a: 0}
    - _Color: {r: 0.05, g: 0.05, b: 0.07, a: 0.7}
  m_BuildTextureStacks: []
"""


def full(p):
    return os.path.join(ROOT, p)


def main():
    apply_ = "--apply" in sys.argv

    # ---------- 0. 自检 ----------
    for p in (SHADER, OLD_MAT, OLD_MAT + ".meta", SCENE):
        if not os.path.exists(full(p)):
            print("缺少文件，中止：%s" % p)
            return 1
    if os.path.exists(full(NEW_MAT)):
        print("目标已存在，中止：%s" % NEW_MAT)
        return 1

    old_meta = io.open(full(OLD_MAT + ".meta"), encoding="utf-8").read()
    mat_guid = re.search(r"^guid: ([0-9a-f]{32})", old_meta, re.M).group(1)
    shader_guid = uuid.uuid4().hex
    print("材质沿用原 guid : %s   （场景引用不用改）" % mat_guid)
    print("新着色器 guid   : %s" % shader_guid)

    # ---------- 1. 场景：Image 颜色归一 ----------
    s = io.open(full(SCENE), encoding="utf-8", errors="replace").read()
    m = re.search(
        r"(?m)^--- !u!114 &" + IMAGE_COMP + r"(?: stripped)?[ \t]*\n(.*?)(?=^--- |\Z)", s, re.S | re.M
    )
    if not m:
        print("!! 场景里找不到 Image 组件 &%s" % IMAGE_COMP)
        return 1
    body = m.group(1)
    old_color = re.search(r"m_Color: (\{[^}]*\})", body).group(1)
    new_body = re.sub(r"m_Color: \{[^}]*\}", "m_Color: {r: 1, g: 1, b: 1, a: 1}", body, count=1)
    new_s = s[: m.start(1)] + new_body + s[m.end(1) :]

    print()
    print("=== 场景 ===")
    print("  ShopAction Panel 的 Image m_Color: %s -> {r: 1, g: 1, b: 1, a: 1}" % old_color)
    print("      （遮罩深浅改由材质 _Color 决定，避免两处相乘）")

    # ---------- 2. SGS_VolumeProfile 挪走 ----------
    print()
    print("=== SRP Core 残留 ===")
    if os.path.exists(full(SGS)):
        print("  Assets/PostProcess/SGS_VolumeProfile.asset -> _bak_pipeline_switch/（唯一引用方是它自己）")
        if apply_:
            dst = os.path.join(BAK, "SGS_VolumeProfile.asset")
            if not os.path.exists(dst):
                shutil.move(full(SGS), dst)
            if os.path.exists(full(SGS + ".meta")) and not os.path.exists(dst + ".meta"):
                shutil.move(full(SGS + ".meta"), dst + ".meta")
    else:
        print("  已不在工程里")

    if not apply_:
        print()
        print("（dry-run，未写入）")
        return 0

    # ---------- 3. 落盘 ----------
    io.open(full(SHADER + ".meta"), "w", encoding="utf-8", newline="\n").write(META_TEMPLATE % shader_guid)
    io.open(full(NEW_MAT), "w", encoding="utf-8", newline="\n").write(MAT_TEMPLATE % shader_guid)
    io.open(full(NEW_MAT + ".meta"), "w", encoding="utf-8", newline="\n").write(
        old_meta.replace(mat_guid, mat_guid)  # guid 原样保留
    )
    os.remove(full(OLD_MAT))
    os.remove(full(OLD_MAT + ".meta"))
    io.open(full(SCENE), "w", encoding="utf-8", newline="\n").write(new_s)

    # 中间态备份（Standard Fade 版）没意义，清掉免得和真原件混淆
    junk = os.path.join(BAK, "materials_std", "Assets__Martials__UniversalBlurUI.mat")
    if os.path.exists(junk):
        os.remove(junk)

    print()
    print("已写入：")
    print("  + %s (+ .meta)" % SHADER)
    print("  + %s (+ .meta，沿用 guid %s)" % (NEW_MAT, mat_guid))
    print("  - %s (+ .meta)" % OLD_MAT)
    print("  ~ %s" % SCENE)
    return 0


if __name__ == "__main__":
    sys.exit(main())
