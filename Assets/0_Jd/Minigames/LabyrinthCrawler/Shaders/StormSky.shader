// Low-resolution storm sky for Labyrinth Crawler.
// Three cloud texture reads produce a warped two-layer field. The entity is
// composited as a translucent dark mass inside that field, so lightning
// reveals it without turning it into a hard-edged billboard.
Shader "Arcade/PS1/Storm Sky"
{
    Properties
    {
        [NoScaleOffset] _CloudTex ("Cloud Noise", 2D) = "gray" {}
        [NoScaleOffset] _EntityMask ("Entity Mask", 2D) = "black" {}
        [NoScaleOffset] _SkylineMask ("Skyline Mask", 2D) = "black" {}

        [HDR] _HorizonColor ("Horizon Color", Color) = (0.13, 0.18, 0.085, 1)
        [HDR] _ZenithColor ("Zenith Color", Color) = (0.02, 0.034, 0.024, 1)
        [HDR] _CloudDarkColor ("Cloud Dark", Color) = (0.027, 0.042, 0.027, 1)
        [HDR] _CloudLightColor ("Cloud Light", Color) = (0.25, 0.35, 0.14, 1)
        [HDR] _FlashColor ("Sky Flash Color", Color) = (0.68, 1.0, 0.58, 1)

        _CloudPlaneScale ("Cloud Plane Scale", Range(0.01, 1)) = 0.20
        _HorizonBias ("Horizon Projection Bias", Range(0.02, 0.5)) = 0.12
        _WarpScale ("Warp Scale", Range(0.1, 8)) = 1.5
        _WarpStrength ("Warp Strength", Range(0, 2)) = 0.48
        _CloudScaleA ("Cloud Scale A", Range(0.1, 8)) = 1
        _CloudScaleB ("Cloud Scale B", Range(0.1, 8)) = 2.5
        _CloudSpeedA ("Cloud Speed A", Vector) = (0.011, 0.004, 0, 0)
        _CloudSpeedB ("Cloud Speed B", Vector) = (-0.006, 0.009, 0, 0)
        _CloudContrast ("Cloud Contrast", Range(0.25, 4)) = 1.7
        _SwirlAmount ("Entity Vortex Swirl", Range(-8, 8)) = 1.5

        _EntityYaw ("Entity Yaw", Range(-180, 180)) = 22
        _EntityElevation ("Entity Elevation", Range(5, 80)) = 31
        _EntitySize ("Entity Angular Size", Range(0.05, 1.5)) = 0.62
        _EntityDarkness ("Entity Remaining Light", Range(0, 1)) = 0.22
        _EntityPresence ("Entity Presence", Range(0, 1)) = 0.18
        _EntityGlow ("Entity Backlight", Range(0, 4)) = 0

        [HDR] _SkylineColor ("Skyline Color", Color) = (0.01, 0.014, 0.01, 1)
        _SkylineHeight ("Skyline Angular Height", Range(0.02, 0.5)) = 0.13
        _SkylineBelowHorizon ("Skyline Below Horizon", Range(0, 0.5)) = 0.10
        _SkylineRepeat ("Skyline Repeat", Range(1, 8)) = 2
        _SkyFogBlend ("Sky Fog Blend", Range(0, 1)) = 0.16

        [HideInInspector] _StormFlash ("Storm Flash", Range(0, 1)) = 0
        [HideInInspector] _StormFlashDirection ("Storm Flash Direction", Vector) = (0, 1, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "RenderPipeline" = "UniversalPipeline"
            "PreviewType" = "Skybox"
        }

        Pass
        {
            Name "StormSky"

            Cull Off
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_CloudTex);
            SAMPLER(sampler_CloudTex);
            TEXTURE2D(_EntityMask);
            SAMPLER(sampler_EntityMask);
            TEXTURE2D(_SkylineMask);
            SAMPLER(sampler_SkylineMask);

            CBUFFER_START(UnityPerMaterial)
            half4 _HorizonColor;
            half4 _ZenithColor;
            half4 _CloudDarkColor;
            half4 _CloudLightColor;
            half4 _FlashColor;
            half4 _SkylineColor;
            float4 _CloudSpeedA;
            float4 _CloudSpeedB;
            float4 _StormFlashDirection;
            float _CloudPlaneScale;
            float _HorizonBias;
            float _WarpScale;
            float _WarpStrength;
            float _CloudScaleA;
            float _CloudScaleB;
            float _CloudContrast;
            float _SwirlAmount;
            float _EntityYaw;
            float _EntityElevation;
            float _EntitySize;
            float _EntityDarkness;
            float _EntityPresence;
            float _EntityGlow;
            float _SkylineHeight;
            float _SkylineBelowHorizon;
            float _SkylineRepeat;
            float _SkyFogBlend;
            float _StormFlash;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 viewDir : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                // DrawSkybox is a dedicated render path, not a normal URP
                // unlit object pass. Pin the cube to the far clip plane so it
                // fills only pixels untouched by scene geometry.
                output.positionCS.z =
                    UNITY_RAW_FAR_CLIP_VALUE * output.positionCS.w;
                output.viewDir = input.positionOS.xyz;
                return output;
            }

            float3 EntityDirection()
            {
                float yaw = radians(_EntityYaw);
                float elevation = radians(_EntityElevation);
                float horizontal = cos(elevation);
                return normalize(float3(
                    sin(yaw) * horizontal,
                    sin(elevation),
                    cos(yaw) * horizontal));
            }

            float2 SwirlCloudPlane(float2 planeUV, float3 entityDir)
            {
                float entityProjectionY = max(entityDir.y, _HorizonBias);
                float2 entityPlane = entityDir.xz / entityProjectionY * _CloudPlaneScale;
                float2 offset = planeUV - entityPlane;
                float radius = length(offset);
                float angle = atan2(offset.y, offset.x) + _SwirlAmount / (radius + 1.0);
                return entityPlane + radius * float2(cos(angle), sin(angle));
            }

            float SampleClouds(float2 planeUV)
            {
                float2 warpUV = planeUV * _WarpScale + _Time.y * float2(0.003, -0.002);
                float2 warp = SAMPLE_TEXTURE2D(_CloudTex, sampler_CloudTex, warpUV).rg * 2.0 - 1.0;
                float2 warpedUV = planeUV + warp * _WarpStrength;
                float layerA = SAMPLE_TEXTURE2D(
                    _CloudTex,
                    sampler_CloudTex,
                    warpedUV * _CloudScaleA + _Time.y * _CloudSpeedA.xy).r;
                float layerB = SAMPLE_TEXTURE2D(
                    _CloudTex,
                    sampler_CloudTex,
                    warpedUV * _CloudScaleB + _Time.y * _CloudSpeedB.xy).r;
                float cloud = layerA * 0.68 + layerB * 0.32;
                return saturate((cloud - 0.5) * _CloudContrast + 0.5);
            }

            float SampleEntity(float3 viewDir, float3 entityDir, out float radial)
            {
                float3 tangentRight = normalize(cross(float3(0.0, 1.0, 0.0), entityDir));
                float3 tangentUp = normalize(cross(entityDir, tangentRight));
                float facing = dot(viewDir, entityDir);
                float2 projected = float2(
                    dot(viewDir, tangentRight),
                    dot(viewDir, tangentUp)) / max(facing, 1e-4);
                float2 uv = projected / max(_EntitySize, 1e-3) + 0.5;
                radial = length(uv - 0.5) * 2.0;
                float inside = step(0.0, facing)
                    * step(0.0, uv.x) * step(uv.x, 1.0)
                    * step(0.0, uv.y) * step(uv.y, 1.0);
                return SAMPLE_TEXTURE2D(_EntityMask, sampler_EntityMask, uv).r * inside;
            }

            half3 ApplySkyline(half3 sky, float3 viewDir)
            {
                const float inverseTau = 0.15915494309;
                float heading = frac(atan2(viewDir.x, viewDir.z) * inverseTau + 0.5);
                float skylineV = (viewDir.y + _SkylineBelowHorizon)
                    / max(_SkylineHeight + _SkylineBelowHorizon, 1e-4);
                float band = step(0.0, skylineV) * step(skylineV, 1.0);
                float mask = SAMPLE_TEXTURE2D(
                    _SkylineMask,
                    sampler_SkylineMask,
                    float2(frac(heading * _SkylineRepeat), saturate(skylineV))).r * band;
                return lerp(sky, _SkylineColor.rgb, mask);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 viewDir = normalize(input.viewDir);
                float3 entityDir = EntityDirection();

                float2 planeUV = viewDir.xz / max(viewDir.y, _HorizonBias)
                    * _CloudPlaneScale;
                planeUV = SwirlCloudPlane(planeUV, entityDir);
                float clouds = SampleClouds(planeUV);

                half3 gradient = lerp(
                    _HorizonColor.rgb,
                    _ZenithColor.rgb,
                    saturate(viewDir.y));
                half3 cloudColor = lerp(_CloudDarkColor.rgb, _CloudLightColor.rgb, clouds);
                half3 color = gradient * 0.45 + cloudColor;

                // Lift the cloud field first, then composite the entity into
                // that illuminated mass. Adding flash after the darkening
                // washed the silhouette out exactly when it should resolve.
                float3 flashDir = normalize(
                    _StormFlashDirection.xyz + float3(1e-4, 1e-4, 1e-4));
                float directionalFlash = lerp(
                    0.3,
                    1.0,
                    pow(saturate(dot(viewDir, flashDir)), 4.0));
                color += _FlashColor.rgb * (_StormFlash * directionalFlash);

                float entityRadial;
                float entityMask = SampleEntity(viewDir, entityDir, entityRadial);
                float entityHalo = saturate(1.0 - abs(entityRadial - 0.9) * 2.5);
                entityHalo *= (1.0 - entityMask) * _EntityPresence * _EntityGlow;
                color += _FlashColor.rgb * entityHalo * 0.45;

                float entityAmount = entityMask * _EntityPresence;
                color *= lerp(1.0h, (half)_EntityDarkness, (half)entityAmount);

                color = ApplySkyline(color, viewDir);
                color = lerp(color, unity_FogColor.rgb, _SkyFogBlend);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
