// Fuego procedural, sin texturas, en dos modos:
// - Suelo: brasas y llamas rastreras sobre un Quad tumbado. El ruido va en coordenadas de mundo, asi que
//   varias celdas juntas forman un solo incendio continuo, y deja el suelo chamuscado debajo.
// - Llama: lenguas de fuego verticales sobre un Quad que siempre mira a la camara (gira solo en Y).
// Las dos se encienden desde el centro (_Inicio) y se apagan desvaneciendose (_Fin); CeldaFuego pone esos
// tiempos. Sin ellos el fuego sale ya encendido. Colores en HDR para que el bloom los haga brillar.
Shader "UdeM/Efectos/Fuego"
{
    Properties
    {
        [KeywordEnum(Suelo, Llama)] _Modo ("Modo", Float) = 0

        [Header(Color)]
        [HDR] _ColorBrasa ("Brasa", Color) = (0.55, 0.06, 0.02, 1)
        [HDR] _ColorFuego ("Fuego", Color) = (2.4, 0.75, 0.12, 1)
        [HDR] _ColorNucleo ("Nucleo", Color) = (3.6, 2.7, 1.3, 1)
        _Intensidad ("Intensidad", Range(0, 4)) = 1.2

        [Header(Movimiento)]
        _Escala ("Escala del ruido", Range(0.1, 8)) = 1.4
        _Velocidad ("Velocidad", Range(0, 4)) = 1.1
        _Distorsion ("Distorsion", Range(0, 1)) = 0.4
        _Parpadeo ("Parpadeo", Range(0, 1)) = 0.25

        [Header(Suelo)]
        _Chamuscado ("Chamuscado del suelo", Range(0, 1)) = 0.5
        _Chispas ("Chispas", Range(0, 1)) = 0.5
        _Borde ("Suavidad del borde del fuego", Range(0.02, 0.6)) = 0.15
        _Celda ("Celda dentro del Quad (chamuscado)", Range(0.3, 1)) = 0.833

        [Header(Llama)]
        _Altura ("Altura de las lenguas", Range(0.2, 2)) = 1
        _Ancho ("Ancho de la base", Range(0.2, 1)) = 0.7
        _Opacidad ("Opacidad de la llama", Range(0, 1)) = 0.75

        [Header(Encendido y apagado)]
        _DuracionEncendido ("Segundos en encenderse", Range(0.01, 2)) = 0.35
        _DuracionApagado ("Segundos en apagarse", Range(0.01, 2)) = 0.3
        [HideInInspector] _Inicio ("Inicio", Float) = -1000
        [HideInInspector] _Fin ("Fin", Float) = -1
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

        // Premultiplicado: el color suma luz y el alfa oscurece lo de debajo (el chamuscado)
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
            #pragma shader_feature_local _MODO_SUELO _MODO_LLAMA

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ColorBrasa;
                half4 _ColorFuego;
                half4 _ColorNucleo;
                float _Intensidad;
                float _Escala;
                float _Velocidad;
                float _Distorsion;
                float _Parpadeo;
                float _Chamuscado;
                float _Chispas;
                float _Borde;
                float _Celda;
                float _Altura;
                float _Ancho;
                float _Opacidad;
                float _DuracionEncendido;
                float _DuracionApagado;
                float _Inicio;
                float _Fin;
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
                float3 positionWS : TEXCOORD1;
                float semilla : TEXCOORD2;
                float fog : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float2 Hash22(float2 p)
            {
                float n = Hash21(p);
                return float2(n, Hash21(p + n + 17.17));
            }

            float Ruido(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // Cuatro octavas, giradas para que no se note la rejilla del ruido
            float Fbm(float2 p)
            {
                const float2x2 giro = float2x2(0.8, -0.6, 0.6, 0.8);
                float suma = 0.0;
                float amplitud = 0.5;
                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    suma += Ruido(p) * amplitud;
                    p = mul(giro, p) * 2.03 + 11.7;
                    amplitud *= 0.5;
                }
                return suma;
            }

            half3 Rampa(float calor)
            {
                half3 color = lerp(_ColorBrasa.rgb, _ColorFuego.rgb, smoothstep(0.12, 0.6, calor));
                return lerp(color, _ColorNucleo.rgb, smoothstep(0.62, 0.98, calor));
            }

            // _Time.y es el tiempo desde que cargo la escena: CeldaFuego usa Time.timeSinceLevelLoad

            // 0 a 1 mientras se enciende: da la forma (la celda crece, las lenguas suben)
            float Encendido()
            {
                return saturate((_Time.y - _Inicio) / _DuracionEncendido);
            }

            // 1 a 0 mientras se apaga: desvanece todo, chamuscado incluido
            float Apagado()
            {
                return _Fin > 0.0 ? 1.0 - saturate((_Time.y - _Fin) / _DuracionApagado) : 1.0;
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 centro = TransformObjectToWorld(float3(0, 0, 0));

            #if defined(_MODO_LLAMA)
                // Billboard cilindrico: el Quad gira en Y hacia la camara y conserva la escala del objeto
                float escalaX = length(float3(UNITY_MATRIX_M._m00, UNITY_MATRIX_M._m10, UNITY_MATRIX_M._m20));
                float escalaY = length(float3(UNITY_MATRIX_M._m01, UNITY_MATRIX_M._m11, UNITY_MATRIX_M._m21));
                float3 derecha = float3(UNITY_MATRIX_V._m00, 0.0, UNITY_MATRIX_V._m02);
                derecha = dot(derecha, derecha) > 1e-5 ? normalize(derecha) : float3(1, 0, 0);
                float3 positionWS = centro + derecha * input.positionOS.x * escalaX + float3(0, 1, 0) * input.positionOS.y * escalaY;
            #else
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
            #endif

                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                output.semilla = Hash21(floor(centro.xz * 7.3) + 0.5);
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 FragSuelo(Varyings input, float t, float vida)
            {
                // Forma de la celda: entre circulo y cuadrado, crece desde el centro al encenderse
                float2 c = input.uv * 2.0 - 1.0;
                float forma = lerp(length(c), max(abs(c.x), abs(c.y)), 0.65);
                float2 p = input.positionWS.xz * _Escala;
                forma += (Ruido(p * 2.7 + t * 0.5) - 0.5) * 0.22;
                float alcance = lerp(0.05, 1.0, smoothstep(0.0, 1.0, vida));
                float mascara = 1.0 - smoothstep(alcance - _Borde, alcance, forma);

                // Calor: ruido deformado por otro ruido que avanza, para que el fuego "repte"
                float2 deformacion = float2(Fbm(p * 0.7 + t * 0.15), Fbm(p * 0.7 + 5.2 - t * 0.12));
                float calor = Fbm(p + deformacion * _Distorsion * 3.0 + float2(0.0, -t * _Velocidad * 0.6));
                float lenguas = Fbm(p * 2.3 - deformacion * 1.5 + float2(t * 0.3, -t * _Velocidad * 1.4));
                float intensidad = saturate(calor * 1.9 - 0.5) * (0.35 + 0.65 * lenguas);
                intensidad *= 1.0 - _Parpadeo * 0.5 * (0.5 + 0.5 * sin(t * 13.0 + input.semilla * 40.0));

                half3 color = Rampa(intensidad) * intensidad;

                // Chispas: puntos que se encienden y se apagan, unos pocos por celda del ruido
                float2 celda = floor(p * 3.0);
                float2 punto = Hash22(celda) * 0.7 + 0.15;
                float azar = Hash21(celda + 3.1);
                float distancia = length(frac(p * 3.0) - punto);
                float chispa = smoothstep(0.1, 0.0, distancia) * step(1.0 - _Chispas * 0.4, azar);
                chispa *= saturate(sin(t * (4.0 + azar * 6.0) + azar * 40.0));
                color += _ColorNucleo.rgb * chispa;

                // El Quad es algo mayor que la celda para que el fuego de celdas vecinas se funda sin costuras;
                // el chamuscado se queda en su celda, o donde dos se solapan se oscureceria el doble
                float enCelda = 1.0 - smoothstep(_Celda - 0.05, _Celda + 0.03, max(abs(c.x), abs(c.y)));
                float alfaFuego = saturate(intensidad * 1.4 + chispa) * mascara;
                float alfa = saturate(max(alfaFuego, _Chamuscado * 0.85 * mascara * enCelda));
                return half4(color * _Intensidad * mascara, alfa);
            }

            half4 FragLlama(Varyings input, float t, float vida)
            {
                // Las lenguas suben al encenderse: la llama crece de la base hacia arriba
                float y = input.uv.y / max(0.05, smoothstep(0.0, 1.0, vida));
                if (y > 1.0)
                    return 0;

                float x = input.uv.x * 2.0 - 1.0;
                float s = input.semilla * 19.0;
                float subida = t * _Velocidad * 2.2;

                float vaiven = Fbm(float2(x * 1.4 + s, y * 2.0 - subida));
                float xd = x + (vaiven - 0.5) * _Distorsion * 1.8 * y;

                float ancho = lerp(_Ancho, 0.03, pow(saturate(y), 0.6));
                float cuerpo = 1.0 - smoothstep(ancho * 0.45, ancho, abs(xd));

                // Detalle que sube y lenguas finas que se separan del cuerpo y se cortan al subir
                float detalle = Fbm(float2(xd * 3.2 + s * 0.5, y * 3.0 - subida * 1.35));
                float lenguas = smoothstep(0.35, 0.75, Fbm(float2(xd * 5.0 + s, y * 1.6 - subida * 1.8)));
                float densidad = cuerpo * saturate(detalle * 2.2 - y * 1.6 / _Altura + 0.05) * (0.45 + 0.55 * lenguas);
                densidad *= smoothstep(0.0, 0.07, input.uv.y);
                densidad *= 1.0 - _Parpadeo * 0.4 * (0.5 + 0.5 * sin(t * 17.0 + s));

                float calor = saturate(densidad * 1.3 + (1.0 - y) * 0.25 * cuerpo);
                half3 color = Rampa(calor) * densidad;
                float alfa = saturate(densidad * 1.2) * _Opacidad;
                return half4(color * _Intensidad, alfa);
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float t = _Time.y;
                float vida = Encendido();
                float apagado = Apagado();
                if (vida <= 0.0 || apagado <= 0.0)
                    return 0;

            #if defined(_MODO_LLAMA)
                half4 color = FragLlama(input, t, vida);
            #else
                half4 color = FragSuelo(input, t, vida);
            #endif

                color *= apagado;

                // Con niebla, lo que se ve detras tiende al color de la niebla en la misma proporcion
                color.rgb = MixFogColor(color.rgb, unity_FogColor.rgb * color.a, input.fog);
                return color;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
