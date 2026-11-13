Shader "FullScreen/UnderwaterDistort"
{
    Properties
    {
        _Aberration ("Chromatic Aberration", Range(0, 0.05)) = 0.01
        _Intensity ("Intensity", Range(0, 1)) = 1
        _Frequency ("Frequency", Float) = 20
        _Speed ("Speed", Float) = 1.5
        _Amplitude ("Amplitude", Range(0, 0.02)) = 0.005
        _Tint ("Tint", Color) = (0.5, 0.8, 0.9, 1)
    }


    HLSLINCLUDE
    #pragma vertex Vert
    #pragma target 4.5
    #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/RenderPass/CustomPass/CustomPassCommon.hlsl"

    float _Intensity;
    float _Frequency;
    float _Speed;
    float _Amplitude;
    float4 _Tint;
    float _Aberration;

    float4 FullScreenPass(Varyings varyings) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(varyings);

        float depth = LoadCameraDepth(varyings.positionCS.xy);
        PositionInputs posInput = GetPositionInput(varyings.positionCS.xy, _ScreenSize.zw, depth, UNITY_MATRIX_I_VP, UNITY_MATRIX_V);

        float2 ndc = posInput.positionNDC.xy;
        float2 uv = ndc * _RTHandleScale.xy;

        float t = _Time.y * _Speed;
        float2 offset;
        offset.x = sin(uv.y * _Frequency + t) * _Amplitude;
        offset.y = cos(uv.x * _Frequency * 0.8 + t * 1.3) * _Amplitude;

        float2 ca = (ndc - 0.5) * _Aberration * _Intensity;
        float2 maxUV = _RTHandleScale.xy - _ScreenSize.zw;

        float2 baseUV = uv + offset * _Intensity;
        float2 uvR = clamp(baseUV + ca * _RTHandleScale.xy, 0.0, maxUV);
        float2 uvG = clamp(baseUV, 0.0, maxUV);
        float2 uvB = clamp(baseUV - ca * _RTHandleScale.xy, 0.0, maxUV);

        float3 color;
        color.r = CustomPassSampleCameraColor(uvR, 0).r;
        color.g = CustomPassSampleCameraColor(uvG, 0).g;
        color.b = CustomPassSampleCameraColor(uvB, 0).b;

        color = lerp(color, color * _Tint.rgb, _Intensity);
        return float4(color, 1);
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" }
        Pass
        {
            Name "Underwater Distort"
            ZWrite Off ZTest Always Blend Off Cull Off
            HLSLPROGRAM
            #pragma fragment FullScreenPass
            ENDHLSL
        }
    }
    Fallback Off
}