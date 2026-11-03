// Escudo divino que protegen las cruces alrededor del sacerdote (esfera).
// Borde luminoso tipo fresnel con un enrejado de cruces sobre la superficie. Con menos cruces
// se debilita: baja la fuerza, aparecen grietas y parpadea. Al recibir un golpe se enciende y
// lanza una onda desde el punto de impacto. Aditivo.
Shader "UdeM/Efectos/EscudoDivino"
{
    Properties
    {
        _Glifo ("Textura de cruz", 2D) = "white" {}
        [HDR] _Color ("Color", Color) = (2.2, 1.6, 0.6, 1)
        [HDR] _ColorImpacto ("Color del impacto", Color) = (6, 4.5, 1.8, 1)
        _Fuerza ("Fuerza (cruces vivas)", Range(0, 1)) = 1
        _Visible ("Visibilidad en reposo", Range(0, 1)) = 0.35
        _Teselas ("Cruces alrededor", Range(2, 16)) = 8
        _Impacto ("Impacto (1 al golpear, baja a 0)", Range(0, 1)) = 0
        _DireccionImpacto ("Direccion del impacto (objeto)", Vector) = (0, 0, 1, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend One One
        ZWrite Off
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

            TEXTURE2D(_Glifo);
            SAMPLER(sampler_Glifo);

            CBUFFER_START(UnityPerMaterial)
                float4 _Glifo_ST;
                half4 _Color;
                half4 _ColorImpacto;
                half _Fuerza;
                half _Visible;
                half _Teselas;
                half _Impacto;
                float4 _DireccionImpacto;
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
                float3 direccionOS : TEXCOORD2;
                half fogFactor : TEXCOORD3;
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
                output.direccionOS = normalize(input.positionOS.xyz);
                output.fogFactor = ComputeFogFactor(posiciones.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float t = _Time.y;
                float3 normal = normalize(input.normalWS);
                float3 vista = normalize(GetWorldSpaceViewDir(input.positionWS));
                float fresnel = pow(1.0 - saturate(dot(normal, vista)), 3.0);

                // Coordenadas esfericas para repartir las cruces sobre la superficie.
                float3 d = input.direccionOS;
                float u = atan2(d.z, d.x) / 6.2831853 + 0.5 + t * 0.02;
                float v = asin(clamp(d.y, -1.0, 1.0)) / 3.14159265 + 0.5;
                float2 uvCruces = float2(u * _Teselas, v * _Teselas * 0.5);
                // Sin mipmaps: frac() corta las coordenadas y con derivadas se verian costuras.
                float cruces = SAMPLE_TEXTURE2D_LOD(_Glifo, sampler_Glifo, frac(uvCruces), 0).a;

                // Debilidad: grietas que se abren y parpadeo cuando quedan pocas cruces.
                float debilidad = 1.0 - _Fuerza;
                float grietas = VetasSacerdote(float2(u * 9.0, v * 6.0), 0.03 + 0.05 * debilidad) * debilidad;
                float corte = RuidoSacerdote(float2(t * 7.0, 1.3));
                float parpadeo = lerp(1.0, smoothstep(0.2, 0.7, corte), debilidad * 0.85);

                // Respira despacio en reposo.
                float respiracion = 0.85 + 0.15 * sin(t * 2.2);

                float reposo = (fresnel * 0.9 + cruces * (0.25 + fresnel) * 0.45) * _Visible * respiracion;
                reposo = reposo * _Fuerza * parpadeo;
                // Las grietas brillan aunque el escudo este debil, para que se note que se rompe.
                reposo += grietas * 0.35 * _Visible;

                // Onda del golpe: anillo que se aleja del punto de impacto.
                float cercania = dot(d, normalize(_DireccionImpacto.xyz)) * 0.5 + 0.5;
                float frente = 1.0 - _Impacto;
                float onda = saturate(1.0 - abs((1.0 - cercania) - frente * 0.9) / 0.12) * _Impacto;
                float foco = pow(cercania, 6.0) * _Impacto;
                float impacto = onda * (0.6 + cruces) + foco * (0.4 + cruces * 1.2) + fresnel * _Impacto * 0.5;

                half3 color = _Color.rgb * reposo + _ColorImpacto.rgb * impacto * (0.4 + 0.6 * _Fuerza);

                color = MixFogColor(color, half3(0, 0, 0), input.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
