// PS1-era lit shader for the Labyrinth Crawler retro look.
// Recreates the console's three signature artifacts on modern URP:
//   1. Vertex snapping  - clip-space positions quantize to a virtual low-res
//      grid (_RetroSnapResolution global, set by RetroPresenter), so geometry
//      "wobbles" as the camera moves.
//   2. Affine texture mapping - UVs interpolate linearly in screen space
//      (no perspective correction), so textures swim on walls seen at an
//      angle. _AffineWarp dials 0 (clean) -> 1 (full PS1 swim); _AffineMaxWarp
//      clamps the per-pixel deviation so the maze's large unsubdivided
//      floor/wall quads can't blow out into nausea at grazing angles.
//   3. Per-vertex lighting - main light + per-object additional lights +
//      ambient SH evaluated at vertices only (Forward path; Forward+ would
//      drop the additional-light loop). No shadow receiving, no normal maps.
// Fog uses the standard Unity fog pipeline (RetroPresenter drives
// RenderSettings), so unconverted materials fade into the same murk.
Shader "Arcade/PS1/Lit"
{
    Properties
    {
        // MainTexture/MainColor let Material.mainTexture and Material.color
        // resolve (there is no _MainTex/_Color here; without these attributes
        // any script touching .color errors).
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _EmissionMap ("Emission Map", 2D) = "white" {}
        [HDR] _EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)
        _AffineWarp ("Affine Warp (0 = clean, 1 = full PS1)", Range(0, 1)) = 0.4
        _AffineMaxWarp ("Affine Max Warp (UV clamp)", Range(0, 1)) = 0.12
        _SnapStrength ("Vertex Snap Strength", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST;
        half4 _BaseColor;
        half4 _EmissionColor;
        half _AffineWarp;
        half _AffineMaxWarp;
        half _SnapStrength;
        CBUFFER_END

        // Set globally by RetroPresenter (render-target size * snap scale).
        // Falls back to a coarse grid when nothing set it (editor previews).
        float4 _RetroSnapResolution;

        float4 SnapToRetroGrid(float4 positionCS, half strength)
        {
            float2 grid = _RetroSnapResolution.xy;
            if (grid.x < 1.0)
            {
                grid = float2(320.0, 180.0);
            }
            if (positionCS.w > 0.0)
            {
                float2 ndc01 = positionCS.xy / positionCS.w * 0.5 + 0.5;
                float2 snapped = floor(ndc01 * grid + 0.5) / grid;
                ndc01 = lerp(ndc01, snapped, strength);
                positionCS.xy = (ndc01 * 2.0 - 1.0) * positionCS.w;
            }
            return positionCS;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_EmissionMap);
            SAMPLER(sampler_EmissionMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                // xy = uv * w for affine reconstruction, z = w.
                float3 uvw : TEXCOORD0;
                float2 uv : TEXCOORD1;
                // rgb = vertex lighting, a = fog factor.
                half4 lightFog : TEXCOORD2;
            };

            half3 PS1VertexLighting(float3 positionWS, float3 normalWS)
            {
                half3 lighting = SampleSH(normalWS);

                Light mainLight = GetMainLight();
                lighting += mainLight.color * saturate(dot(normalWS, mainLight.direction));

                uint lightCount = GetAdditionalLightsCount();
                for (uint li = 0u; li < lightCount; li++)
                {
                    Light light = GetAdditionalLight(li, positionWS);
                    lighting += light.color * light.distanceAttenuation
                        * saturate(dot(normalWS, light.direction));
                }
                return lighting;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(IN.positionOS.xyz);
                float4 positionCS = SnapToRetroGrid(vertexInput.positionCS, _SnapStrength);
                OUT.positionCS = positionCS;

                float2 uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.uv = uv;
                OUT.uvw = float3(uv * positionCS.w, positionCS.w);

                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.lightFog.rgb = PS1VertexLighting(vertexInput.positionWS, normalWS);
                OUT.lightFog.a = ComputeFogFactor(positionCS.z);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // IN.uv interpolates perspective-correct (the clean UV).
                // The uv*w / w reconstruction yields the screen-linear
                // (affine, swimming) UV. Their difference is the raw PS1
                // warp; clamp it so large grazing quads (maze floors/walls)
                // can't blow out, then scale by _AffineWarp for the dial.
                float2 uvAffine = IN.uvw.xy / max(IN.uvw.z, 1e-5);
                float2 warp = clamp(uvAffine - IN.uv, -_AffineMaxWarp, _AffineMaxWarp);
                float2 uv = IN.uv + warp * _AffineWarp;

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;
                half3 color = albedo.rgb * IN.lightFog.rgb;
                color += SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uv).rgb
                    * _EmissionColor.rgb;
                color = MixFog(color, IN.lightFog.a);

                return half4(color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);

            #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif

                float4 positionCS = TransformWorldToHClip(
                    ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif

                OUT.positionCS = positionCS;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                // Same snap as ForwardLit so any depth prepass matches.
                float4 positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionCS = SnapToRetroGrid(positionCS, _SnapStrength);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
