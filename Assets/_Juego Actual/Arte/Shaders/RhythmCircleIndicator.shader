// Indicador de ritmo proyectado en el suelo.
//
// EXACTAMENTE DOS CIRCULOS:
//   1. Anillo objetivo, fijo y delgado. Es la meta.
//   2. Anillo de aproximacion, grueso, que se contrae hasta encajar en el objetivo.
//
// Legibilidad estilo Hi-Fi Rush: trazos gruesos, color plano y contorno oscuro en
// lugar de halos difusos. El contorno es lo que hace que la forma se lea sobre
// cualquier fondo sin necesidad de brillo. Acierto, fallo y dano NO dibujan
// circulos nuevos: solo recolorean estos dos.
Shader "Custom/RhythmCircleIndicator"
{
    Properties
    {
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("Prueba de Profundidad (4=LEqual, 8=Always)", Float) = 4
        _ZOffsetFactor ("Z Offset Factor (Bias contra el suelo)", Float) = -4
        _ZOffsetUnits ("Z Offset Units", Float) = -8

        [Header(Colores)]
        [HDR] _BaseColor ("Anillo Movil Lejano (Hueso)", Color) = (0.85, 0.82, 0.75, 1.0)
        [HDR] _ReadyColor ("Anillo Movil Cercano (Sangre)", Color) = (0.92, 0.12, 0.14, 1.0)
        [HDR] _TargetColor ("Anillo Objetivo", Color) = (0.6, 0.57, 0.52, 0.9)
        [HDR] _HitColor ("Acierto", Color) = (1.0, 0.78, 0.25, 1.0)
        [HDR] _MissColor ("Fallo", Color) = (0.5, 0.1, 0.35, 1.0)
        [HDR] _DamageColor ("Dano Recibido", Color) = (0.95, 0.06, 0.12, 1.0)
        _OutlineColor ("Contorno", Color) = (0.03, 0.01, 0.04, 1.0)
        _BackingColor ("Sombra de Fondo", Color) = (0.02, 0.0, 0.01, 1.0)

        [Header(Estado Ritmico)]
        _FillProgress ("Progreso hacia la Nota (0 a 1)", Range(0.0, 1.0)) = 0.0
        _WindowOpen ("Ventana de Golpe Abierta (0 o 1)", Range(0.0, 1.0)) = 0.0
        _HitFeedback ("Intensidad Acierto (0 a 1)", Range(0.0, 1.0)) = 0.0
        _MissFeedback ("Intensidad Fallo (0 a 1)", Range(0.0, 1.0)) = 0.0
        _DamageFeedback ("Intensidad Dano (0 a 1)", Range(0.0, 1.0)) = 0.0
        _NextNoteSoon ("Viene Otra Nota Pegada (0 o 1)", Range(0.0, 1.0)) = 0.0
        _ChainPulseSpeed ("Velocidad del Latido de Cadena", Range(1.0, 20.0)) = 9.0

        [Header(Forma)]
        _OuterRadius ("Radio Inicial del Anillo Movil", Range(0.35, 0.95)) = 0.6
        _TargetRadius ("Radio del Anillo Objetivo", Range(0.1, 0.6)) = 0.3
        _RingThickness ("Grosor del Anillo Movil", Range(0.02, 0.3)) = 0.09
        _TargetThickness ("Grosor del Anillo Objetivo", Range(0.01, 0.2)) = 0.045
        _OutlineWidth ("Grosor del Contorno", Range(0.0, 0.08)) = 0.022
        _BackingStrength ("Opacidad de la Sombra de Fondo", Range(0.0, 0.9)) = 0.3
        _Smoothness ("Suavizado Anti-Aliasing", Range(0.002, 0.03)) = 0.008
        _GlowIntensity ("Intensidad de Color", Range(0.5, 2.5)) = 1.15
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent+15"
            "IgnoreProjector"="True"
        }
        LOD 100

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest [_ZTest]
        Cull Off
        Offset [_ZOffsetFactor], [_ZOffsetUnits]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            fixed4 _BaseColor;
            fixed4 _ReadyColor;
            fixed4 _TargetColor;
            fixed4 _HitColor;
            fixed4 _MissColor;
            fixed4 _DamageColor;
            fixed4 _OutlineColor;
            fixed4 _BackingColor;

            float _FillProgress;
            float _WindowOpen;
            float _HitFeedback;
            float _MissFeedback;
            float _DamageFeedback;
            float _NextNoteSoon;
            float _ChainPulseSpeed;

            float _OuterRadius;
            float _TargetRadius;
            float _RingThickness;
            float _TargetThickness;
            float _OutlineWidth;
            float _BackingStrength;
            float _Smoothness;
            float _GlowIntensity;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            // Anillo de borde duro: 1 dentro del grosor, 0 fuera. Sin halo.
            float Ring(float dist, float r, float thickness)
            {
                float halfWidth = thickness * 0.5;
                return 1.0 - smoothstep(halfWidth - _Smoothness, halfWidth + _Smoothness, abs(dist - r));
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 p = (i.uv - 0.5) * 2.0;
                float dist = length(p);

                float boundary = 1.0 - smoothstep(0.96, 0.998, dist);
                if (boundary <= 0.001)
                    discard;

                // --- CIRCULO 1: anillo objetivo, fijo ---
                float targetFill = Ring(dist, _TargetRadius, _TargetThickness);
                float targetOutline = Ring(dist, _TargetRadius, _TargetThickness + _OutlineWidth * 2.0);

                // --- CIRCULO 2: anillo movil, se contrae hasta encajar en el objetivo ---
                float moveRadius = lerp(_OuterRadius, _TargetRadius, _FillProgress);
                float moveFill = Ring(dist, moveRadius, _RingThickness);
                float moveOutline = Ring(dist, moveRadius, _RingThickness + _OutlineWidth * 2.0);

                // Entra suave para no hacer "pop" al inicio de cada nota.
                float appear = smoothstep(0.0, 0.08, _FillProgress);
                moveFill *= appear;
                moveOutline *= appear;

                // De hueso a sangre conforme se acerca: el color da la lectura del
                // tiempo restante aunque se mire de reojo.
                fixed4 moveColor = lerp(_BaseColor, _ReadyColor, saturate(_FillProgress * 1.2));

                // --- Sombra de fondo: degradado suave, sin borde, para separar del terreno ---
                float backing = (1.0 - smoothstep(0.0, _OuterRadius, dist)) * _BackingStrength;

                // --- Pintado por capas (painter's algorithm), no aditivo ---
                // Lo aditivo era lo que generaba halos que se leian como circulos extra.
                fixed3 rgb = _BackingColor.rgb;
                float alpha = backing;

                rgb = lerp(rgb, _OutlineColor.rgb, targetOutline);
                alpha = max(alpha, targetOutline * _OutlineColor.a);

                rgb = lerp(rgb, _TargetColor.rgb, targetFill);
                alpha = max(alpha, targetFill * _TargetColor.a);

                rgb = lerp(rgb, _OutlineColor.rgb, moveOutline);
                alpha = max(alpha, moveOutline * _OutlineColor.a);

                rgb = lerp(rgb, moveColor.rgb, moveFill);
                alpha = max(alpha, moveFill * moveColor.a);

                // --- Estados: recolorean los dos anillos, no anaden formas ---
                float rings = max(targetFill, moveFill);

                // Ventana abierta: el objetivo se enciende con el color de "pulsa ahora".
                rgb = lerp(rgb, _ReadyColor.rgb, targetFill * _WindowOpen);

                // Cadena: si tras esta nota viene otra pegada, el anillo objetivo late.
                // Avisa de la racha sin dibujar un tercer circulo.
                float chainPulse = (0.5 + 0.5 * sin(_Time.y * _ChainPulseSpeed)) * _NextNoteSoon;
                rgb = lerp(rgb, _ReadyColor.rgb, targetFill * chainPulse * 0.75);

                // El acierto blanquea un instante antes de asentarse en el color de acierto,
                // para que el golpe se sienta seco.
                rgb = lerp(rgb, _HitColor.rgb, saturate(rings * _HitFeedback));
                rgb = lerp(rgb, fixed3(1.0, 1.0, 1.0), saturate(rings * _HitFeedback * _HitFeedback * 0.8));
                rgb = lerp(rgb, _MissColor.rgb, saturate(rings * _MissFeedback));
                rgb = lerp(rgb, _DamageColor.rgb, saturate(rings * _DamageFeedback));

                // El dano tambien tine la sombra, para que se sienta en todo el charco.
                rgb = lerp(rgb, _DamageColor.rgb * 0.35, backing * _DamageFeedback);

                rgb *= _GlowIntensity;

                return fixed4(rgb, saturate(alpha) * boundary);
            }
            ENDCG
        }
    }
    FallBack Off
}
