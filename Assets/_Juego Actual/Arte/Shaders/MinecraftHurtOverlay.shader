Shader "Custom/MinecraftHurtOverlay"
{
    Properties
    {
        [HDR] _Color ("Color Flash", Color) = (1.0, 0.08, 0.08, 1.0)
        _Emission ("Brillo Adicional", Range(0, 3)) = 0.5
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry+10"
        }
        LOD 100

        Pass
        {
            Name "UnlitFlash"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Emission;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half3 col = _Color.rgb * (1.0 + _Emission);
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }

    // SubShader para Built-in Render Pipeline. Unity usa el primero cuyos tags
    // coincidan con el pipeline activo: en URP gana el de arriba, y en Built-in
    // ese se descarta por su tag "RenderPipeline" y se usa este. Sin esto, en un
    // proyecto Built-in el material cae al shader de error y pinta el personaje
    // de un color roto en lugar del rojo de daño.
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry+10" }
        LOD 100

        Pass
        {
            Name "UnlitFlashBuiltIn"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
            };

            fixed4 _Color;
            float _Emission;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return fixed4(_Color.rgb * (1.0 + _Emission), 1.0);
            }
            ENDCG
        }
    }

    FallBack Off
}
