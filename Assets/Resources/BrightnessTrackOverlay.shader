Shader "UIBuilder/BrightnessTrackOverlay"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _PlayerTex ("Player silhouette", 2D) = "white" {}
        _PlayerUVRect ("Player sprite UV rectangle", Vector) = (0,0,1,1)
        _PlayerMaskEnabled ("Player mask opacity", Float) = 0
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
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
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 playerUV : TEXCOORD1;
            };
            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 playerUV : TEXCOORD1;
                float4 local : TEXCOORD2;
            };
            sampler2D _MainTex, _PlayerTex;
            float4 _PlayerTex_TexelSize, _PlayerUVRect, _ClipRect;
            float _PlayerMaskEnabled;

            v2f vert(appdata v)
            {
                v2f o;
                o.local = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                o.playerUV = v.playerUV;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = i.playerUV;
                float inside = step(_PlayerUVRect.x, p.x) * step(p.x, _PlayerUVRect.z) *
                    step(_PlayerUVRect.y, p.y) * step(p.y, _PlayerUVRect.w);
                float2 playerUV = clamp(p, _PlayerUVRect.xy + _PlayerTex_TexelSize.xy * 0.5,
                    _PlayerUVRect.zw - _PlayerTex_TexelSize.xy * 0.5);
                fixed playerAlpha = tex2D(_PlayerTex, playerUV).a * inside * _PlayerMaskEnabled;
                fixed4 result = tex2D(_MainTex, i.uv) * i.color;
                result.a *= 1 - saturate(playerAlpha);
                #ifdef UNITY_UI_CLIP_RECT
                result.a *= UnityGet2DClipping(i.local.xy, _ClipRect);
                #endif
                return result;
            }
            ENDCG
        }
    }
}
