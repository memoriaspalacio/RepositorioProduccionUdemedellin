Shader "Custom/RhythmCircleIndicator"
{
    Properties
    {
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("Prueba de Profundidad (4=LEqual, 8=Always)", Float) = 4
        _ZOffsetFactor ("Z Offset Factor (Bias contra el suelo)", Float) = -4
        _ZOffsetUnits ("Z Offset Units", Float) = -4

        [HDR] _BaseColor ("Color Base (Inactivo)", Color) = (0.15, 0.65, 1.0, 0.45)
        [HDR] _FillColor ("Color de Relleno (Carga)", Color) = (0.2, 0.9, 1.0, 0.9)
        [HDR] _ReadyColor ("Color Ventana Abierta (Hit Ready)", Color) = (1.0, 0.88, 0.2, 1.0)
        [HDR] _HitColor ("Color de Acierto (Hit Feedback)", Color) = (1.0, 0.98, 0.5, 1.0)
        [HDR] _MissColor ("Color de Fallo (Miss/Whiff)", Color) = (1.0, 0.2, 0.2, 0.8)

        _FillProgress ("Progreso de Relleno (0 a 1)", Range(0.0, 1.0)) = 0.0
        _WindowOpen ("Ventana de Golpe Abierta (0 o 1)", Range(0.0, 1.0)) = 0.0
        _HitFeedback ("Intensidad Feedback Hit (0 a 1)", Range(0.0, 1.0)) = 0.0
        _MissFeedback ("Intensidad Feedback Fallo (0 a 1)", Range(0.0, 1.0)) = 0.0
        _BeatPulse ("Pulso Subyacente (0 a 1)", Range(0.0, 1.0)) = 0.0

        _TargetRadius ("Radio del Anillo", Range(0.2, 0.9)) = 0.70
        _RingThickness ("Grosor del Anillo", Range(0.01, 0.2)) = 0.06
        _CoreRadius ("Radio del Nucleo", Range(0.0, 0.3)) = 0.08
        _Smoothness ("Suavizado Anti-Aliasing", Range(0.002, 0.04)) = 0.012
        _GlowIntensity ("Intensidad de Brillo General", Range(0.5, 3.5)) = 1.4
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
            fixed4 _FillColor;
            fixed4 _ReadyColor;
            fixed4 _HitColor;
            fixed4 _MissColor;

            float _FillProgress;
            float _WindowOpen;
            float _HitFeedback;
            float _MissFeedback;
            float _BeatPulse;

            float _TargetRadius;
            float _RingThickness;
            float _CoreRadius;

            float _Smoothness;
            float _GlowIntensity;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Coordenadas centradas de -1 a 1
                float2 p = (i.uv - 0.5) * 2.0;
                float dist = length(p);

                // Mascara circular exterior para evitar cortes cuadrados bruscos
                float boundaryMask = 1.0 - smoothstep(0.95, 0.99, dist);
                if (boundaryMask <= 0.001)
                    discard;

                // Angulo horario normalizado (0 arriba a las 12, avanza en sentido horario)
                float angle = atan2(p.x, p.y) / 6.28318530718;
                if (angle < 0.0) angle += 1.0;

                float halfThick = _RingThickness * 0.5;
                float targetDist = abs(dist - _TargetRadius);

                // --- Anillo guia (siempre visible, tenue) ---
                float guideRing = 1.0 - smoothstep(halfThick - _Smoothness, halfThick + _Smoothness, targetDist);
                fixed4 currentTargetColor = lerp(_BaseColor, _ReadyColor, _WindowOpen);

                // --- Relleno tipo gauge: avanza en sentido horario desde las 12 ---
                float angleDiff = _FillProgress - angle;
                float sweepMask = step(0.0, angleDiff) * guideRing;

                // Chispa brillante en la punta de avance
                float tipDist = abs(angle - _FillProgress);
                float tipGlow = exp(-tipDist * 40.0) * guideRing * 1.6 * step(0.001, _FillProgress);

                fixed4 activeFillColor = lerp(_FillColor, _ReadyColor, _WindowOpen * 0.65);

                // --- Nucleo central con pulso de ritmo ---
                float coreRadius = _CoreRadius * (1.0 + _BeatPulse * 0.25);
                float coreSharp = 1.0 - smoothstep(coreRadius - _Smoothness, coreRadius + _Smoothness, dist);
                float coreGlow = exp(-dist * 14.0) * 0.45;
                float coreMask = saturate(coreSharp + coreGlow) * (0.35 + _BeatPulse * 0.45);

                // --- Retroalimentacion de Golpe (shockwave + destello) ---
                float waveProgress = 1.0 - _HitFeedback;
                float waveRadius = _TargetRadius + waveProgress * 0.38;
                float waveDist = abs(dist - waveRadius);
                float waveThick = 0.045 * (1.0 + waveProgress * 0.8);
                float shockwave = (1.0 - smoothstep(waveThick * 0.5 - _Smoothness, waveThick * 0.5 + _Smoothness, waveDist)) * _HitFeedback;
                shockwave += exp(-waveDist * 16.0) * 0.45 * _HitFeedback;

                float burstFlare = exp(-dist * 4.5) * _HitFeedback * 2.0;

                // --- Destello de fallo ---
                float missFlash = _MissFeedback * exp(-dist * 4.0);

                // --- Composicion final ---
                fixed4 col = fixed4(0, 0, 0, 0);

                col += currentTargetColor * (guideRing - sweepMask); // parte aun no rellenada del anillo (tenue)
                col += activeFillColor * sweepMask;                  // parte ya rellenada (brillante)
                col += activeFillColor * tipGlow;                    // punta de avance
                col += lerp(_BaseColor, _ReadyColor, _WindowOpen) * coreMask;
                col += _HitColor * (shockwave * 1.5 + burstFlare);

                col = lerp(col, _HitColor * 1.6, _HitFeedback * 0.45);
                col = lerp(col, _MissColor, missFlash);

                col.rgb *= _GlowIntensity;

                float alpha = saturate(guideRing * currentTargetColor.a +
                                       sweepMask * activeFillColor.a * 0.6 +
                                       tipGlow * 0.9 +
                                       coreMask * 0.5 +
                                       shockwave * 1.2 +
                                       burstFlare * 0.9 +
                                       missFlash * 0.65);

                col.a = saturate(alpha * boundaryMask);

                return col;
            }
            ENDCG
        }
    }
    FallBack "Transparent/Cutout/VertexLit"
}
