Shader "Prehistoric/ARShadowMatte"
{
    Properties
    {
        _ShadowColor ("Shadow Color", Color) = (0.25, 0.25, 0.25, 1.0)
        _MaxRadius ("Max Shadow Radius", Float) = 85.0
        _FadeDistance ("Fade Distance", Float) = 12.0
    }
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent-50" 
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend DstColor Zero
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _ShadowColor;
                float _MaxRadius;
                float _FadeDistance;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Soft edge distance falloff to ensure zero hard square borders on real camera view
                float distFromCenter = length(input.positionWS.xz);
                float fade = saturate((_MaxRadius - distFromCenter) / max(0.001, _FadeDistance));

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half shadow = mainLight.shadowAttenuation;
                
                // When fully lit or outside fade: shadowFactor = 1.0 -> col = (1,1,1) -> 100% transparent via DstColor Zero blend
                // When in shadow: shadow < 1.0 -> col < 1.0 -> multiplies real camera feed, casting soft realistic shadows
                half shadowFactor = lerp(1.0, shadow, fade);
                half3 col = lerp(_ShadowColor.rgb, half3(1.0, 1.0, 1.0), shadowFactor);
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
