// Brillo que se suma encima del material de las cruces del sacerdote: borde luminoso tipo
// fresnel y un resplandor interior que late. La intensidad la controla el script segun lo que
// haga la cruz (reposo, cargando un ataque, invocando al sacerdote). Aditivo.
Shader "UdeM/Efectos/BrilloDivino"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (3, 2.2, 0.8, 1)
        _Intensidad ("Intensidad", Range(0, 4)) = 0.4
        _Borde ("Potencia del borde", Range(0.5, 8)) = 2.5
        _Interior ("Brillo interior", Range(0, 1)) = 0.2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent-10"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend One One
        ZWrite Off
        ZTest LEqual
        Offset -1, -1
        Cull Back

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
            #include "EfectosSacerdote.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensidad;
                half _Borde;
                half _Interior;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs posiciones = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = posiciones.positionCS;
                output.positionWS = posiciones.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fogFactor = ComputeFogFactor(posiciones.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                if (_Intensidad <= 0.001)
                    return half4(0, 0, 0, 1);

                float3 normal = normalize(input.normalWS);
                float3 vista = normalize(GetWorldSpaceViewDir(input.positionWS));
                float fresnel = pow(1.0 - saturate(dot(normal, vista)), _Borde);

                // Destellos que recorren la cruz de abajo hacia arriba.
                float destello = RuidoSacerdote(float2(input.positionWS.y * 3.0 - _Time.y * 2.5, input.positionWS.x * 2.0));

                half3 color = _Color.rgb * (fresnel + _Interior * (0.6 + 0.6 * destello)) * _Intensidad;

                color = MixFogColor(color, half3(0, 0, 0), input.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
