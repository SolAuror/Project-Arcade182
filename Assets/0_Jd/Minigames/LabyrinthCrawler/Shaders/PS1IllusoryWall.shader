// Illusory-wall variant of Arcade/PS1/Lit for the Labyrinth Crawler.
// Same three PS1 artifacts (vertex snap, clamped affine UV swim, per-vertex
// lighting) plus two secrets of its own:
//   1. Surface ripples - up to 8 expanding damped rings (script-driven via
//      MaterialPropertyBlock: _RipplePoints xyz = world hit, w = start time;
//      _RippleAmps = per-ring amplitude). Spell impacts and player touches
//      make the "stone" wobble like a desert mirage: pure hue-free
//      refraction (UV push + a faint luminance flutter), no colored glow -
//      the diegetic tell that it's fake.
//      Analytic rings instead of a sim RT (see Sol.RippleSim) - a wall only
//      ever needs a few transient rings, not a persistent height field.
//   2. Dither dissolve - _DissolveAmount 0 -> 1 eats the wall in chunky
//      world-space cells with a glowing edge, driven by IllusoryWall.cs when
//      the player walks through.
// The passive tell is in the material, not the code: the maze walls run
// _AffineWarp 1, this one defaults lower - the one wall that doesn't swim.
Shader "Arcade/PS1/IllusoryWall"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _EmissionMap ("Emission Map", 2D) = "white" {}
        [HDR] _EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)
        _AffineWarp ("Affine Warp (0 = clean, 1 = full PS1)", Range(0, 1)) = 0.35
        _AffineMaxWarp ("Affine Max Warp (UV clamp)", Range(0, 1)) = 0.8
        _SnapStrength ("Vertex Snap Strength", Range(0, 1)) = 0.85

        [Header(Ripples)]
        _RippleSpeed ("Ripple Speed (m per s)", Range(0.1, 20)) = 2.2
        _RippleWavelength ("Ripple Wavelength (m)", Range(0.05, 3)) = 0.9
        _RippleDuration ("Ripple Duration (s)", Range(0.1, 5)) = 1.6
        _RippleStrength ("Ripple UV Push", Range(0, 0.2)) = 0.035
        _RippleShimmer ("Ripple Light Shimmer", Range(0, 1)) = 0.18

        [Header(Dissolve)]
        _DissolveAmount ("Dissolve Amount", Range(0, 1)) = 0
        _DissolveCellSize ("Dissolve Cell Size (m)", Range(0.01, 2)) = 0.22
        _DissolveEdgeWidth ("Dissolve Edge Width", Range(0, 0.5)) = 0.08
        [HDR] _DissolveEdgeColor ("Dissolve Edge Glow", Color) = (1.6, 1.1, 2.8, 1)
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
        half4 _DissolveEdgeColor;
        half _AffineWarp;
        half _AffineMaxWarp;
        half _SnapStrength;
        half _RippleSpeed;
        half _RippleWavelength;
        half _RippleDuration;
        half _RippleStrength;
        half _RippleShimmer;
        half _DissolveAmount;
        half _DissolveCellSize;
        half _DissolveEdgeWidth;
        CBUFFER_END

        // Script-driven per-renderer state (MaterialPropertyBlock). xyz = world
        // impact point, w = start time (_Time.y clock). Amp 0 or ancient start
        // time = inert slot.
        float4 _RipplePoints[8];
        float _RippleAmps[8];

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

        // Chunky world-space cells so the dissolve eats the wall in PS1-sized
        // bites instead of a smooth fade.
        float DissolveNoise(float3 positionWS)
        {
            float3 cell = floor(positionWS / max(_DissolveCellSize, 0.001));
            return frac(sin(dot(cell, float3(12.9898, 78.233, 37.719))) * 43758.5453);
        }

        // Signed survival margin: negative = this cell is already gone.
        float DissolveMargin(float3 positionWS)
        {
            // 1.001 so amount 1 clips even the highest-noise cell.
            return DissolveNoise(positionWS) - _DissolveAmount * 1.001;
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
                float3 positionWS : TEXCOORD3;
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
                OUT.positionWS = vertexInput.positionWS;

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
                float dissolveMargin = DissolveMargin(IN.positionWS);
                clip(dissolveMargin);

                // Same clamped affine warp as Arcade/PS1/Lit (see its notes).
                float2 uvAffine = IN.uvw.xy / max(IN.uvw.z, 1e-5);
                float2 warp = clamp(uvAffine - IN.uv, -_AffineMaxWarp, _AffineMaxWarp);
                float2 uv = IN.uv + warp * _AffineWarp;

                // Ripples: expanding damped rings around recent impacts. The
                // ring displaces UVs radially; the radial direction lives in
                // world space, so map it into UV space with the screen-space
                // jacobian. All derivative ops stay outside the loop (ddx/ddy
                // inside divergent flow is undefined).
                float2 duvx = ddx(IN.uv);
                float2 duvy = ddy(IN.uv);
                float3 dpx = ddx(IN.positionWS);
                float3 dpy = ddy(IN.positionWS);
                float jacobianDet = duvx.x * duvy.y - duvx.y * duvy.x;

                float2 rippleUv = float2(0.0, 0.0);
                float rippleShimmer = 0.0;

                [loop]
                for (int ri = 0; ri < 8; ri++)
                {
                    float age = _Time.y - _RipplePoints[ri].w;
                    float amp = _RippleAmps[ri];
                    if (amp <= 0.001 || age < 0.0 || age > _RippleDuration)
                    {
                        continue;
                    }

                    float3 toPixel = IN.positionWS - _RipplePoints[ri].xyz;
                    float dist = max(length(toPixel), 1e-4);
                    float front = age * _RippleSpeed;

                    float lifeFade = 1.0 - age / _RippleDuration;
                    lifeFade *= lifeFade;

                    // Damped ring band ~2 wavelengths wide around the front.
                    float band = (dist - front) / max(_RippleWavelength, 1e-4);
                    float envelope = saturate(1.0 - abs(band) * 0.5);
                    float wave = sin(band * 6.28318530718) * envelope * lifeFade * amp;

                    float3 radialWS = toPixel / dist;
                    float distDx = dot(radialWS, dpx);
                    float distDy = dot(radialWS, dpy);
                    if (abs(jacobianDet) > 1e-8)
                    {
                        // Solve J^T g = grad(dist) for the radial dir in UV space.
                        float2 radialUV = float2(
                            duvy.y * distDx - duvx.y * distDy,
                            -duvy.x * distDx + duvx.x * distDy) / jacobianDet;
                        float radialLen = length(radialUV);
                        if (radialLen > 1e-5)
                        {
                            rippleUv += (radialUV / radialLen) * wave * _RippleStrength;
                        }
                    }

                    // Signed, so the bands read as light bending, not adding.
                    rippleShimmer += wave;
                }

                uv += rippleUv;

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;
                half3 color = albedo.rgb * IN.lightFog.rgb;
                color += SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uv).rgb
                    * _EmissionColor.rgb;

                // Mirage flutter: hue-free luminance wobble riding the rings,
                // like heat haze catching torchlight.
                color *= 1.0 + rippleShimmer * _RippleShimmer;

                // Glowing rim on cells about to dissolve.
                color += _DissolveEdgeColor.rgb
                    * step(dissolveMargin, _DissolveEdgeWidth)
                    * step(0.0001, _DissolveAmount);

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
                float3 positionWS : TEXCOORD0;
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
                OUT.positionWS = positionWS;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Dissolved cells stop casting shadows too.
                clip(DissolveMargin(IN.positionWS));
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
                float3 positionWS : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                // Same snap as ForwardLit so any depth prepass matches.
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float4 positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionCS = SnapToRetroGrid(positionCS, _SnapStrength);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                clip(DissolveMargin(IN.positionWS));
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
