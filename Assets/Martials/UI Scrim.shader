// ============================================================================
//  UI Scrim —— UGUI(Image / RawImage) 用的「半透明深色遮罩」着色器
//
//  来历：原来是 Assets/Martials/UniversalBlurUI.mat，指向
//        com.unify.unified-universal-blur 包的 TintedBlurUI 着色器。
//        那个包硬依赖 URP；2026-10-16 切回 Built-in RP 时把包一起删了，
//        材质随之丢了着色器 → 在工程里显示洋红。
//
//  为什么不直接用 Standard：
//        Canvas 默认只往顶点流里塞 TexCoord0，而 Standard 需要 Normal 通道，
//        挂到 UGUI 上 Unity 会报「Canvas 缺少 Normal 着色器通道」，
//        而且 Standard 是受光着色器，在 Overlay Canvas 里表现不可控。
//        所以照 Assets/Martials/Wipe Shader.shader 的路子，
//        手写一个 UI/Default 风格的**无光照**着色器（同样以 Fallback "UI/Default" 兜底）。
//
//  语义：rgb = _Color.rgb
//        a   = _Color.a × 顶点色.a × _MainTex.a
//        即一块纯色半透明遮罩。只吃贴图的 alpha、不吃图案，
//        Image 没挂 sprite 时就是一块均匀的暗色，正好当面板背景的压暗层。
//
//  使用：材质 Assets/Martials/UI Scrim.mat（_Color = 0.05, 0.05, 0.07, 0.7），
//        由 Persistent.unity 的 ShopAction Panel 背景层引用。
//        要调深浅直接改材质 _Color.a；改成暖色/冷色改 _Color.rgb。
//
//  注：属性名与 UI/Default 对齐（_Stencil* / _ColorMask / _UseUIAlphaClip /
//      _ClipRect / _UIMaskSoftnessX,Y），所以 Mask、RectMask2D 裁剪都正常。
// ============================================================================
Shader "UI/Scrim"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (0.05, 0.05, 0.07, 0.7)

        // ---- UGUI 标准属性（与 UI/Default 一致，Stencil / RectMask 才正常）----
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
            "Queue"             = "Transparent"
            "IgnoreProjector"   = "True"
            "RenderType"        = "Transparent"
            "PreviewType"       = "Plane"
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
            Name "SCRIM"

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
            fixed4 _Color;
            fixed4 _TextureSampleAdd;   // 位图字体图集用；UI/Default 里同名，保持对齐
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
                // 只取 alpha：遮罩要的是一整块色，不要 sprite 的图案
                fixed4 tex = tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd;

                fixed4 color;
                color.rgb = IN.color.rgb;
                color.a   = IN.color.a * tex.a;

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
