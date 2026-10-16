# 未使用资源清理方案

扫描结论：**候选孤儿 6230 个 / 1138.2 MB**

- **安全删除**：6062 个资产 / 1060.5 MB
- **需确认**：168 个资产 / 77.8 MB

> 判定方法：从工程入口（Addressables 场景组 + Build Settings + Resources +
> ProjectSettings）做引用图可达性遍历。已通过反向自洽验证（全工程重扫，
> 「非孤儿引用孤儿」= 0 处）。

## 按品类汇总

| 品类 | 安全删除 | 需确认 | 合计体积 |
|---|---:|---:|---:|
| A. 场景资源包 Assets/Prefabs/场景资源 | 4998 | 0 | 889.2 MB |
| B. 第三方插件 Demo/示例 | 806 | 67 | 123.3 MB |
| C. Art/Characters 未使用精灵 | 84 | 0 | 12.0 MB |
| D. Art/UI 未使用精灵 | 72 | 0 | 7.0 MB |
| E. Art/Icons 未使用图标 | 47 | 0 | 1.0 MB |
| F. Art/Mask 未使用遮罩 | 1 | 0 | 0.6 MB |
| G. Art/Fonts 未使用字体 | 0 | 2 | 0.0 MB |
| H. Art/Audio 未使用音频 | 2 | 0 | 6.0 MB |
| J. 场景文件 .unity | 0 | 1 | 0.0 MB |
| K. Environment 未使用 | 14 | 0 | 0.4 MB |
| L. Martials/Terrains 未使用 | 21 | 1 | 20.1 MB |
| M. TextMesh Pro 未使用 | 7 | 14 | 0.8 MB |
| N. Scenes 内光照/烘焙数据 | 2 | 25 | 0.0 MB |
| O. Scripts/Settings | 0 | 57 | 0.0 MB |
| Z. 其他散落 | 8 | 1 | 0.0 MB |

## 需确认清单（168 个）

| 原因 | 数量 | 体积 |
|---|---:|---:|
| 字体（Arts/Fonts） | 1 | 32.0 MB |
| 后缀 .otf | 1 | 20.4 MB |
| 光照/烘焙数据（Lightmap） | 21 | 18.1 MB |
| 后缀 .unity | 9 | 4.5 MB |
| 光照/烘焙数据（ReflectionProbe） | 7 | 1.8 MB |
| 后缀 .cs | 92 | 0.5 MB |
| 光照/烘焙数据（LightingData） | 2 | 0.2 MB |
| 后缀 .cginc | 9 | 0.1 MB |
| 后缀 .mdb | 2 | 0.1 MB |
| 后缀 .shader | 13 | 0.1 MB |
| 后缀 .dll | 1 | 0.1 MB |
| 后缀 .shadergraph | 1 | 0.0 MB |
| 后缀 .uxml | 1 | 0.0 MB |
| 后缀 .asmdef | 6 | 0.0 MB |
| 后缀 .uss | 1 | 0.0 MB |
| 后缀 .rsp | 1 | 0.0 MB |
