// Haz de energia divina: enlaces entre las cruces y el sacerdote, estela de las cruces al volar
// y columna de luz de la invocacion. Pensado para LineRenderer y TrailRenderer (uv.x a lo largo,
// uv.y a lo ancho). Aditivo: pulsos que viajan por el haz y nucleo brillante en el centro.
Shader "UdeM/Efectos/EnlaceDivino"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (3, 2.2, 0.9, 1)
        _Fuerza ("Fuerza", Range(0, 1)) = 1
        _Parpadeo ("Parpadeo (debilidad)", Range(0, 1)) = 0
        _Densidad ("Pulsos a lo largo", Range(0.5, 12)) = 4
        _Velocidad ("Velocidad de los pulsos", Range(-6, 6)) = 2.2
        _Desvanecer ("Desvanecer en los extremos", Range(0, 0.5)) = 0.08
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
            #include "EfectosSacerdote.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Fuerza;
                half _Parpadeo;
                half _Densidad;
                half _Velocidad;
                half _Desvanecer;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                half fogFactor : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs posiciones = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = posiciones.positionCS;
                output.uv = input.uv;
                output.color = input.color;
                output.fogFactor = ComputeFogFactor(posiciones.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float t = _Time.y;
                float a = input.uv.x;
                float ancho = abs(input.uv.y * 2.0 - 1.0);

                // Nucleo blanco y halo suave hacia los bordes.
                float nucleo = pow(saturate(1.0 - ancho), 2.5);
                float halo = pow(saturate(1.0 - ancho), 0.8) * 0.35;

                // Pulsos que viajan por el haz.
                float pulso = frac(a * _Densidad - t * _Velocidad);
                pulso = smoothstep(0.0, 0.15, pulso) * smoothstep(0.55, 0.15, pulso);

                // Ondulacion de energia a lo largo.
                float onda = RuidoSacerdote(float2(a * 9.0 - t * 3.0, t * 0.7));

                // Cuanto mas debil, mas se entrecorta.
                float corte = RuidoSacerdote(float2(t * 9.0, a * 3.0));
                float parpadeo = lerp(1.0, smoothstep(0.25, 0.6, corte), _Parpadeo);

                float extremos = saturate(a / max(_Desvanecer, 0.001)) * saturate((1.0 - a) / max(_Desvanecer, 0.001));

                float energia = (nucleo * (0.55 + 0.9 * pulso) + halo * (0.6 + 0.6 * onda)) * extremos;
                half3 color = _Color.rgb * input.color.rgb * energia * _Fuerza * parpadeo * input.color.a;

                color = MixFogColor(color, half3(0, 0, 0), input.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
