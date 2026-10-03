Shader "Custom/GTA_GroundRing"
{
    Properties
    {
        _Color ("Ring Color", Color) = (1.0, 0.78, 0.18, 0.9)
        _EmissionPower ("Emission Multiplier", Float) = 2.5
        _PulseSpeed ("Pulse Speed", Float) = 2.8
        _PulseAmount ("Pulse Amount", Float) = 0.15
        _RingWidth ("Ring Width", Range(0.01, 0.3)) = 0.08
    }
    SubShader
    {
        Tags 
        { 
            "RenderPipeline"="UniversalPipeline" 
            "Queue"="Transparent+45" 
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
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _EmissionPower;
                half _PulseSpeed;
                half _PulseAmount;
                half _RingWidth;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(posWS);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Remap uv (0..1) to centered circular coordinates (-1..1)
                float2 uvCentered = (input.uv - 0.5) * 2.0;
                float dist = length(uvCentered);

                if (dist > 1.0)
                {
                    discard;
                }

                // Crisp and glowing outer ring boundary
                float ringInner = 1.0 - _RingWidth;
                float ring = smoothstep(1.0, 0.96, dist) * smoothstep(ringInner - 0.05, ringInner + 0.03, dist) * 2.5;

                // Subtle semi-transparent ambient fill inside the circle
                float innerGlow = smoothstep(1.0, 0.0, dist) * 0.15;

                float pulse = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseAmount;
                float intensity = (ring + innerGlow) * pulse;

                half3 rgb = _Color.rgb * _EmissionPower * intensity;
                half a = saturate(_Color.a * intensity);

                return half4(rgb, a);
            }
            ENDHLSL
        }
    }
}
