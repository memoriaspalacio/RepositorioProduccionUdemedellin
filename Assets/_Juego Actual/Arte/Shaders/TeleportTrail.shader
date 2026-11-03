Shader "Custom/TeleportTrail"
{
    Properties
    {
        [HDR] _Color ("Color Principal", Color) = (0, 1, 1, 1)
        _MainTex ("Textura de Energia (Ruido)", 2D) = "white" {}
        _ScrollSpeed ("Velocidad de Flujo (X, Y)", Vector) = (-2, 0, 0, 0)
        _EdgeSoftness ("Suavidad de Bordes", Range(0.1, 3.0)) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        LOD 100

        // Mezcla Aditiva para efectos magicos/energia
        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR; // Recibe el color/fade del TrailRenderer
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                UNITY_FOG_COORDS(1)
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float2 _ScrollSpeed;
            float _EdgeSoftness;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                
                // Animamos las UVs para que la textura parezca fluir/moverse
                o.uv = TRANSFORM_TEX(v.uv, _MainTex) + _ScrollSpeed * _Time.y;
                
                // Multiplicamos por el color del TrailRenderer (útil para que se desvanezca la cola)
                o.color = v.color * _Color;
                
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Leer la textura desplazada
                fixed4 col = tex2D(_MainTex, i.uv) * i.color;
                
                // En un TrailRenderer, la V (uv.y) va de 0 a 1 a lo ancho del trail.
                // Usamos el Seno de V multiplicado por PI para hacer que los bordes (0 y 1) se desvanezcan,
                // dejando solo el centro brillante como un rayo de energia.
                float edgeFade = sin(i.uv.y * 3.14159);
                col.a *= pow(max(edgeFade, 0.001), _EdgeSoftness);
                
                // Al ser aditivo, multiplicamos el RGB por el Alpha para que no haya artefactos brillantes
                col.rgb *= col.a;

                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
