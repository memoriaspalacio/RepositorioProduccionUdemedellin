Shader "UdeM/Ambientacion/Lienzo"
{
    Properties
    {
        _BaseMap ("Imagen", 2D) = "white" {}
        _Recorte ("Recorte (escala xy, desplazamiento zw)", Vector) = (1, 1, 0, 0)
        [Toggle] _Redondo ("Redondo", Float) = 0

        [Header(Pintura)]
        _Barniz ("Barniz", Color) = (1, 0.97, 0.9, 1)
        _Contraste ("Contraste", Range(0.5, 2)) = 1.18
        _Saturacion ("Saturacion", Range(0, 2)) = 1.25

        [Header(Realce)]
        _Brillo ("Autoiluminacion", Range(0, 1)) = 0.45
        _LuzGaleria ("Luz de galeria desde arriba", Range(0, 1)) = 0.45
        _Vineta ("Vineta", Range(0, 1)) = 0.15
        _ColorBorde ("Color del filo dorado", Color) = (1, 0.78, 0.4, 1)
        _AnchoBorde ("Ancho del filo", Range(0, 0.1)) = 0.018
        _IntensidadBorde ("Intensidad del filo", Range(0, 1)) = 0.3
        _ResistenciaNiebla ("Resistencia a la niebla", Range(0, 1)) = 0.7
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _Recorte;
                float _Redondo;
                half4 _Barniz;
                half _Contraste;
                half _Saturacion;
                half _Brillo;
                half _LuzGaleria;
                half _Vineta;
                half4 _ColorBorde;
                half _AnchoBorde;
                half _IntensidadBorde;
                half _ResistenciaNiebla;
            CBUFFER_END

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
                o.positionWS = posicion.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv;
                o.fogFactor = ComputeFogFactor(posicion.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 centrado = i.uv - 0.5;
                float radio = length(centrado) * 2.0;
                if (_Redondo > 0.5)
                    clip(1.0 - radio);

                half3 pintura = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv * _Recorte.xy + _Recorte.zw).rgb;
                half luminancia = dot(pintura, half3(0.2126, 0.7152, 0.0722));
                pintura = lerp(luminancia.xxx, pintura, _Saturacion);
                pintura = max(0.0, (pintura - 0.18) * _Contraste + 0.18);
                pintura *= _Barniz.rgb;

                float bordeRect = min(min(i.uv.x, 1.0 - i.uv.x), min(i.uv.y, 1.0 - i.uv.y));
                float distanciaBorde = _Redondo > 0.5 ? (1.0 - radio) * 0.5 : bordeRect;
                float alejamiento = _Redondo > 0.5 ? radio : max(abs(centrado.x), abs(centrado.y)) * 2.0;
                pintura *= lerp(1.0 - _Vineta, 1.0, smoothstep(1.0, 0.35, alejamiento));

                float2 foco = (i.uv - float2(0.5, 0.78)) * float2(1.6, 1.2);
                half galeria = 1.0 + _LuzGaleria * exp(-dot(foco, foco) * 3.0);

                float3 normal = normalize(i.normalWS);
                Light luz = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half3 directa = luz.color * saturate(dot(normal, luz.direction)) * luz.shadowAttenuation * luz.distanceAttenuation;
                half3 ambiente = SampleSH(normal);
                half3 iluminacion = directa + ambiente;

                half3 color = pintura * (iluminacion * galeria + _Brillo);

                half filo = smoothstep(_AnchoBorde, 0.0, distanciaBorde) * _IntensidadBorde;
                color += _ColorBorde.rgb * filo * (iluminacion + 0.25);

                color = lerp(MixFog(color, i.fogFactor), color, _ResistenciaNiebla);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
