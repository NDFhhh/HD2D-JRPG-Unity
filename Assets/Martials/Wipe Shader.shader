// ============================================================================
//  Wipe Shader —— UGUI(RawImage / Image) 用的「擦拭转场」着色器
//
//  来历：原来是 Assets/Martials/Wipe Shader.shadergraph，用的是团结引擎私有的
//        UniversalCanvasSubTarget(UI/Canvas 子目标)。标准 Unity 的
//        URP 14.0.12 / ShaderGraph 14.0.12 里**没有**这个子目标，
//        于是导入报 "Expected active subtarget not found"，
//        生成的 Shader 所有 SubShader 被移除 → 材质在场景里显示洋红。
//
//  本文件用等价的手写 HLSL 复刻原图的逻辑（与原图逐一对应）：
//        Mask(Texture2D) ──R──┐
//                             ├─ Step ── One Minus ──> Alpha
//        _Progress ──Edge ────┘
//        _OverlayColor ───────────────────────────> Base Color
//  即：  alpha = 1 - step(_Progress, tex2D(_Mask, uv).r)
//        rgb   = _OverlayColor.rgb
//  语义：_Progress 从 0 → 1 时，遮罩红通道较小的地方先显形，整体是一次擦拭式淡入/淡出。
//
//  使用：材质 Assets/Martials/Wipe Shader.mat 已指向本 Shader；
//        由 FadeController.cs 通过 material.SetFloat("_Progress", 0..1) 驱动。
//
//  注：属性名与原来完全一致（_Mask / _Progress / _OverlayColor / _Stencil* /
//      _ColorMask / _ClipRect / _UIMaskSoftnessX,Y），因此材质里已有的取值全部沿用。
//      若擦拭方向上下相反，把 _Mask 的 Tiling.y 设为 -1、Offset.y 设为 1 即可。
// ============================================================================
Shader "UI/Wipe Shader"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        // ---- 原 ShaderGraph 的三个属性 ----
        _Mask ("Mask", 2D) = "white" {}
        _Progress ("Progress", Range(0, 1)) = 0
        _OverlayColor ("OverlayColor", Color) = (0,0,0,0)

        // ---- UGUI 标准属性（保持与 UI/Default 一致，掩盖色/遮罩才正常）----
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0

        _ClipRect ("Clip Rect", Vector) = (-32767, -32767, 32767, 32767)
        _UIMaskSoftnessX ("Mask Softness X", Float) = 0
        _UIMaskSoftnessY ("Mask Softness Y", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType"      = "Transparent"
            "PreviewType"     = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref       [_Stencil]
            Comp      [_StencilComp]
            Pass      [_StencilOp]
            ReadMask  [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "WIPE"

            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                half4  mask          : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            sampler2D _Mask;
            fixed4 _Color;
            fixed4 _OverlayColor;
            float  _Progress;
            float4 _ClipRect;
            float  _UIMaskSoftnessX;
            float  _UIMaskSoftnessY;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float4 vPosition = UnityObjectToClipPos(v.vertex);
                OUT.worldPosition = v.vertex;
                OUT.vertex   = vPosition;
                OUT.texcoord = v.texcoord;
                OUT.color    = v.color * _Color;

                float2 pixelSize = vPosition.w;
                pixelSize /= abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));

                float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
                OUT.mask = half4(v.vertex.xy * 2 - clampedRect.xy - clampedRect.zw,
                                 0.25 / (0.25 * half2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize.xy)));
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // 与原 ShaderGraph 一致：取遮罩的 R 通道
                float maskValue = tex2D(_Mask, IN.texcoord).r;

                // Step(Edge = _Progress, In = maskValue)  →  maskValue >= _Progress 时为 1
                float wipe = step(_Progress, maskValue);

                // One Minus → 1 - wipe
                fixed4 color;
                color.rgb = _OverlayColor.rgb;
                color.a   = (1.0 - wipe) * IN.color.a;

                #ifdef UNITY_UI_CLIP_RECT
                half2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(IN.mask.xy)) * IN.mask.zw);
                color.a *= m.x * m.y;
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }

    Fallback "UI/Default"
}
