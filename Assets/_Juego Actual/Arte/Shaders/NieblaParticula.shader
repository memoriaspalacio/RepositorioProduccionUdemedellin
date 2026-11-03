Shader "UdeM/Ambientacion/NieblaParticula"
{
    Properties
    {
        _BaseMap ("Textura", 2D) = "white" {}
        _BaseColor ("Color", Color) = (0.55, 0.64, 0.78, 1)
        _SuavizadoProfundidad ("Suavizado contra la geometria", Range(0.01, 10)) = 1.5
        _CercaInicio ("Desvanecer cerca de la camara (inicio)", Float) = 1.5
        _CercaFin ("Desvanecer cerca de la camara (fin)", Float) = 7
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

        Blend SrcAlpha OneMinusSrcAlpha
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

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                float _SuavizadoProfundidad;
                float _CercaInicio;
                float _CercaFin;
            CBUFFER_END

            float _AtmosferaSuave;

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
                float4 screenPos : TEXCOORD1;
                float profundidad : TEXCOORD2;
                float fogFactor : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs posicion = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = posicion.positionCS;
                o.screenPos = ComputeScreenPos(posicion.positionCS);
                o.profundidad = -posicion.positionVS.z;
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.color = v.color;
                o.fogFactor = ComputeFogFactor(posicion.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor * i.color;

                if (_AtmosferaSuave > 0.5)
                {
                    float2 uvPantalla = i.screenPos.xy / i.screenPos.w;
                    float escena = LinearEyeDepth(SampleSceneDepth(uvPantalla), _ZBufferParams);
                    color.a *= saturate((escena - i.profundidad) / _SuavizadoProfundidad);
                }

                color.a *= saturate((i.profundidad - _CercaInicio) / max(0.001, _CercaFin - _CercaInicio));
                color.rgb = MixFog(color.rgb, i.fogFactor);
                return color;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
