Shader "UIBuilder/BlockOutlineOverlay"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        _OuterUV ("Sprite UV rectangle", Vector) = (0,0,1,1)
        _ShapeBorder ("Sliced border in shape", Vector) = (0,0,0,0)
        _TextureBorder ("Sliced border in texture", Vector) = (0,0,0,0)
        _OutlineStep ("Outline width", Vector) = (0,0,0,0)
        _SpriteFlip ("Sprite flip", Vector) = (0,0,0,0)
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

            struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; float4 local : TEXCOORD1; };
            sampler2D _MainTex;
            float4 _MainTex_TexelSize, _OuterUV, _ShapeBorder, _TextureBorder, _SpriteFlip, _ClipRect;
            float2 _OutlineStep;

            v2f vert(appdata v)
            {
                v2f o;
                o.local = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            float SliceUV(float p, float low, float high, float uvLow, float uvHigh)
            {
                if (low > 0 && p < low) return p * uvLow / low;
                if (high > 0 && p > 1 - high) return 1 - (1 - p) * uvHigh / high;
                return uvLow + (p - low) * (1 - uvLow - uvHigh) / max(0.00001, 1 - low - high);
            }

            fixed Alpha(float2 p)
            {
                float inside = step(0, p.x) * step(p.x, 1) * step(0, p.y) * step(p.y, 1);
                p = lerp(p, 1 - p, _SpriteFlip.xy);
                p = float2(SliceUV(p.x, _ShapeBorder.x, _ShapeBorder.z, _TextureBorder.x, _TextureBorder.z),
                    SliceUV(p.y, _ShapeBorder.y, _ShapeBorder.w, _TextureBorder.y, _TextureBorder.w));
                float2 uv = lerp(_OuterUV.xy, _OuterUV.zw, saturate(p));
                uv = clamp(uv, _OuterUV.xy + _MainTex_TexelSize.xy * 0.5,
                    _OuterUV.zw - _MainTex_TexelSize.xy * 0.5);
                return tex2D(_MainTex, uv).a * inside;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 d = _OutlineStep;
                fixed outer = max(max(Alpha(i.uv + float2(d.x, 0)), Alpha(i.uv - float2(d.x, 0))),
                    max(Alpha(i.uv + float2(0, d.y)), Alpha(i.uv - float2(0, d.y))));
                d *= 0.70710678;
                outer = max(outer, max(max(Alpha(i.uv + d), Alpha(i.uv - d)),
                    max(Alpha(i.uv + float2(d.x, -d.y)), Alpha(i.uv + float2(-d.x, d.y)))));
                // Subtract the original silhouette: the block itself stays behind the darkness.
                fixed4 result = fixed4(i.color.rgb, i.color.a * saturate(outer - Alpha(i.uv)));
                #ifdef UNITY_UI_CLIP_RECT
                result.a *= UnityGet2DClipping(i.local.xy, _ClipRect);
                #endif
                return result;
            }
            ENDCG
        }
    }
}
