Shader "UdeM/Ambientacion/BrilloOjos"
{
    Properties
    {
        _BaseMap ("Textura del brillo", 2D) = "white" {}
        [HDR] _Color ("Color", Color) = (0.5, 1.3, 4, 1)
        _Pulso ("Pulso", Range(0, 1)) = 0.35
        _VelocidadPulso ("Velocidad del pulso", Range(0, 5)) = 1.1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+2"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _Color;
                half _Pulso;
                half _VelocidadPulso;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float fogFactor : TEXCOORD1;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs posicion = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = posicion.positionCS;
                o.color = v.color;
                o.uv = v.uv;
                o.fogFactor = ComputeFogFactor(posicion.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half brillo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a;
                half pulso = 1.0 - _Pulso + _Pulso * (0.5 + 0.5 * sin(_Time.y * _VelocidadPulso + i.color.a * 6.2832));
                half3 color = _Color.rgb * brillo * pulso * i.color.rgb;
                color = MixFogColor(color, half3(0, 0, 0), i.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
