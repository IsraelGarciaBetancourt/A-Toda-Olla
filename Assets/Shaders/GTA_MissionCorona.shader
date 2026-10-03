Shader "Custom/GTA_MissionCorona"
{
    Properties
    {
        _Color ("Marker Color", Color) = (1.0, 0.78, 0.18, 0.85)
        _EmissionPower ("Emission Multiplier", Float) = 2.5
        _VerticalFadePower ("Vertical Fade Power", Float) = 1.6
        _RimPower ("Rim Power", Float) = 1.8
        _PulseSpeed ("Pulse Speed", Float) = 2.8
        _PulseAmount ("Pulse Amount", Float) = 0.18
        _BaseRingBoost ("Base Ring Glow Boost", Float) = 2.2
    }
    SubShader
    {
        Tags 
        { 
            "RenderPipeline"="UniversalPipeline" 
            "Queue"="Transparent+50" 
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
                half _VerticalFadePower;
                half _RimPower;
                half _PulseSpeed;
                half _PulseAmount;
                half _BaseRingBoost;
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
                
                // Discard top and bottom caps of Unity cylinder (where normal.y is near 1 or -1)
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
                
                // Rim glow (glancing angle)
                float NdotV = abs(dot(normalize(input.normalWS), viewDirWS));
                float rim = pow(saturate(1.0 - NdotV), _RimPower);
                
                // Vertical fade: bright at the bottom (localY = 0), fading softly towards top (localY = 1)
                float vFade = pow(saturate(1.0 - input.localY), _VerticalFadePower);
                
                // Intense bright base ring right where it touches the ground
                float baseRing = smoothstep(0.12, 0.0, input.localY) * _BaseRingBoost;

                // Subtle sinusoidal pulsing
                float pulse = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseAmount;

                float intensity = (vFade * 0.75 + rim * 0.55 + baseRing) * pulse;
                
                half3 rgb = _Color.rgb * _EmissionPower * intensity;
                half a = saturate(_Color.a * intensity);

                return half4(rgb, a);
            }
            ENDHLSL
        }
    }
}
