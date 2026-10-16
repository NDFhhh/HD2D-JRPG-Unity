# 未使用资源扫描报告

> 判定方式：从工程入口（Addressables 场景组 / Build Settings / Resources / ProjectSettings）
> 出发做引用图可达性遍历，遍历不到的资产即为候选孤儿。
> 已通过反向自洽验证（全工程重扫，非孤儿引用孤儿 = 0 处）。

- **候选孤儿合计：6230 个 / 1138.2 MB**
- 全部资产均为 Unity 文本序列化（Force Text），引用形式为 32 位 hex GUID

| 品类 | 数量 | 体积 |
|---|---:|---:|
| A. 场景资源包（Assets/Prefabs/场景资源） | 4998 | 889.2 MB |
| B. 第三方插件 Demo / 示例内容 | 873 | 125.2 MB |
| C. Art/Characters 未使用精灵 | 84 | 12.0 MB |
| D. Art/UI 未使用精灵 | 72 | 7.0 MB |
| E. Art/Icons 未使用图标 | 47 | 1.0 MB |
| F. Art/Mask 未使用遮罩 | 1 | 0.6 MB |
| G. Art/Fonts 未使用字体 | 2 | 52.4 MB |
| H. Art/Audio 未使用音频 | 2 | 6.0 MB |
| J. 场景文件（.unity） | 1 | 3.5 MB |
| K. Environment 未使用（模型/材质/贴图） | 14 | 0.4 MB |
| L. Martials/Terrains 未使用地形材质 | 22 | 20.1 MB |
| M. TextMesh Pro 未使用资源 | 21 | 0.9 MB |
| N. 其他 Scripts/Settings 等 | 57 | 0.2 MB |
| Z. 未归类 | 36 | 19.6 MB |

---

## A. 场景资源包（Assets/Prefabs/场景资源）

共 4998 个 / 889.2 MB

- `Assets/Prefabs/场景资源/模型/建筑/EnvBdgMD_City_B_Outdoor_Full.FBX`  (5.20 MB)
- `Assets/Prefabs/场景资源/模型/建筑/MbdMD_Co_T_Plain_L_A_Build_02.FBX`  (4.80 MB)
- `Assets/Prefabs/场景资源/贴图/建筑/EnvBdgTX_Castle_Raimei_A_cl.tga`  (4.00 MB)
- `Assets/Prefabs/场景资源/模型/建筑/MbdMD_T_Forest_S_GeneralShop.FBX`  (3.55 MB)
- `Assets/Prefabs/场景资源/模型/建筑组件/EnvObjMD_Mine_A_Cart_A.FBX`  (2.74 MB)
- `Assets/Prefabs/场景资源/模型/建筑/ObjMD_Theater_1F.FBX`  (2.70 MB)
- `Assets/Prefabs/场景资源/贴图/建筑组件/EnvMdrTX_Desert_Tower_A_cl.tga`  (2.64 MB)
- `Assets/Prefabs/场景资源/贴图/建筑组件/EnvMdrTX_Mansion_B_cl.tga`  (2.62 MB)
- `Assets/Prefabs/场景资源/模型/建筑/MbdMD_Co_T_Snow_M_B_Build_03.FBX`  (2.61 MB)
- `Assets/Prefabs/场景资源/模型/建筑/EnvBdgMD_Mount_Daiseidou_A_Outdoor.FBX`  (2.53 MB)
- `Assets/Prefabs/场景资源/模型/建筑组件/EnvMdrMD_Theater_A_Balcony_A.FBX`  (2.41 MB)
- `Assets/Prefabs/场景资源/贴图/建筑组件/EnvMdrTX_Ruins_A_cl.tga`  (2.41 MB)
- `Assets/Prefabs/场景资源/贴图/建筑组件/EnvMdrTX_Desert_Broken_A_cl.tga`  (2.40 MB)
- `Assets/Prefabs/场景资源/贴图/建筑组件/EnvMdrTX_Daiseidou_A_Glass_no_2.tga`  (2.37 MB)
- `Assets/Prefabs/场景资源/贴图/建筑组件/EnvMdrTX_Mansion_A_GrdCpt_A_cl.tga`  (2.35 MB)
- `Assets/Prefabs/场景资源/模型/建筑/ObjMD_Daiseido_A.FBX`  (2.33 MB)
- `Assets/Prefabs/场景资源/贴图/建筑组件/EnvMdrTX_Ruins_D_cl.tga`  (2.31 MB)
- `Assets/Prefabs/场景资源/贴图/建筑组件/EnvObjTX_MayCastle_A_cl.tga`  (2.26 MB)
- `Assets/Prefabs/场景资源/贴图/建筑组件/EnvObjTX_Atlantis_A_cl.tga`  (2.24 MB)
- `Assets/Prefabs/场景资源/贴图/建筑组件/EnvMdrTX_Desert_Castle_A_GrdCpt_A_cl.tga`  (2.24 MB)
- `Assets/Prefabs/场景资源/贴图/建筑组件/EnvMdrTX_Snow_B_Jail_A_cl.tga`  (2.24 MB)
- `Assets/Prefabs/场景资源/贴图/建筑组件/EnvMdrTX_Ruins_C_cl.tga`  (2.22 MB)
- `Assets/Prefabs/场景资源/贴图/建筑组件/EnvMdrTX_Ruins_A_Wall_Fresco_A_cl.tga`  (2.22 MB)
- `Assets/Prefabs/场景资源/贴图/建筑组件/EnvMdrTX_SnowWall_A_cl.tga`  (2.21 MB)
- `Assets/Prefabs/场景资源/模型/建筑组件/EnvMdrMD_Ruins_D_Wall_CoIn_A.FBX`  (2.17 MB)
- `Assets/Prefabs/场景资源/贴图/建筑组件/EnvMdrTX_Desert_Castle_A_cl.tga`  (2.17 MB)
- `Assets/Prefabs/场景资源/贴图/建筑/EnvBdgTX_City_B_Outdoor_cl.tga`  (2.15 MB)
- `Assets/Prefabs/场景资源/贴图/船/EnvObjTX_Ship_Broken_A_cl.tga`  (2.14 MB)
- `Assets/Prefabs/场景资源/贴图/船/EnvObjTX_Ship_A_cl.tga`  (2.13 MB)
- `Assets/Prefabs/场景资源/贴图/建筑组件/EnvMdrTX_Ship_B_cl.tga`  (2.10 MB)
- `Assets/Prefabs/场景资源/贴图/建筑/ObjTX_Theater_A_cl.tga`  (2.10 MB)
- `Assets/Prefabs/场景资源/贴图/建筑组件/EnvMdrTX_Ship_E_cl.tga`  (2.09 MB)
- `Assets/Prefabs/场景资源/贴图/建筑/EnvBdgTX_Factory_B_cl.tga`  (2.06 MB)
- `Assets/Prefabs/场景资源/贴图/建筑组件/EnvMdrTX_Ship_A_cl.tga`  (2.03 MB)
- `Assets/Prefabs/场景资源/贴图/建筑/ObjTX_Daiseido_B_01_no.tga`  (2.01 MB)
- `Assets/Prefabs/场景资源/贴图/物件/ObjTX_Daiseido_A_01_no.tga`  (2.01 MB)
- `Assets/Prefabs/场景资源/贴图/建筑/ObjTX_Daiseido_A_01_no.tga`  (2.01 MB)
- `Assets/Prefabs/场景资源/贴图/建筑/EnvBdgTX_Wilderness_A_Outdoor_Mansion_A_cl.tga`  (1.97 MB)
- `Assets/Prefabs/场景资源/模型/物件/EnvObjMD_Stage_C_Base_A.FBX`  (1.97 MB)
- `Assets/Prefabs/场景资源/贴图/建筑组件/EnvMdrTX_Atl_A_cl.tga`  (1.97 MB)
- ... 其余 4958 个，见 `unused_manifest.tsv`

## B. 第三方插件 Demo / 示例内容

共 873 个 / 125.2 MB

- `Assets/Plugins/Hovl Studio/Magic effects pack/Materials/VFXatlas.psd`  (9.06 MB)
- `Assets/Plugins/Hovl Studio/Magic effects pack/Textures/Concrete slab_Normal.png`  (7.94 MB)
- `Assets/Plugins/Hovl Studio/Magic effects pack/Textures/Concrete slab.png`  (6.76 MB)
- `Assets/Plugins/Hovl Studio/Magic effects pack/Textures/Concrete slab_Height.png`  (6.24 MB)
- `Assets/Plugins/Hovl Studio/Magic effects pack/Textures/Concrete slab_MetallicSmoothness.png`  (3.53 MB)
- `Assets/Plugins/JMO Assets/Cartoon FX FREE (old legacy effects).unitypackage`  (2.44 MB)
- `Assets/Plugins/Hovl Studio/Magic effects pack/Prefabs/AoE effects/Crystals crossfade.prefab`  (1.25 MB)
- `Assets/Plugins/JMO Assets 0/Cartoon FX (legacy)/CFX2 Prefabs/Explosions/CFX2_WWExplosion_C.prefab`  (1.25 MB)
- `Assets/Plugins/Hovl Studio/Magic effects pack/Prefabs/AoE effects/Laser AOE.prefab`  (1.24 MB)
- `Assets/Plugins/Hovl Studio/Magic effects pack/Prefabs/AoE effects/Red energy explosion.prefab`  (1.24 MB)
- `Assets/Plugins/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Texts/CFXR4 _POISONED_.prefab`  (1.01 MB)
- `Assets/Plugins/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Texts/CFXR2 _CURSED_.prefab`  (1.00 MB)
- `Assets/Plugins/JMO Assets 0/Cartoon FX (legacy)/Textures/CFX_T_Smoke_4Frames.tga`  (1.00 MB)
- `Assets/Plugins/Hovl Studio/Magic effects pack/Prefabs/AoE effects/Meteors AOE.prefab`  (0.90 MB)
- `Assets/Plugins/Hovl Studio/Magic effects pack/Prefabs/AoE effects/Snow AOE.prefab`  (0.90 MB)
- `Assets/Plugins/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Eerie/CFXR2 WW Enemy Explosion.prefab`  (0.80 MB)
- `Assets/Plugins/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Texts/CFXR4 _FROZEN_.prefab`  (0.79 MB)
- `Assets/Plugins/Hovl Studio/Magic effects pack/Prefabs/AoE effects/Plexus AoE.prefab`  (0.79 MB)
- `Assets/Plugins/Lana Studio/Hyper Casual FX/Prefabs/Confetti/Confetti_directional_multicolor.prefab`  (0.78 MB)
- `Assets/Plugins/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Texts/CFXR _SLASH_.prefab`  (0.78 MB)
- `Assets/Plugins/Lana Studio/Hyper Casual FX/Prefabs/Area/Area_star_ellow.prefab`  (0.78 MB)
- `Assets/Plugins/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Liquids/CFXR Water Splash (Smaller).prefab`  (0.77 MB)
- `Assets/Plugins/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Misc/CFXR2 Broken Heart.prefab`  (0.75 MB)
- `Assets/Plugins/JMO Assets 0/Cartoon FX (legacy)/CFX Prefabs/Misc/CFX_MagicPoof.prefab`  (0.73 MB)
- `Assets/Plugins/Hovl Studio/Magic effects pack/Textures/Crystal1_Base.png`  (0.72 MB)
- `Assets/Plugins/Hovl Studio/Magic effects pack/Prefabs/AoE effects/AoE slash green.prefab`  (0.68 MB)
- `Assets/Plugins/Hovl Studio/Magic effects pack/Prefabs/AoE effects/AoE slash blue.prefab`  (0.68 MB)
- `Assets/Plugins/Hovl Studio/Magic effects pack/Prefabs/Slash effects/Charge slash blue.prefab`  (0.68 MB)
- `Assets/Plugins/Hovl Studio/Magic effects pack/Prefabs/Slash effects/Charge slash red.prefab`  (0.68 MB)
- `Assets/Plugins/Hovl Studio/Magic effects pack/Prefabs/Slash effects/Charge slash purple.prefab`  (0.68 MB)
- `Assets/Plugins/Hovl Studio/Magic effects pack/Prefabs/Magic circles/Magic circle.prefab`  (0.67 MB)
- `Assets/Plugins/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Texts/CFXR _BOOM_.prefab`  (0.67 MB)
- `Assets/Plugins/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Texts/CFXR2 _WHAM_ 3.prefab`  (0.67 MB)
- `Assets/Plugins/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Texts/CFXR _BOING_.prefab`  (0.67 MB)
- `Assets/Plugins/Lana Studio/Hyper Casual FX/Prefabs/Area/Area_magic_multicolor.prefab`  (0.67 MB)
- `Assets/Plugins/Lana Studio/Hyper Casual FX/Prefabs/Area/Area_fire_red.prefab`  (0.67 MB)
- `Assets/Plugins/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Explosions/CFXR Explosion 1.prefab`  (0.67 MB)
- `Assets/Plugins/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Impacts/CFXR Hit A (Red).prefab`  (0.66 MB)
- `Assets/Plugins/Hovl Studio/Magic effects pack/Textures/Crystal1_Normal.png`  (0.64 MB)
- `Assets/Plugins/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Electric/CFXR3 Hit Electric C (Air).prefab`  (0.57 MB)
- ... 其余 833 个，见 `unused_manifest.tsv`

## C. Art/Characters 未使用精灵

共 84 个 / 12.0 MB

- `Assets/Arts/Characters/Enemy/enemy00067.png`  (0.43 MB)
- `Assets/Arts/Characters/Enemy/enemy00035.png`  (0.31 MB)
- `Assets/Arts/Characters/Enemy/enemy00014.png`  (0.30 MB)
- `Assets/Arts/Characters/NPC/Human04-battle.png`  (0.30 MB)
- `Assets/Arts/Characters/Enemy/enemy00023.png`  (0.26 MB)
- `Assets/Arts/Characters/Enemy/enemy00011.png`  (0.25 MB)
- `Assets/Arts/Characters/Enemy/enemy00024.png`  (0.24 MB)
- `Assets/Arts/Characters/Enemy/enemy00003.png`  (0.23 MB)
- `Assets/Arts/Characters/Enemy/enemy00054.png`  (0.23 MB)
- `Assets/Arts/Characters/Enemy/enemy00040.png`  (0.22 MB)
- `Assets/Arts/Characters/Enemy/enemy00037.png`  (0.22 MB)
- `Assets/Arts/Characters/Enemy/enemy00050.png`  (0.22 MB)
- `Assets/Arts/Characters/Enemy/enemy00038.png`  (0.21 MB)
- `Assets/Arts/Characters/Enemy/enemy00063.png`  (0.21 MB)
- `Assets/Arts/Characters/Enemy/enemy00001.png`  (0.21 MB)
- `Assets/Arts/Characters/Enemy/enemy00061.png`  (0.20 MB)
- `Assets/Arts/Characters/Enemy/enemy00048.png`  (0.19 MB)
- `Assets/Arts/Characters/NPC/Human03-battle.png`  (0.19 MB)
- `Assets/Arts/Characters/Enemy/enemy00010.png`  (0.19 MB)
- `Assets/Arts/Characters/Enemy/enemy00031.png`  (0.18 MB)
- `Assets/Arts/Characters/Enemy/enemy00047.png`  (0.18 MB)
- `Assets/Arts/Characters/Enemy/enemy00036.png`  (0.18 MB)
- `Assets/Arts/Characters/Enemy/enemy00074.png`  (0.18 MB)
- `Assets/Arts/Characters/NPC/Human02-battle.png`  (0.18 MB)
- `Assets/Arts/Characters/Enemy/enemy00059.png`  (0.17 MB)
- `Assets/Arts/Characters/Enemy/enemy00029.png`  (0.17 MB)
- `Assets/Arts/Characters/Enemy/enemy00016.png`  (0.17 MB)
- `Assets/Arts/Characters/Enemy/enemy00032.png`  (0.16 MB)
- `Assets/Arts/Characters/Enemy/enemy00017.png`  (0.16 MB)
- `Assets/Arts/Characters/Enemy/enemy00033.png`  (0.16 MB)
- `Assets/Arts/Characters/Enemy/enemy00052.png`  (0.15 MB)
- `Assets/Arts/Characters/Enemy/enemy00042.png`  (0.15 MB)
- `Assets/Arts/Characters/Enemy/enemy00034.png`  (0.15 MB)
- `Assets/Arts/Characters/Enemy/enemy00046.png`  (0.15 MB)
- `Assets/Arts/Characters/Enemy/enemy00009.png`  (0.15 MB)
- `Assets/Arts/Characters/Enemy/enemy00064.png`  (0.15 MB)
- `Assets/Arts/Characters/Enemy/enemy00058.png`  (0.15 MB)
- `Assets/Arts/Characters/Enemy/enemy00025.png`  (0.15 MB)
- `Assets/Arts/Characters/Enemy/enemy00055.png`  (0.15 MB)
- `Assets/Arts/Characters/Enemy/enemy00073.png`  (0.15 MB)
- ... 其余 44 个，见 `unused_manifest.tsv`

## D. Art/UI 未使用精灵

共 72 个 / 7.0 MB

- `Assets/Arts/UI/Overlay Panel/menu_map_bg.png`  (4.01 MB)
- `Assets/Arts/UI/01/battle_gameover_cover.png`  (2.42 MB)
- `Assets/Arts/UI/01/star.png`  (0.14 MB)
- `Assets/Arts/UI/01/battle_gameover_bg.png`  (0.08 MB)
- `Assets/Arts/UI/01/battle_font_gameover.png`  (0.05 MB)
- `Assets/Arts/UI/04/menu_character_bg.png`  (0.04 MB)
- `Assets/Arts/UI/02 Map Interaction/talking_bg.png`  (0.03 MB)
- `Assets/Arts/UI/02 Map Interaction/talking_astonish_bg.png`  (0.03 MB)
- `Assets/Arts/UI/03 Popup & Button/common_popup_s.png`  (0.03 MB)
- `Assets/Arts/UI/02 Map Interaction/map_choice_bg.png`  (0.01 MB)
- `Assets/Arts/UI/02 Map Interaction/talking_name_bg.png`  (0.01 MB)
- `Assets/Arts/UI/A1 Icon/icon_aid.png`  (0.00 MB)
- `Assets/Arts/UI/A1 Icon/icon_escape.png`  (0.00 MB)
- `Assets/Arts/UI/A1 Icon/icon_shield.png`  (0.00 MB)
- `Assets/Arts/UI/A1 Icon/icon_coin.png`  (0.00 MB)
- `Assets/Arts/UI/01/battle_font_attpoint.png`  (0.00 MB)
- `Assets/Arts/UI/02 Map Interaction/talking_name_thinking_bg.png`  (0.00 MB)
- `Assets/Arts/UI/A1 Icon/偷窃.png`  (0.00 MB)
- `Assets/Arts/UI/A1 Icon/招募.png`  (0.00 MB)
- `Assets/Arts/UI/02 Map Interaction/talking_thinking.png`  (0.00 MB)
- `Assets/Arts/UI/A1 Icon/援助.png`  (0.00 MB)
- `Assets/Arts/UI/A1 Icon/icon_bottle.png`  (0.00 MB)
- `Assets/Arts/UI/A1 Icon/购买.png`  (0.00 MB)
- `Assets/Arts/UI/A1 Icon/逃跑.png`  (0.00 MB)
- `Assets/Arts/UI/A1 Icon/防御.png`  (0.00 MB)
- `Assets/Arts/UI/A1 Icon/暴击.png`  (0.00 MB)
- `Assets/Arts/UI/A1 Icon/属性防御力.png`  (0.00 MB)
- `Assets/Arts/UI/A1 Icon/查看.png`  (0.00 MB)
- `Assets/Arts/UI/A1 Icon/推荐装备.png`  (0.00 MB)
- `Assets/Arts/UI/A1 Icon/攻击.png`  (0.00 MB)
- `Assets/Arts/UI/A1 Icon/盾.png`  (0.00 MB)
- `Assets/Arts/UI/A1 Icon/金币.png`  (0.00 MB)
- `Assets/Arts/UI/A1 Icon/sp果子.png`  (0.00 MB)
- `Assets/Arts/UI/A1 Icon/弱点.png`  (0.00 MB)
- `Assets/Arts/UI/01/battle_font_onekill.png`  (0.00 MB)
- `Assets/Arts/UI/A1 Icon/身体盔甲.png`  (0.00 MB)
- `Assets/Arts/UI/A1 Icon/弓.png`  (0.00 MB)
- `Assets/Arts/UI/A1 Icon/行动速度.png`  (0.00 MB)
- `Assets/Arts/UI/A1 Icon/打听.png`  (0.00 MB)
- `Assets/Arts/UI/01/battle_font_nohurt.png`  (0.00 MB)
- ... 其余 32 个，见 `unused_manifest.tsv`

## E. Art/Icons 未使用图标

共 47 个 / 1.0 MB

- `Assets/Arts/Icons/UI Pack/Wild Dog Milk.png`  (0.78 MB)
- `Assets/Arts/Icons/DamageType Icons/Storm.png`  (0.02 MB)
- `Assets/Arts/Icons/StatType Icons/MDef-1.png`  (0.02 MB)
- `Assets/Arts/Icons/StatType Icons/Speed.png`  (0.01 MB)
- `Assets/Arts/Icons/StatType Icons/MaxSP.png`  (0.01 MB)
- `Assets/Arts/Icons/StatType Icons/Evasion.png`  (0.01 MB)
- `Assets/Arts/Icons/StatType Icons/Accuracy.png`  (0.01 MB)
- `Assets/Arts/Icons/StatType Icons/MDef.png`  (0.01 MB)
- `Assets/Arts/Icons/StatType Icons/PDef-1.png`  (0.01 MB)
- `Assets/Arts/Icons/StatType Icons/PAtk.png`  (0.01 MB)
- `Assets/Arts/Icons/StatType Icons/PDef.png`  (0.01 MB)
- `Assets/Arts/Icons/StatType Icons/MaxSP-1.png`  (0.01 MB)
- `Assets/Arts/Icons/StatType Icons/MAtk.png`  (0.01 MB)
- `Assets/Arts/Icons/DamageType Icons/Thunder.png`  (0.01 MB)
- `Assets/Arts/Icons/StatType Icons/MaxHP.png`  (0.01 MB)
- `Assets/Arts/Icons/Item Category Icons/03.png`  (0.00 MB)
- `Assets/Arts/Icons/Interaction Icons/icon_weaponshop.png`  (0.00 MB)
- `Assets/Arts/Icons/Interaction Icons/icon_hotel.png`  (0.00 MB)
- `Assets/Arts/Icons/Item Category Icons/04.png`  (0.00 MB)
- `Assets/Arts/Icons/Item Category Icons/06.png`  (0.00 MB)
- `Assets/Arts/Icons/Interaction Icons/icon_itemshop.png`  (0.00 MB)
- `Assets/Arts/Icons/UI Pack/援助.png`  (0.00 MB)
- `Assets/Arts/Icons/Item Category Icons/10.png`  (0.00 MB)
- `Assets/Arts/Icons/Item Category Icons/05.png`  (0.00 MB)
- `Assets/Arts/Icons/UI Pack/购买.png`  (0.00 MB)
- `Assets/Arts/Icons/UI Pack/暴击.png`  (0.00 MB)
- `Assets/Arts/Icons/UI Pack/查看.png`  (0.00 MB)
- `Assets/Arts/Icons/UI Pack/推荐装备.png`  (0.00 MB)
- `Assets/Arts/Icons/Item Category Icons/07.png`  (0.00 MB)
- `Assets/Arts/Icons/Interaction Icons/icon_door.png`  (0.00 MB)
- `Assets/Arts/Icons/UI Pack/金币.png`  (0.00 MB)
- `Assets/Arts/Icons/Interaction Icons/icon_buy.png`  (0.00 MB)
- `Assets/Arts/Icons/UI Pack/sp果子.png`  (0.00 MB)
- `Assets/Arts/Icons/Interaction Icons/icon_save.png`  (0.00 MB)
- `Assets/Arts/Icons/UI Pack/弱点.png`  (0.00 MB)
- `Assets/Arts/Icons/UI Pack/物理防御力.png`  (0.00 MB)
- `Assets/Arts/Icons/Interaction Icons/icon_search.png`  (0.00 MB)
- `Assets/Arts/Icons/UI Pack/命中.png`  (0.00 MB)
- `Assets/Arts/Icons/Item Category Icons/09.png`  (0.00 MB)
- `Assets/Arts/Icons/UI Pack/属性攻击力.png`  (0.00 MB)
- ... 其余 7 个，见 `unused_manifest.tsv`

## F. Art/Mask 未使用遮罩

共 1 个 / 0.6 MB

- `Assets/Arts/Mask/Smoke8x8.png`  (0.58 MB)

## G. Art/Fonts 未使用字体

共 2 个 / 52.4 MB

- `Assets/Arts/Fonts/SourceHanSerifSC-ExtraLight SDF.asset`  (32.01 MB)
- `Assets/Arts/Fonts/SourceHanSerifSC-ExtraLight.otf`  (20.41 MB)

## H. Art/Audio 未使用音频

共 2 个 / 6.0 MB

- `Assets/Arts/Audio/BGM/Menu/Menu.mp3`  (5.44 MB)
- `Assets/Arts/Audio/SFX/Battle/Battle_Defeat/你干嘛哎哟.WAV`  (0.56 MB)

## J. 场景文件（.unity）

共 1 个 / 3.5 MB

- `Assets/Scenes/Town/Town01_Night/Town01_Night.unity`  (3.48 MB)

## K. Environment 未使用（模型/材质/贴图）

共 14 个 / 0.4 MB

- `Assets/Environment/Textures/Terrain/dimian_04_n.tga`  (0.19 MB)
- `Assets/Environment/Textures/Terrain/dimian_04_d.tga`  (0.19 MB)
- `Assets/Environment/Textures/Terrain/dimian_02.tga`  (0.01 MB)
- `Assets/Environment/Sky/M_SkyNight.mat`  (0.00 MB)
- `Assets/Environment/Materials/EnvBdgMD_City_B_Outdoor 2.mat`  (0.00 MB)
- `Assets/Environment/Materials/SM_light03_1 1.mat`  (0.00 MB)
- `Assets/Environment/Materials/M_light01 1.mat`  (0.00 MB)
- `Assets/Environment/Materials/M_light04 1.mat`  (0.00 MB)
- `Assets/Environment/Textures/Terrain/dimian_03_n.tga`  (0.00 MB)
- `Assets/Environment/Textures/Terrain/dimian_03.tga`  (0.00 MB)
- `Assets/Environment/Materials/EnvBdgMD_City_B_Outdoor 1.mat`  (0.00 MB)
- `Assets/Environment/Textures/Terrain/NewLayer 2.terrainlayer`  (0.00 MB)
- `Assets/Environment/Textures/Terrain/NewLayer.terrainlayer`  (0.00 MB)
- `Assets/Environment/Textures/Terrain/NewLayer 1.terrainlayer`  (0.00 MB)

## L. Martials/Terrains 未使用地形材质

共 22 个 / 20.1 MB

- `Assets/Martials/Terrains/Snow/dirt_nor_gl_1k.exr`  (2.33 MB)
- `Assets/Martials/Terrains/Grass/rocky_terrain_02_nor_gl_1k.exr`  (2.06 MB)
- `Assets/Martials/Terrains/Snow/snow_field_aerial_height_1k.png`  (1.86 MB)
- `Assets/Martials/Terrains/Grass/rocky_terrain_02_spec_1k.png`  (1.79 MB)
- `Assets/Martials/Terrains/Snow/snow_field_aerial_nor_gl_1k.exr`  (1.65 MB)
- `Assets/Martials/Terrains/Snow/dirt_disp_1k.png`  (1.64 MB)
- `Assets/Martials/Terrains/Snow/blue_plaster_wall_nor_gl_1k.exr`  (1.62 MB)
- `Assets/Martials/Terrains/Snow/blue_plaster_wall_disp_1k.png`  (1.54 MB)
- `Assets/Martials/Terrains/Grass/rocky_terrain_02_disp_1k.png`  (1.50 MB)
- `Assets/Martials/Terrains/Grass/rocky_terrain_02_diff_1k.jpg`  (0.80 MB)
- `Assets/Martials/Terrains/Snow/snow_field_aerial_col_1k.jpg`  (0.72 MB)
- `Assets/Martials/Terrains/Snow/dirt_diff_1k.jpg`  (0.70 MB)
- `Assets/Martials/Terrains/Snow/blue_plaster_wall_diff_1k.jpg`  (0.57 MB)
- `Assets/Martials/Terrains/Snow/snow_field_aerial_rough_1k.jpg`  (0.47 MB)
- `Assets/Martials/Terrains/Grass/rocky_terrain_02_rough_1k.exr`  (0.40 MB)
- `Assets/Martials/Terrains/Snow/blue_plaster_wall_rough_1k.exr`  (0.25 MB)
- `Assets/Martials/Terrains/Snow/dirt_rough_1k.exr`  (0.20 MB)
- `Assets/Martials/Wipe Shader.shadergraph`  (0.03 MB)
- `Assets/Martials/Material Shadow Field 1.mat`  (0.00 MB)
- `Assets/Martials/Terrains/Snow/Ice.terrainlayer`  (0.00 MB)
- `Assets/Martials/Terrains/Snow/dirt.terrainlayer`  (0.00 MB)
- `Assets/Martials/Terrains/Grass/Grass.terrainlayer`  (0.00 MB)

## M. TextMesh Pro 未使用资源

共 21 个 / 0.9 MB

- `Assets/TextMesh Pro/Documentation/TextMesh Pro User Guide 2016.pdf`  (0.66 MB)
- `Assets/TextMesh Pro/Sprites/EmojiOne.png`  (0.11 MB)
- `Assets/TextMesh Pro/Resources/Sprite Assets/EmojiOne.asset`  (0.01 MB)
- `Assets/TextMesh Pro/Shaders/TMP_SDF SSD.shader`  (0.01 MB)
- `Assets/TextMesh Pro/Shaders/TMP_SDF Overlay.shader`  (0.01 MB)
- `Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile Masking.shader`  (0.01 MB)
- `Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile Overlay.shader`  (0.01 MB)
- `Assets/TextMesh Pro/Shaders/TMPro_Mobile.cginc`  (0.01 MB)
- `Assets/TextMesh Pro/Resources/Style Sheets/Default Style Sheet.asset`  (0.00 MB)
- `Assets/TextMesh Pro/Shaders/TMP_SDF-Surface.shader`  (0.00 MB)
- `Assets/TextMesh Pro/Shaders/TMPro_Surface.cginc`  (0.00 MB)
- `Assets/TextMesh Pro/Shaders/TMP_Bitmap-Mobile.shader`  (0.00 MB)
- `Assets/TextMesh Pro/Shaders/TMP_SDF-Surface-Mobile.shader`  (0.00 MB)
- `Assets/TextMesh Pro/Shaders/TMP_Bitmap.shader`  (0.00 MB)
- `Assets/TextMesh Pro/Shaders/TMP_Bitmap-Custom-Atlas.shader`  (0.00 MB)
- `Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile SSD.shader`  (0.00 MB)
- `Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Drop Shadow.mat`  (0.00 MB)
- `Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Outline.mat`  (0.00 MB)
- `Assets/TextMesh Pro/Shaders/TMPro_Properties.cginc`  (0.00 MB)
- `Assets/TextMesh Pro/Shaders/TMPro.cginc`  (0.00 MB)
- `Assets/TextMesh Pro/Resources/TMP Settings.asset`  (0.00 MB)

## N. 其他 Scripts/Settings 等

共 57 个 / 0.2 MB

- `Assets/Settings/Input System/CharacteInputActions.cs`  (0.04 MB)
- `Assets/Scripts/Battle/Core/Models/BattleEntity.cs`  (0.02 MB)
- `Assets/Scripts/Battle/Core/Commands/AttackSkillExecutionEngine.cs`  (0.01 MB)
- `Assets/Scripts/Battle/Core/FSM/EnemyAIState.cs`  (0.01 MB)
- `Assets/Scripts/Game/Audio/AudioEventBridge.cs`  (0.01 MB)
- `Assets/Scripts/Game/Dialogue/DialogueManager.cs`  (0.01 MB)
- `Assets/Scripts/Battle/Core/Systems/BattleRoundScheduler.cs`  (0.01 MB)
- `Assets/Scripts/Battle/Event/BattleEvents.cs`  (0.01 MB)
- `Assets/Scripts/Game/Character/CharacterRuntimeData.cs`  (0.01 MB)
- `Assets/Scripts/Game/Audio/SFXPlayer.cs`  (0.01 MB)
- `Assets/Scripts/Battle/Core/FSM/TargetSelectionState.cs`  (0.00 MB)
- `Assets/Scripts/Game/Audio/MusicPlayer.cs`  (0.00 MB)
- `Assets/Scripts/Battle/Core/Systems/BattleOutcomeResolver.cs`  (0.00 MB)
- `Assets/Scripts/MFrameWork/Event/EventBus.cs`  (0.00 MB)
- `Assets/Scripts/Utility/Enums.cs`  (0.00 MB)
- `Assets/Scripts/Game/Skill/StealSkillLogic_SO.cs`  (0.00 MB)
- `Assets/Scripts/Game/Dialogue/DialogueRunner.cs`  (0.00 MB)
- `Assets/Scripts/Battle/Core/FSM/PlayerInputState.cs`  (0.00 MB)
- `Assets/Scripts/Battle/Core/FSM/BattleSetupState.cs`  (0.00 MB)
- `Assets/Scripts/Battle/Core/FSM/SelectNextEntityState.cs`  (0.00 MB)
- `Assets/Scripts/Game/Audio/Editor/AudioSystemSetupMenu.cs`  (0.00 MB)
- `Assets/Scripts/UI/Equipment/EquipmentService.cs`  (0.00 MB)
- `Assets/Scripts/Game/Flag/GameFlagManager.cs`  (0.00 MB)
- `Assets/Scripts/Battle/Core/Systems/BattleTargeting.cs`  (0.00 MB)
- `Assets/Scripts/Battle/Core/FSM/TurnEndState.cs`  (0.00 MB)
- `Assets/Scripts/Game/Audio/AudioSettingsPanelController.cs`  (0.00 MB)
- `Assets/Scripts/Game/Character/CharacterDefinition_SO.cs`  (0.00 MB)
- `Assets/Scripts/Battle/Core/Commands/EscapeCommandHandler.cs`  (0.00 MB)
- `Assets/Scripts/Battle/Core/Commands/AttackSkillCommandHandler.cs`  (0.00 MB)
- `Assets/Scripts/Battle/Core/Models/BuffInstance.cs`  (0.00 MB)
- `Assets/Scripts/Game/Audio/E_SoundId.cs`  (0.00 MB)
- `Assets/Scripts/UI/Field Panel/PanelController.cs`  (0.00 MB)
- `Assets/Scripts/Game/Scene Transition/SceneLoadRequest.cs`  (0.00 MB)
- `Assets/Scripts/Battle/Core/Models/BattleCommandRequest.cs`  (0.00 MB)
- `Assets/Scripts/Battle/Core/FSM/PerformActionState.cs`  (0.00 MB)
- `Assets/Scripts/Battle/Core/Commands/ItemCommandHandler.cs`  (0.00 MB)
- `Assets/Scripts/Game/Audio/AudioSettings.cs`  (0.00 MB)
- `Assets/Scripts/Battle/Core/Commands/BattleCommandHandlerBase.cs`  (0.00 MB)
- `Assets/Scripts/Game/Audio/UIButtonSfx.cs`  (0.00 MB)
- `Assets/Scripts/Game/Interaction/InteractionEvent.cs`  (0.00 MB)
- ... 其余 17 个，见 `unused_manifest.tsv`

## Z. 未归类

共 36 个 / 19.6 MB

- `Assets/Scenes/Town/Town01_Night/Lightmap-0_comp_light.exr`  (3.36 MB)
- `Assets/Scenes/Town/Town01_Night/Lightmap-1_comp_light.exr`  (3.20 MB)
- `Assets/Scenes/Town/Town01_Night/Lightmap-3_comp_light.exr`  (2.85 MB)
- `Assets/Scenes/Town/Town01_Night/Lightmap-2_comp_light.exr`  (2.76 MB)
- `Assets/Scenes/Town/Town01_Night/Lightmap-0_comp_dir.png`  (1.30 MB)
- `Assets/Scenes/Town/Town01_Night/Lightmap-1_comp_dir.png`  (1.17 MB)
- `Assets/Scenes/Town/Town01_Night/Lightmap-3_comp_dir.png`  (1.08 MB)
- `Assets/Scenes/Town/Town01_Night/Lightmap-2_comp_dir.png`  (0.86 MB)
- `Assets/Scenes/Town/Town01_Night/Lightmap-0_comp_shadowmask.png`  (0.39 MB)
- `Assets/Scenes/Town/Town01_Night/Lightmap-1_comp_shadowmask.png`  (0.34 MB)
- `Assets/Scenes/Town/Town01_Night/Lightmap-3_comp_shadowmask.png`  (0.33 MB)
- `Assets/Scenes/Town/Town01_Night/Lightmap-2_comp_shadowmask.png`  (0.32 MB)
- `Assets/Scenes/Town/Town01_Day/ReflectionProbe-0.exr`  (0.29 MB)
- `Assets/Scenes/Field/Field02/Field02/ReflectionProbe-0.exr`  (0.29 MB)
- `Assets/Scenes/Battle/Battle02/Battle02/ReflectionProbe-0.exr`  (0.29 MB)
- `Assets/Scenes/Battle/Battle01/Battle01/ReflectionProbe-0.exr`  (0.29 MB)
- `Assets/Scenes/Town/Town01_Night/ReflectionProbe-0.exr`  (0.29 MB)
- `Assets/Scenes/Town/Town01_Night/LightingData.asset`  (0.10 MB)
- `Assets/Scenes/Field/Field02/Field02/Lightmap-0_comp_light.exr`  (0.04 MB)
- `Assets/Scenes/Battle/Battle02/Battle02/Lightmap-0_comp_light.exr`  (0.04 MB)
- `Assets/Scenes/Battle/Battle01/Battle01/Lightmap-0_comp_light.exr`  (0.02 MB)
- `Assets/PostProcess/SGS_VolumeProfile 1.asset`  (0.01 MB)
- `Assets/Scenes/Field/Field02/Field02/Lightmap-0_comp_dir.png`  (0.00 MB)
- `Assets/Scenes/Battle/Battle02/Battle02/Lightmap-0_comp_dir.png`  (0.00 MB)
- `Assets/Scenes/Battle/Battle01/Battle01/Lightmap-0_comp_dir.png`  (0.00 MB)
- `Assets/Newly-battle-idle-hp-normal.anim`  (0.00 MB)
- `Assets/Animation/Template/Common/base-anim.anim`  (0.00 MB)
- `Assets/Scenes/Town/Effect/Materials/M_WaterfallFoam 1.mat`  (0.00 MB)
- `Assets/Scenes/Battle/Battle01/Battle01/Lightmap-0_comp_shadowmask.png`  (0.00 MB)
- `Assets/Scenes/Town/Effect/Materials/M_WaterfallFoam_Volumetric 1.mat`  (0.00 MB)
- `Assets/Animation/Template/Common/Common.controller`  (0.00 MB)
- `Assets/Game Data/Skill/Skill Data/Steal.asset`  (0.00 MB)
- `Assets/Newly-Enemy-Battle.overrideController`  (0.00 MB)
- `Assets/Game Data/Audio/BGM/Menu.asset`  (0.00 MB)
- `Assets/Game Data/Skill/Logic/Steal Skill Logic.asset`  (0.00 MB)
- `Assets/csc.rsp`  (0.00 MB)
