// Solo editor (GeneradorSueloGrid): dibuja el suelo modelado visto desde arriba y guarda la luz de su lightmap,
// para que el plano de SueloGrid se ilumine igual que el suelo original sin volver a hornear.
Shader "Hidden/UdeM/CapturaLuzSuelo"
{
    Properties
    {
        _Lightmap ("Lightmap", 2D) = "black" {}
    }

    SubShader
    {
        Pass
        {
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"

            TEXTURE2D(_Lightmap);
            SAMPLER(sampler_Lightmap);
            float4 _LightmapST;
            float4 _Rect;
            float4x4 _Matriz;
            float4x4 _MatrizNormal;
            float _VoltearY;
            float _UsarUV0;

            struct Atributos
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uvLuz : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            Varyings Vert(Atributos v)
            {
                Varyings o;
                float3 posicion = mul(_Matriz, float4(v.positionOS.xyz, 1.0)).xyz;
                float2 xy = (posicion.xz - _Rect.xy) * _Rect.zw * 2.0 - 1.0;
                if (_VoltearY > 0.5)
                    xy.y = -xy.y;
                o.positionCS = float4(xy, 0.5, 1.0);
                o.normalWS = mul((float3x3)_MatrizNormal, v.normalOS);
                o.uvLuz = (_UsarUV0 > 0.5 ? v.uv0 : v.uv1) * _LightmapST.xy + _LightmapST.zw;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                // Solo caras que miran arriba: los biseles y paredes de las juntas dejan huecos que se rellenan despues.
                if (abs(normalize(i.normalWS).y) < 0.85)
                    discard;
                half4 codificada = SAMPLE_TEXTURE2D(_Lightmap, sampler_Lightmap, i.uvLuz);
                half3 luz = DecodeLightmap(codificada, half4(LIGHTMAP_HDR_MULTIPLIER, LIGHTMAP_HDR_EXPONENT, 0.0, 0.0));
                return half4(luz, 1.0);
            }
            ENDHLSL
        }
    }
}
