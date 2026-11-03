// Capa extra sobre el cuerpo del sacerdote mientras baja al infierno o sale de el.
// Se dibuja encima del material normal: quema y oscurece la parte cercana al suelo, con un
// filo de fuego justo donde el cuerpo cruza el piso y vetas de brasa que suben por la ropa.
// Mezcla premultiplicada: rgb = brillo del fuego, alfa = cuanto se oscurece (calcina) el cuerpo.
Shader "UdeM/Efectos/SacerdoteInfierno"
{
    Properties
    {
        _Intensidad ("Intensidad del efecto", Range(0, 1)) = 0
        _AlturaSuelo ("Altura del suelo (mundo)", Float) = 0
        _AlturaBrasa ("Altura que alcanzan las brasas", Range(0.1, 4)) = 1.4
        _AnchoFilo ("Ancho del filo de fuego", Range(0.02, 1)) = 0.28
        [HDR] _ColorFuego ("Fuego del filo", Color) = (6, 1.6, 0.25, 1)
        [HDR] _ColorBrasa ("Vetas de brasa", Color) = (3, 0.35, 0.05, 1)
        _Calcinar ("Cuanto oscurece el cuerpo", Range(0, 1)) = 0.85
        _EscalaRuido ("Escala del ruido", Range(0.5, 12)) = 4
        _Velocidad ("Velocidad de las llamas", Range(0, 4)) = 1.6
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent-20"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend One OneMinusSrcAlpha
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
                half _Intensidad;
                float _AlturaSuelo;
                half _AlturaBrasa;
                half _AnchoFilo;
                half4 _ColorFuego;
                half4 _ColorBrasa;
                half _Calcinar;
                half _EscalaRuido;
                half _Velocidad;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
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
                output.positionWS = posiciones.positionWS;
                output.fogFactor = ComputeFogFactor(posiciones.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                if (_Intensidad <= 0.001)
                    return half4(0, 0, 0, 0);

                float altura = input.positionWS.y - _AlturaSuelo;
                float t = _Time.y * _Velocidad;

                // Coordenadas que suben: las llamas lamen el cuerpo hacia arriba.
                float2 uvLlama = float2(input.positionWS.x + input.positionWS.z, input.positionWS.y) * _EscalaRuido;
                float llama = FbmSacerdote(uvLlama + float2(0, -t));

                // Filo de fuego donde el cuerpo atraviesa el suelo, con lenguas irregulares.
                float anchoFilo = _AnchoFilo * (0.55 + llama * 0.9);
                float filo = saturate(1.0 - altura / max(anchoFilo, 0.001));
                filo = filo * filo;

                // Vetas de brasa que se apagan con la altura.
                float alcance = saturate(1.0 - altura / _AlturaBrasa);
                float vetas = VetasSacerdote(uvLlama * 0.8 + float2(t * 0.15, -t * 0.35), 0.07) * alcance * alcance;

                // Cuerpo calcinado cerca del suelo.
                float calcinado = saturate(1.0 - altura / (_AlturaBrasa * 1.25)) * _Calcinar;

                // Parpadeo general para que el fuego respire.
                float parpadeo = 0.8 + 0.2 * RuidoSacerdote(float2(t * 3.1, 7.3));

                half3 brillo = (_ColorFuego.rgb * filo + _ColorBrasa.rgb * vetas) * parpadeo;
                half oscuro = saturate(max(calcinado, filo * 0.6));

                brillo *= _Intensidad;
                oscuro *= _Intensidad;

                brillo = MixFogColor(brillo, half3(0, 0, 0), input.fogFactor);
                return half4(brillo, oscuro);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
