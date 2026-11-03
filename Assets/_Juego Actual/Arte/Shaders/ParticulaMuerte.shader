// Particulas de los efectos de muerte (polvo, humo, ceniza, llamas y brasas).
// La mezcla se elige con _SrcBlend/_DstBlend: alfa para polvo y humo, aditiva para el fuego.
Shader "UdeM/Efectos/ParticulaMuerte"
{
    Properties
    {
        _BaseMap ("Textura", 2D) = "white" {}
        [HDR] _BaseColor ("Color", Color) = (1, 1, 1, 1)
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Mezcla origen", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Mezcla destino", Float) = 10
        [Toggle] _NieblaAditiva ("La niebla apaga en vez de tenir (para mezcla aditiva)", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Blend [_SrcBlend] [_DstBlend]
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
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _SrcBlend;
                half _DstBlend;
                half _NieblaAditiva;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float fogFactor : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs posicion = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = posicion.positionCS;
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.color = v.color;
                o.fogFactor = ComputeFogFactor(posicion.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor * i.color;
                half3 conNiebla = MixFog(color.rgb, i.fogFactor);
                half3 apagado = MixFogColor(color.rgb, half3(0, 0, 0), i.fogFactor);
                color.rgb = lerp(conNiebla, apagado, _NieblaAditiva);
                return color;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
