// Fullscreen present shader for the Labyrinth Crawler retro look.
// RetroPresenter draws the low-res gameplay RenderTexture to screen through
// a UGUI RawImage using this material. The point-filtered upscale gives the
// chunky pixels; this shader adds the PS1's 15-bit colour crush: quantize to
// _ColorLevels per channel with a 4x4 Bayer ordered dither, computed at
// render-target pixel scale so dither dots match the chunky pixels exactly.
// Quantization happens in gamma space so the bands are perceptually even.
Shader "Arcade/PS1/Present"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _ColorLevels ("Color Levels Per Channel", Range(4, 64)) = 32
        _DitherStrength ("Dither Strength", Range(0, 1)) = 0.8
        _ShadowTint ("Shadow Tint", Color) = (0.72, 0.82, 1, 1)
        _HighlightTint ("Highlight Tint", Color) = (1, 0.92, 0.78, 1)
        _GradeStrength ("Color Grade Strength", Range(0, 1)) = 0.35
        _VignetteStrength ("Vignette Strength", Range(0, 2)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Overlay"
            "IgnoreProjector" = "True"
            "RenderType" = "Opaque"
            "PreviewType" = "Plane"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _ColorLevels;
            float _DitherStrength;
            float3 _ShadowTint;
            float3 _HighlightTint;
            float _GradeStrength;
            float _VignetteStrength;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            static const float BAYER_4X4[16] =
            {
                 0.0,  8.0,  2.0, 10.0,
                12.0,  4.0, 14.0,  6.0,
                 3.0, 11.0,  1.0,  9.0,
                15.0,  7.0, 13.0,  5.0
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 c = tex2D(_MainTex, i.uv).rgb;
            #ifndef UNITY_COLORSPACE_GAMMA
                c = LinearToGammaSpace(c);
            #endif

                // Split-tone grade in gamma space: push shadows cool and
                // highlights warm for a moody dungeon read, then quantize the
                // graded result so dithering follows the final palette.
                float luma = dot(c, float3(0.299, 0.587, 0.114));
                float3 tint = lerp(_ShadowTint, _HighlightTint, smoothstep(0.0, 1.0, luma));
                c = lerp(c, c * tint, _GradeStrength);

                // Soft vignette to deepen the murk toward the frame edges.
                float2 d = i.uv - 0.5;
                c *= saturate(1.0 - dot(d, d) * _VignetteStrength);

                // Bayer threshold indexed by render-target pixel, not screen
                // pixel, so the dither pattern rides the chunky upscale.
                uint2 pix = (uint2)floor(i.uv * _MainTex_TexelSize.zw);
                uint idx = (pix.x & 3u) + ((pix.y & 3u) << 2u);
                float threshold = (BAYER_4X4[idx] + 0.5) / 16.0 - 0.5;

                float steps = max(_ColorLevels - 1.0, 1.0);
                c = saturate(c + threshold * (_DitherStrength / steps));
                c = floor(c * steps + 0.5) / steps;

            #ifndef UNITY_COLORSPACE_GAMMA
                c = GammaToLinearSpace(c);
            #endif
                return fixed4(c, 1.0);
            }
            ENDCG
        }
    }

    FallBack Off
}
