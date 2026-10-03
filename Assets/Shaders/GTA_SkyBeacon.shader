Shader "Custom/GTA_SkyBeacon"
{
    Properties
    {
        _Color ("Beam Color", Color) = (1.0, 0.84, 0.26, 0.72)
        _EmissionPower ("Emission Multiplier", Float) = 3.6
        _EdgeSoftness ("Edge Softness", Float) = 1.8
        _TopFadeHeight ("Top Fade Start", Range(0.5, 1.0)) = 0.88
        _BottomFadeHeight ("Bottom Fade End", Range(0.0, 0.3)) = 0.03
        _CoreBoost ("Core Glow Boost", Float) = 1.4
    }
    SubShader
    {
        Tags 
        { 
            "RenderPipeline"="UniversalPipeline" 
            "Queue"="Transparent+30" 
            "RenderType"="Transparent" 
            "IgnoreProjector"="True" 
        }
        LOD 100

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float2 uv         : TEXCOORD2;
                float localY      : TEXCOORD3;
                float capDiscard  : TEXCOORD4;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _EmissionPower;
                half _EdgeSoftness;
                half _TopFadeHeight;
                half _BottomFadeHeight;
                half _CoreBoost;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;

                // Unity Cylinder local Y goes from -1 to +1. Normalized localY goes from 0 to 1
                output.localY = saturate((input.positionOS.y + 1.0) * 0.5);

                // Discard top and bottom caps of cylinder
                output.capDiscard = abs(input.normalOS.y) > 0.5 ? 1.0 : 0.0;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                if (input.capDiscard > 0.5)
                {
                    discard;
                }

                float3 viewDirWS = normalize(GetCameraPositionWS() - input.positionWS);
                float NdotV = abs(dot(normalize(input.normalWS), viewDirWS));

                // Soft cylindrical edge falloff so it looks like a volumetric beam of light
                float edgeSoft = pow(saturate(1.0 - NdotV), _EdgeSoftness);

                // Vertical fade: smooth transition from near ground marker, and fade out smoothly into the sky
                float vFadeBottom = smoothstep(0.0, _BottomFadeHeight, input.localY);
                float vFadeTop = smoothstep(1.0, _TopFadeHeight, input.localY);
                float vFade = vFadeBottom * vFadeTop;

                // Center core + soft edge
                float beamFactor = saturate((0.5 + edgeSoft * 0.5) * _CoreBoost);
                float alpha = saturate(_Color.a * vFade * beamFactor);

                half3 rgb = _Color.rgb * _EmissionPower;

                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
}
