Shader "Custom/FloorGrid"
{
    Properties
    {
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Source Blend (Fondo)", Float) = 5 // SrcAlpha por defecto
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination Blend (Fondo)", Float) = 10 // OneMinusSrcAlpha por defecto
        
        [HDR] _GridColor ("Color de la Cuadrícula", Color) = (0.2, 0.8, 1.0, 0.8)
        _BackgroundColor ("Color de Fondo", Color) = (0.0, 0.0, 0.0, 0.0)
        
        _GridSize ("Tamaño de la Celda (Unidades)", Float) = 1.0
        _GridOrigin ("Origen del Grid (Offset)", Vector) = (0,0,0,0)
        _LineThickness ("Grosor de Línea", Range(0.001, 0.5)) = 0.03
        
        _BeatPulse ("Pulso del Beat (0 a 1)", Range(0.0, 1.0)) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100
        
        // El modo de fusión ahora es dinámico y expuesto en el Inspector
        Blend [_SrcBlend] [_DstBlend]
        ZWrite Off
        Offset -1, -1

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD0;
            };

            fixed4 _GridColor;
            fixed4 _BackgroundColor;
            float _GridSize;
            float4 _GridOrigin;
            float _LineThickness;
            float _BeatPulse;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Protegemos contra dividir por cero si la grid no está configurada aún
                float safeGridSize = max(_GridSize, 0.001);
                
                // Alineamos el shader con el origen real del bounds.min del terreno
                float2 alignedPos = i.worldPos.xz - _GridOrigin.xy;
                float2 gridPos = alignedPos / safeGridSize;
                
                float2 gridUV = frac(gridPos);
                
                float2 dist = min(gridUV, 1.0 - gridUV);
                float2 derivative = fwidth(gridPos);
                
                float2 lineCheck = smoothstep(derivative, derivative + (_LineThickness * 0.5), dist);
                float isLine = 1.0 - min(lineCheck.x, lineCheck.y);
                
                // Multiplicamos TODO el color (RGB y Alpha) por el pulso
                // Esto es vital porque en modos de fusión como Additive (One, One), el Alpha se ignora.
                fixed4 activeGridColor = _GridColor * _BeatPulse;
                
                fixed4 finalColor = lerp(_BackgroundColor, activeGridColor, isLine);
                return finalColor;
            }
            ENDCG
        }
    }
}
