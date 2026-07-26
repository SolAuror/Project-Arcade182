#ifndef LABYRINTH_PS1_LIGHTING_INCLUDED
#define LABYRINTH_PS1_LIGHTING_INCLUDED

// Shared by the ordinary and illusory labyrinth surface shaders. The caller
// must include URP Lighting.hlsl before this file.
half4 _StormFlashColor;
float4 _StormFlashDirection;

half3 LabyrinthPS1VertexLighting(half3 normalWS)
{
    half3 lighting = SampleSH(normalWS);

    // Supplemental wrapped response keeps the deliberately dark PS1 surfaces
    // readable during a strike while preserving its world-space direction.
    half3 flashDirection = normalize(
        (half3)_StormFlashDirection.xyz + half3(0.0001h, 0.0001h, 0.0001h));
    half flashFacing = saturate(
        (dot(normalWS, flashDirection) + 0.12h) / 1.12h);
    lighting += _StormFlashColor.rgb * (0.08h + flashFacing * 0.92h);

    Light mainLight = GetMainLight();
    lighting += mainLight.color * saturate(dot(normalWS, mainLight.direction));
    return lighting;
}

half3 LabyrinthPS1SingleLocalLight(Light light, half3 normalWS)
{
    return light.color
        * light.distanceAttenuation
        * light.shadowAttenuation
        * saturate(dot(normalWS, light.direction));
}

half3 LabyrinthPS1LocalLighting(
    float3 positionWS,
    half3 normalWS,
    float2 normalizedScreenSpaceUV)
{
    half3 lighting = 0.0h;

#if defined(_ADDITIONAL_LIGHTS)
    InputData inputData = (InputData)0;
    inputData.positionWS = positionWS;
    inputData.normalWS = normalWS;
    inputData.normalizedScreenSpaceUV = normalizedScreenSpaceUV;
    uint lightCount = GetAdditionalLightsCount();

    #if USE_FORWARD_PLUS
    [loop] for (
        uint lightIndex = 0u;
        lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS);
        lightIndex++)
    {
        FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK
        Light light = GetAdditionalLight(
            lightIndex,
            positionWS,
            half4(1.0h, 1.0h, 1.0h, 1.0h));
        lighting += LabyrinthPS1SingleLocalLight(light, normalWS);
    }
    #endif

    LIGHT_LOOP_BEGIN(lightCount)
        Light light = GetAdditionalLight(
            lightIndex,
            positionWS,
            half4(1.0h, 1.0h, 1.0h, 1.0h));
        lighting += LabyrinthPS1SingleLocalLight(light, normalWS);
    LIGHT_LOOP_END
#endif

    return lighting;
}

#endif
