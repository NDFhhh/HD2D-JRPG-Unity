#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
把 URP 材质还原成 Built-in 管线能用的材质。

本工程移除 URP 后，46 个 URP Lit 材质与 1 个模糊材质全部变洋红。
这个脚本做三件事：

  1. URP Lit / 无光照 URP 材质 -> Built-in `Standard`
     属性映射：_BaseMap->_MainTex、_BaseColor->_Color
     模式映射：_Surface/_AlphaClip -> _Mode（0 Opaque / 1 Cutout / 2 Fade / 3 Transparent）
  2. 模糊材质 -> `Standard` 的 Fade 模式（半透明深色遮罩）
  3. 顺手摘掉每个材质里 URP 留下的 MaterialVersion 伴生块（script guid d0353a89…）

用法：
    python fix_urp_materials.py                # dry-run
    python fix_urp_materials.py --apply
    python fix_urp_materials.py --apply --all  # 连同 Assets/Environment/Materials/* 一起转
"""

import io
import os
import re
import shutil
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
BAK = os.path.join(ROOT, "_bak_pipeline_switch", "materials_std")

BUILTIN_GUID = "0000000000000000f000000000000000"
STANDARD_FID = "46"            # 本工程已验证：Standard.shader 的 fileID
URP_LIT = "933532a4fcc9baf4fa0491de14d08ed7"
BLUR_SHADER = "a7b13085d76ad4a579b8cd4b744827a9"
MATERIAL_VERSION_SCRIPT = "d0353a89b1f911e48b9e16bdc9f2e058"

SHADOW_MATS = [
    "Assets/Martials/Material Shadow Battle.mat",
    "Assets/Martials/Material Shadow Field.mat",
    "Assets/Martials/Material Shadow NPC.mat",
]
BLUR_MAT = "Assets/Martials/UniversalBlurUI.mat"

# 只有当 --all 时才动，它们是待定去留的孤儿资产
ENV_MAT_GLOB_DIR = "Assets/Environment/Materials"

TEXENV_RE = re.compile(
    r"(?m)^    - (\w+):\n"
    r"        m_Texture: (\{.*?\})\n"
    r"        m_Scale: (\{.*?\})\n"
    r"        m_Offset: (\{.*?\})\n"
)


def parse_saved_props(text):
    """把 m_SavedProperties 拆成 名字->值 的三张表"""
    texenvs = {m.group(1): (m.group(2), m.group(3), m.group(4)) for m in TEXENV_RE.finditer(text)}

    i_f = text.index("    m_Floats:")
    i_c = text.index("    m_Colors:")
    i_end = text.index("  m_BuildTextureStacks:")

    floats = {}
    for line in text[i_f:i_c].splitlines():
        m = re.match(r"^    - (\w+): (.*)$", line)
        if m:
            # 小数在 Unity 的序列化里可能写成浮点后缀形式，原样带走
            floats[m.group(1)] = m.group(2).strip()

    colors = {}
    for line in text[i_c:i_end].splitlines():
        m = re.match(r"^    - (\w+): (\{.*\})$", line)
        if m:
            colors[m.group(1)] = m.group(2)

    return texenvs, floats, colors


def tex_line(name, data):
    t, s, o = data
    return (
        "    - %s:\n"
        "        m_Texture: %s\n"
        "        m_Scale: %s\n"
        "        m_Offset: %s\n" % (name, t, s, o)
    )


def fnum(floats, key, default):
    v = floats.get(key)
    if v is None:
        return default
    try:
        return float(v)
    except ValueError:
        return default


def build_standard(name, queue, string_tag, texenvs, floats, colors, mode, dst):
    """按 Built-in Standard 的属性表输出一份材质文本"""

    # --- 贴图：_BaseMap 改名 _MainTex，其余同名搬运 ---
    std_tex_order = [
        "_BumpMap",
        "_DetailAlbedoMap",
        "_DetailMask",
        "_DetailNormalMap",
        "_EmissionMap",
        "_MainTex",
        "_MetallicGlossMap",
        "_OcclusionMap",
        "_ParallaxMap",
    ]
    empty = ("{fileID: 0}", "{x: 1, y: 1}", "{x: 0, y: 0}")
    src_main = texenvs.get("_BaseMap") or texenvs.get("_MainTex") or empty

    tex = {}
    for k in std_tex_order:
        tex[k] = texenvs.get(k, empty)
    tex["_MainTex"] = src_main

    # --- 关键字：由模式 + 反射/高光开关推导 ---
    kw = []
    if mode == 1:
        kw.append("_ALPHATEST_ON")
    elif mode == 2:
        kw.append("_ALPHABLEND_ON")
    elif mode == 3:
        kw.append("_ALPHAPREMULTIPLY_ON")
    if fnum(floats, "_GlossyReflections", 1) == 0:
        kw.append("_GLOSSYREFLECTIONS_OFF")
    if fnum(floats, "_SpecularHighlights", 1) == 0:
        kw.append("_SPECULARHIGHLIGHTS_OFF")

    # --- 混合状态跟着模式走 ---
    if mode in (2, 3):
        dst["srcblend"] = "1" if mode == 3 else "10"
        dst["dstblend"] = "10" if mode == 3 else "5"
        dst["zwrite"] = "0"
    else:
        dst["srcblend"] = "1"
        dst["dstblend"] = "0"
        dst["zwrite"] = "1"

    color = colors.get("_Color") or colors.get("_BaseColor") or "{r: 1, g: 1, b: 1, a: 1}"

    std_floats = [
        ("_BumpScale", floats.get("_BumpScale", "1")),
        ("_Cull", floats.get("_Cull", "2")),
        ("_Cutoff", floats.get("_Cutoff", "0.5")),
        ("_DetailNormalMapScale", floats.get("_DetailNormalMapScale", "1")),
        ("_DstBlend", dst["dstblend"]),
        ("_GlossMapScale", floats.get("_GlossMapScale", "1")),
        ("_Glossiness", floats.get("_Glossiness", "0.5")),
        ("_GlossyReflections", floats.get("_GlossyReflections", "1")),
        ("_Metallic", floats.get("_Metallic", "0")),
        ("_Mode", str(mode)),
        ("_OcclusionStrength", floats.get("_OcclusionStrength", "1")),
        ("_Parallax", floats.get("_Parallax", "0.02")),
        ("_SmoothnessTextureChannel", floats.get("_SmoothnessTextureChannel", "0")),
        ("_SpecularHighlights", floats.get("_SpecularHighlights", "1")),
        ("_SrcBlend", dst["srcblend"]),
        ("_UVSec", "0"),
        ("_ZWrite", dst["zwrite"]),
    ]

    out = []
    out.append("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n")
    out.append("--- !u!21 &2100000\nMaterial:\n")
    out.append("  serializedVersion: 8\n")
    out.append("  m_ObjectHideFlags: 0\n")
    out.append("  m_CorrespondingSourceObject: {fileID: 0}\n")
    out.append("  m_PrefabInstance: {fileID: 0}\n")
    out.append("  m_PrefabAsset: {fileID: 0}\n")
    out.append("  m_Name: %s\n" % name)
    out.append("  m_Shader: {fileID: %s, guid: %s, type: 0}\n" % (STANDARD_FID, BUILTIN_GUID))
    out.append("  m_Parent: {fileID: 0}\n")
    out.append("  m_ModifiedSerializedProperties: 0\n")
    if kw:
        out.append("  m_ValidKeywords:\n")
        for k in kw:
            out.append("  - %s\n" % k)
    else:
        out.append("  m_ValidKeywords: []\n")
    out.append("  m_InvalidKeywords: []\n")
    out.append("  m_LightmapFlags: 4\n")
    out.append("  m_EnableInstancingVariants: 0\n")
    out.append("  m_DoubleSidedGI: 0\n")
    out.append("  m_CustomRenderQueue: %s\n" % queue)
    if string_tag:
        out.append("  stringTagMap:\n")
        for k, v in string_tag:
            out.append("    %s: %s\n" % (k, v))
    else:
        out.append("  stringTagMap: {}\n")
    out.append("  disabledShaderPasses: []\n")
    out.append("  m_LockedProperties: \n")
    out.append("  m_SavedProperties:\n")
    out.append("    serializedVersion: 3\n")
    out.append("    m_TexEnvs:\n")
    for k in std_tex_order:
        out.append(tex_line(k, tex[k]))
    out.append("    m_Ints: []\n")
    out.append("    m_Floats:\n")
    for k, v in std_floats:
        out.append("    - %s: %s\n" % (k, v))
    out.append("    m_Colors:\n")
    out.append("    - _Color: %s\n" % color)
    out.append("    - _EmissionColor: %s\n" % colors.get("_EmissionColor", "{r: 0, g: 0, b: 0, a: 1}"))
    out.append("  m_BuildTextureStacks: []\n")
    return "".join(out)


def strip_material_version(text):
    """摘掉 URP 留下的 MaterialVersion 伴生块（不含马 script guid 的整个 --- !u!114 块）"""
    removed = 0
    while True:
        m = re.search(
            r"(?m)^--- !u!114 &-?\d+(?: stripped)?[ \t]*\n(.*?)(?=^--- |\Z)", text, re.S | re.M
        )
        if not m or MATERIAL_VERSION_SCRIPT not in m.group(1):
            break
        text = text[: m.start()] + text[m.end() :]
        removed += 1
    return text, removed


def write(full, rel, new_text, apply_, note):
    print("    %s" % note)
    if apply_:
        if not os.path.isdir(BAK):
            os.makedirs(BAK)
        bf = os.path.join(BAK, rel.replace("/", "__"))
        if not os.path.exists(bf):
            shutil.copy2(full, bf)
        io.open(full, "w", encoding="utf-8", newline="\n").write(new_text)


def convert_to_standard(rel, apply_, mode_override=None, queue_override=None):
    full = os.path.join(ROOT, rel)
    if not os.path.exists(full):
        print("  - %s 不存在，跳过" % rel)
        return False

    text = io.open(full, encoding="utf-8", errors="replace").read()
    name = re.search(r"(?m)^  m_Name: (.*)$", text).group(1).strip()
    shader = re.search(r"m_Shader: \{fileID: -?\d+, guid: ([0-9a-f]{32})", text).group(1)
    queue = re.search(r"(?m)^  m_CustomRenderQueue: (-?\d+)$", text).group(1)

    texenvs, floats, colors = parse_saved_props(text)

    # 目标模式：Cutout 优先（_AlphaClip=1 且带 _ALPHATEST_ON），否则看 _Surface
    if mode_override is not None:
        mode = mode_override
    elif fnum(floats, "_AlphaClip", 0) >= 1:
        mode = 1
    elif fnum(floats, "_Surface", 0) >= 1:
        mode = 3
    else:
        mode = 0

    if queue_override is not None:
        queue = str(queue_override)

    tags = []
    if mode == 1:
        tags.append(("RenderType", "TransparentCutout"))
    elif mode in (2, 3):
        tags.append(("RenderType", "Transparent"))
    if mode == 3:
        tags.append(("IgnoreProjector", "True"))

    dst = {}
    new_text = build_standard(name, queue, tags, texenvs, floats, colors, mode, dst)
    new_text, n_ver = strip_material_version(new_text)

    print("  * %s" % rel)
    print(
        "      shader %s -> Standard    mode=%d(_Mode)  queue=%s  blend=%s/%s  zwrite=%s"
        % (shader[:8], mode, queue, dst["srcblend"], dst["dstblend"], dst["zwrite"])
    )
    print(
        "      _Color=%s   _AlphaClip=%s -> 关键字 %s"
        % (colors.get("_Color") or colors.get("_BaseColor"), floats.get("_AlphaClip", "-"), " ".join(
            re.findall(r"(?m)^  - (\w+)$", new_text)) or "(无)"
        )
    )
    if n_ver:
        print("      已摘掉 %d 个 MaterialVersion 伴生块" % n_ver)
    write(full, rel, new_text, apply_, "已转换" if apply_ else "[dry-run] 未写入")
    return True


def convert_blur(rel, apply_):
    """模糊材质 -> Standard 的 Fade 模式，做成半透明深色遮罩"""
    full = os.path.join(ROOT, rel)
    if not os.path.exists(full):
        print("  - %s 不存在，跳过" % rel)
        return False

    text = io.open(full, encoding="utf-8", errors="replace").read()
    name = re.search(r"(?m)^  m_Name: (.*)$", text).group(1).strip()
    old = re.search(r"m_Shader: \{fileID: -?\d+, guid: ([0-9a-f]{32})", text)

    texenvs, floats, colors = parse_saved_props(text)
    # 遮罩色：深灰蓝、约 70% 不透明。用户选的就是「半透明深色遮罩」
    colors["_Color"] = "{r: 0.05, g: 0.05, b: 0.07, a: 0.7}"
    floats["_Glossiness"] = "0"
    floats["_GlossyReflections"] = "0"
    floats["_SpecularHighlights"] = "0"

    dst = {}
    new_text = build_standard(name, "3000", [("RenderType", "Transparent")], texenvs, floats, colors, 2, dst)
    new_text, n_ver = strip_material_version(new_text)

    print("  * %s" % rel)
    print("      shader %s（模糊包，包已删）-> Standard Fade" % (old.group(1)[:8] if old else "?"))
    print("      _Color -> {0.05, 0.05, 0.07, 0.7}   queue=3000  blend=%s/%s  zwrite=%s"
          % (dst["srcblend"], dst["dstblend"], dst["zwrite"]))
    if n_ver:
        print("      已摘掉 %d 个 MaterialVersion 伴生块" % n_ver)
    write(full, rel, new_text, apply_, "已转换" if apply_ else "[dry-run] 未写入")
    return True


def strip_version_everywhere(apply_):
    """遍历全工程 .mat，摘掉 URP 留下的 MaterialVersion 伴生块"""
    print("=== 全工程清理 MaterialVersion 伴生块 ===")
    n_files = n_blocks = 0
    for dp, _dns, fns in os.walk(os.path.join(ROOT, "Assets")):
        for fn in fns:
            if not fn.endswith(".mat"):
                continue
            full = os.path.join(dp, fn)
            rel = os.path.relpath(full, ROOT).replace("\\", "/")
            try:
                text = io.open(full, encoding="utf-8", errors="replace").read()
            except OSError:
                continue
            if MATERIAL_VERSION_SCRIPT not in text:
                continue
            new_text, n = strip_material_version(text)
            if not n:
                continue
            print("  - %s  (%d 块)" % (rel, n))
            n_files += 1
            n_blocks += n
            write(full, rel, new_text, apply_, "已清理" if apply_ else "[dry-run] 未写入")
    print("合计 %d 个文件 / %d 个伴生块%s" % (n_files, n_blocks, "" if apply_ else "（dry-run）"))


def main():
    apply_ = "--apply" in sys.argv
    do_all = "--all" in sys.argv

    if "--strip-version" in sys.argv:
        strip_version_everywhere(apply_)
        return

    print("=== 三个 Shadow 材质：URP Lit -> Standard Cutout ===")
    for rel in SHADOW_MATS:
        convert_to_standard(rel, apply_)

    print()
    print("=== 模糊材质 -> Standard Fade 深色遮罩 ===")
    convert_blur(BLUR_MAT, apply_)

    if do_all:
        d = os.path.join(ROOT, ENV_MAT_GLOB_DIR)
        print()
        print("=== Assets/Environment/Materials/*（--all） ===")
        for fn in sorted(os.listdir(d)):
            if not fn.endswith(".mat"):
                continue
            rel = "%s/%s" % (ENV_MAT_GLOB_DIR, fn)
            full = os.path.join(ROOT, rel)
            t = io.open(full, encoding="utf-8", errors="replace").read(900)
            m = re.search(r"m_Shader: \{fileID: -?\d+, guid: ([0-9a-f]{32})", t)
            if not m or m.group(1) != URP_LIT:
                continue
            convert_to_standard(rel, apply_)

    print()
    print("完成%s" % ("" if apply_ else "（dry-run，未写入）"))


if __name__ == "__main__":
    main()
