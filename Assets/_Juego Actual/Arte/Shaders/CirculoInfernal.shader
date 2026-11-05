// Circulo en el suelo donde el sacerdote se hunde al infierno o sale de el.
// Abismo oscuro y arremolinado en el centro, anillo de fuego, grietas incandescentes hacia
// afuera y, cuando lo invoca una cruz, un anillo de cruces girando alrededor.
// Se dibuja en un quad plano sobre el piso (uv de 0 a 1). Mezcla premultiplicada:
// rgb = fuego y runas, alfa = cuanto se oscurece el suelo (el abismo).
Shader "UdeM/Efectos/CirculoInfernal"
{
    Properties
    {
        _Glifo ("Textura de cruz", 2D) = "white" {}
        _Apertura ("Apertura", Range(0, 1)) = 1
        _Intensidad ("Intensidad", Range(0, 2)) = 1
        [HDR] _ColorFuego ("Fuego del anillo", Color) = (6, 1.4, 0.2, 1)
        [HDR] _ColorGrieta ("Grietas", Color) = (2.5, 0.3, 0.03, 1)
        _ColorAbismo ("Abismo", Color) = (0.05, 0, 0, 1)
        [HDR] _ColorRuna ("Cruces de la invocacion", Color) = (4, 2.2, 0.6, 1)
        _Runas ("Cruces de la invocacion", Range(0, 1)) = 0
        _NumeroRunas ("Cantidad de cruces", Range(3, 16)) = 8
        _Giro ("Velocidad de giro", Range(-3, 3)) = 0.6
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent-30"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Blend One OneMinusSrcAlpha
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

            TEXTURE2D(_Glifo);
            SAMPLER(sampler_Glifo);

            CBUFFER_START(UnityPerMaterial)
                float4 _Glifo_ST;
                half _Apertura;
                half _Intensidad;
                half4 _ColorFuego;
                half4 _ColorGrieta;
                half4 _ColorAbismo;
                half4 _ColorRuna;
                half _Runas;
                half _NumeroRunas;
                half _Giro;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
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
                output.fogFactor = ComputeFogFactor(posiciones.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 p = input.uv * 2.0 - 1.0;
                float r = length(p);
                float angulo = atan2(p.y, p.x);
                float t = _Time.y;

                float radio = max(_Apertura, 0.001) * 0.62;

                // Abismo: remolino oscuro que gira hacia adentro.
                float2 remolino = float2(angulo * 1.6 + r * 5.0 - t * 1.4, r * 3.0 - t * 0.6);
                float humo = FbmSacerdote(remolino);
                float abismo = saturate(1.0 - r / radio);
                abismo = smoothstep(0.0, 0.35, abismo);

                // Anillo de fuego con lenguas que se mueven alrededor del borde.
                float lenguas = FbmSacerdote(float2(angulo * 3.0 + t * 0.8, t * 1.7));
                float bordeFuego = radio + (lenguas - 0.5) * 0.12 * _Apertura;
                float anillo = saturate(1.0 - abs(r - bordeFuego) / (0.05 + 0.06 * lenguas));
                anillo = anillo * anillo;

                // Brillo rojo que se filtra desde el fondo del abismo.
                float fondo = abismo * smoothstep(0.35, 0.9, humo) * 0.6;

                // Grietas incandescentes que se abren hacia afuera del circulo.
                float alcanceGrietas = radio + 0.38 * _Apertura;
                float zonaGrietas = saturate((r - radio * 0.9) / 0.05) * saturate((alcanceGrietas - r) / 0.25);
                float grietas = VetasSacerdote(float2(angulo * 2.6, r * 1.3 - t * 0.12), 0.05) * zonaGrietas;
                grietas *= 0.65 + 0.35 * RuidoSacerdote(float2(t * 4.0, angulo * 2.0));

                // Anillo de cruces de la invocacion, girando un poco por fuera del fuego.
                float runas = 0;
                if (_Runas > 0.001)
                {
                    float sector = 6.2831853 / _NumeroRunas;
                    float anguloGiro = angulo + t * _Giro;
                    float local = (frac(anguloGiro / sector + 0.5) - 0.5) * sector;
                    float radioRunas = radio + 0.2;
                    float tamano = 0.16;
                    float2 celda = float2(local * radioRunas, r - radioRunas) / tamano;
                    if (abs(celda.x) < 0.5 && abs(celda.y) < 0.5)
                    {
                        float2 uvGlifo = celda + 0.5;
                        // Sin mipmaps: dentro de un if no se pueden usar derivadas de textura.
                        runas = SAMPLE_TEXTURE2D_LOD(_Glifo, sampler_Glifo, uvGlifo, 0).a;
                    }
                    // Circulo fino que une las cruces.
                    runas = max(runas, saturate(1.0 - abs(r - radioRunas - 0.11) / 0.008) * 0.6);
                    runas *= _Runas;
                }

                half3 brillo = _ColorFuego.rgb * anillo
                             + _ColorFuego.rgb * fondo * 0.35
                             + _ColorGrieta.rgb * grietas
                             + _ColorRuna.rgb * runas;

                // El abismo oscurece el suelo; el fuego no lo tapa.
                half oscuro = saturate(abismo * (0.85 + 0.15 * humo));
                brillo += _ColorAbismo.rgb * oscuro;

                // Desvanece todo fuera del circulo para que el quad no se note.
                float limite = saturate((1.0 - r) / 0.08);
                brillo *= _Intensidad * limite;
                oscuro *= saturate(_Intensidad) * limite;

                brillo = MixFogColor(brillo, half3(0, 0, 0), input.fogFactor);
                return half4(brillo, oscuro);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
