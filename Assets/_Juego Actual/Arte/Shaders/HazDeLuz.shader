Shader "UdeM/Ambientacion/HazDeLuz"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (0.55, 0.7, 1, 1)
        _Intensidad ("Intensidad", Range(0, 2)) = 0.12
        _CaidaLargo ("Caida a lo largo", Range(0.1, 8)) = 1.6
        _SuavidadBordes ("Suavidad de bordes", Range(0.1, 4)) = 1.2
        _EscalaPolvo ("Escala del polvo", Range(0.01, 4)) = 0.6
        _VelocidadPolvo ("Velocidad del polvo", Range(0, 2)) = 0.15
        _SuavizadoProfundidad ("Suavizado contra la geometria", Range(0.01, 10)) = 0.8
        _CercaInicio ("Desvanecer cerca de la camara (inicio)", Float) = 1
        _CercaFin ("Desvanecer cerca de la camara (fin)", Float) = 5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+1"
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Intensidad;
                float _CaidaLargo;
                float _SuavidadBordes;
                float _EscalaPolvo;
                float _VelocidadPolvo;
                float _SuavizadoProfundidad;
                float _CercaInicio;
                float _CercaFin;
            CBUFFER_END

            float _AtmosferaSuave;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float4 screenPos : TEXCOORD3;
                float profundidad : TEXCOORD4;
                float fogFactor : TEXCOORD5;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float hash(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            float ruido(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                float3 u = f * f * (3.0 - 2.0 * f);
                float a = lerp(lerp(hash(i), hash(i + float3(1, 0, 0)), u.x),
                               lerp(hash(i + float3(0, 1, 0)), hash(i + float3(1, 1, 0)), u.x), u.y);
                float b = lerp(lerp(hash(i + float3(0, 0, 1)), hash(i + float3(1, 0, 1)), u.x),
                               lerp(hash(i + float3(0, 1, 1)), hash(i + float3(1, 1, 1)), u.x), u.y);
                return lerp(a, b, u.z);
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs posicion = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = posicion.positionCS;
                o.positionWS = posicion.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.screenPos = ComputeScreenPos(posicion.positionCS);
                o.profundidad = -posicion.positionVS.z;
                o.uv = v.uv;
                o.fogFactor = ComputeFogFactor(posicion.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float largo = pow(saturate(1.0 - i.uv.y), _CaidaLargo) * smoothstep(0.0, 0.06, i.uv.y);
                float borde = pow(sin(saturate(i.uv.x) * PI), _SuavidadBordes);

                float3 vista = normalize(GetWorldSpaceViewDir(i.positionWS));
                float frente = pow(saturate(abs(dot(normalize(i.normalWS), vista))), 1.5);

                float3 p = i.positionWS * _EscalaPolvo + float3(0.3, -0.6, 0.2) * _Time.y * _VelocidadPolvo;
                float polvo = lerp(0.6, 1.25, ruido(p) * 0.65 + ruido(p * 2.7) * 0.35);

                float suave = 1.0;
                if (_AtmosferaSuave > 0.5)
                {
                    float2 uvPantalla = i.screenPos.xy / i.screenPos.w;
                    float escena = LinearEyeDepth(SampleSceneDepth(uvPantalla), _ZBufferParams);
                    suave = saturate((escena - i.profundidad) / _SuavizadoProfundidad);
                }

                float cerca = saturate((i.profundidad - _CercaInicio) / max(0.001, _CercaFin - _CercaInicio));

                float3 color = _Color.rgb * _Intensidad * largo * borde * frente * polvo * suave * cerca;
                color = MixFogColor(color, half3(0, 0, 0), i.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
