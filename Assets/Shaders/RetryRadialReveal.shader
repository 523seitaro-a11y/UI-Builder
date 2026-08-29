Shader "UIBuilder/RetryRadialReveal"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _BeforeColor ("Before Color", Color) = (1,1,1,1)
        _AfterColor ("After Color", Color) = (1,1,1,1)
        [Range(0,1)] _FadedAlpha ("Faded Alpha", Float) = 0.2
        [Range(0,1)] _Fill ("Clockwise Fill", Float) = 0
        [HideInInspector] _UvRect ("Sprite UV Rect", Vector) = (0,0,1,1)
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment RetryRadialFrag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #include "UnitySprites.cginc"

            float _FadedAlpha;
            float _Fill;
            float4 _UvRect;
            fixed4 _BeforeColor;
            fixed4 _AfterColor;

            fixed4 RetryRadialFrag(v2f input) : SV_Target
            {
                fixed4 color = SampleSpriteTexture(input.texcoord) * input.color;
                float2 localUv = (input.texcoord - _UvRect.xy) / max(_UvRect.zw, 0.0001);
                float2 direction = localUv - 0.5;

                // 3時方向を0として、下方向へ時計回りに角度が増えるようにします。
                float angle = atan2(-direction.y, direction.x);
                angle = angle < 0.0 ? angle + 6.28318530718 : angle;
                float radialPosition = angle / 6.28318530718;
                float revealed = step(radialPosition, _Fill);

                color *= lerp(_BeforeColor, _AfterColor, revealed);
                color.a *= lerp(_FadedAlpha, 1.0, revealed);
                color.rgb *= color.a;
                return color;
            }
            ENDCG
        }
    }
}
