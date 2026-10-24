Shader "UdeM/Ambientacion/SkyboxNoche"
{
    Properties
    {
        [Header(Cielo)]
        _ColorCenit ("Color cenit", Color) = (0.015, 0.022, 0.055, 1)
        _ColorHorizonte ("Color horizonte", Color) = (0.07, 0.095, 0.16, 1)
        _ColorBruma ("Bruma del horizonte (la iguala a la niebla el setup)", Color) = (0.085, 0.11, 0.15, 1)
        _AlturaBruma ("Altura de la bruma", Range(0.01, 1)) = 0.16
        _Exposicion ("Exposicion", Range(0, 4)) = 1

        [Header(Luna)]
        _DireccionLuna ("Direccion de la luna (la pone el setup)", Vector) = (0.14, 0.53, 0.83, 0)
        _ColorLuna ("Color luna", Color) = (0.86, 0.93, 1, 1)
        _TamanoLuna ("Tamano luna", Range(0.005, 0.2)) = 0.05
        _BrilloLuna ("Brillo luna", Range(0, 20)) = 3.5
        _ColorHalo ("Color halo", Color) = (0.36, 0.52, 0.8, 1)
        _TamanoHalo ("Concentracion halo", Range(1, 200)) = 14
        _BrilloHalo ("Brillo halo", Range(0, 4)) = 0.9

        [Header(Estrellas)]
        _DensidadEstrellas ("Densidad", Range(10, 400)) = 170
        _CantidadEstrellas ("Cantidad", Range(0, 1)) = 0.14
        _BrilloEstrellas ("Brillo", Range(0, 10)) = 2.2
        _Titileo ("Titileo", Range(0, 10)) = 2

        [Header(Fuego alrededor de la luna (apocalipsis))]
        _IntensidadFuego ("Intensidad", Range(0, 10)) = 0
        [HDR] _ColorFuego ("Color fuego", Color) = (1, 0.6, 0.15, 1)
        [HDR] _ColorFuegoBorde ("Color borde", Color) = (0.8, 0.1, 0.04, 1)
        _TamanoFuego ("Concentracion", Range(0.5, 20)) = 2.2
        _VelocidadFuego ("Velocidad", Range(0, 1)) = 0.12

        [Header(Nubes)]
        _ColorNubes ("Color nubes iluminadas", Color) = (0.42, 0.5, 0.64, 1)
        _ColorNubesSombra ("Color nubes en sombra", Color) = (0.035, 0.045, 0.08, 1)
        _CoberturaNubes ("Cobertura", Range(0, 1)) = 0.58
        _SuavidadNubes ("Suavidad", Range(0.01, 1)) = 0.32
        _EscalaNubes ("Escala", Range(0.1, 10)) = 1.6
        _VelocidadNubes ("Velocidad", Range(0, 0.2)) = 0.008
        _OpacidadNubes ("Opacidad", Range(0, 1)) = 0.93
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            float4 _ColorCenit;
            float4 _ColorHorizonte;
            float4 _ColorBruma;
            float _AlturaBruma;
            float _Exposicion;

            float4 _DireccionLuna;
            float4 _ColorLuna;
            float _TamanoLuna;
            float _BrilloLuna;
            float4 _ColorHalo;
            float _TamanoHalo;
            float _BrilloHalo;

            float _DensidadEstrellas;
            float _CantidadEstrellas;
            float _BrilloEstrellas;
            float _Titileo;

            float _IntensidadFuego;
            float4 _ColorFuego;
            float4 _ColorFuegoBorde;
            float _TamanoFuego;
            float _VelocidadFuego;

            float4 _ColorNubes;
            float4 _ColorNubesSombra;
            float _CoberturaNubes;
            float _SuavidadNubes;
            float _EscalaNubes;
            float _VelocidadNubes;
            float _OpacidadNubes;

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float hash21(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            float ruido(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float fbm(float2 p)
            {
                float valor = 0.0;
                float amplitud = 0.5;
                for (int k = 0; k < 5; k++)
                {
                    valor += amplitud * ruido(p);
                    p = p * 2.03 + float2(17.1, 9.2);
                    amplitud *= 0.5;
                }
                return valor / 0.96875;
            }

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float3 dir = normalize(i.dir);
                float3 luna = normalize(_DireccionLuna.xyz);
                float cosLuna = dot(dir, luna);
                float angulo = acos(clamp(cosLuna, -1.0, 1.0));

                float altura = saturate(dir.y);
                float3 cielo = lerp(_ColorHorizonte.rgb, _ColorCenit.rgb, pow(altura, 0.45));

                float3 pe = dir * _DensidadEstrellas;
                float3 celda = floor(pe);
                float3 local = frac(pe) - 0.5;
                float azar = hash31(celda);
                float3 desvio = float3(hash31(celda + 7.1), hash31(celda + 13.7), hash31(celda + 29.3)) - 0.5;
                float distancia = length(local - desvio * 0.5);
                float estrella = step(1.0 - _CantidadEstrellas, azar) * smoothstep(0.22, 0.0, distancia);
                float titileo = 0.65 + 0.35 * sin(_Time.y * _Titileo + azar * 120.0);
                float3 tono = lerp(float3(0.8, 0.86, 1.0), float3(1.0, 0.94, 0.85), hash31(celda + 3.3));
                float3 estrellas = estrella * titileo * _BrilloEstrellas * tono * smoothstep(0.02, 0.3, dir.y);

                float3 eje = abs(luna.y) < 0.99 ? float3(0, 1, 0) : float3(1, 0, 0);
                float3 tangente = normalize(cross(eje, luna));
                float3 bitangente = cross(luna, tangente);
                float2 enDisco = float2(dot(dir, tangente), dot(dir, bitangente)) / _TamanoLuna;
                float radio = length(enDisco);
                float disco = smoothstep(1.0, 0.9, radio) * step(0.0, cosLuna);
                float mares = fbm(enDisco * 1.8 + 3.7);
                float relieve = lerp(0.62, 1.0, smoothstep(0.35, 0.65, mares));
                float borde = lerp(0.75, 1.0, sqrt(saturate(1.0 - radio * radio)));
                float3 colorLuna = _ColorLuna.rgb * _BrilloLuna * relieve * borde;

                float halo = exp(-angulo * _TamanoHalo) * _BrilloHalo + exp(-angulo * 2.5) * _BrilloHalo * 0.25;

                float elevacion = max(dir.y, 0.0) + 0.1;
                float2 uvNubes = dir.xz / elevacion * (0.18 * _EscalaNubes);
                float2 viento = _Time.y * _VelocidadNubes * float2(1.0, 0.35);
                float densidad = fbm(uvNubes + viento) * 0.75 + fbm(uvNubes * 3.1 - viento * 1.7 + 11.3) * 0.25;
                float umbral = 1.0 - _CoberturaNubes;
                float nubes = smoothstep(umbral - _SuavidadNubes * 0.5, umbral + _SuavidadNubes * 0.5, densidad);
                nubes *= smoothstep(-0.05, 0.12, dir.y);
                float grosor = smoothstep(umbral, umbral + 0.35, densidad);
                float luzLuna = exp(-angulo * 2.2);
                float3 colorNubes = lerp(_ColorNubesSombra.rgb, _ColorNubes.rgb, saturate(0.25 + luzLuna * (1.4 - grosor)));
                colorNubes += _ColorHalo.rgb * exp(-angulo * 6.0) * (1.0 - grosor) * 1.5;

                cielo += estrellas * (1.0 - nubes);
                cielo = lerp(cielo, colorLuna, disco);
                cielo += _ColorHalo.rgb * halo * (1.0 - disco);

                float opacidad = nubes * _OpacidadNubes;
                cielo = lerp(cielo, colorNubes, opacidad);
                cielo += _ColorHalo.rgb * halo * 0.35 * opacidad;

                if (_IntensidadFuego > 0.0)
                {
                    float2 alrededor = float2(dot(dir, tangente), dot(dir, bitangente));
                    float mascara = exp(-angulo * _TamanoFuego) * step(0.0, cosLuna);
                    float llamas = fbm(alrededor * 5.0 + float2(0.0, -_Time.y * _VelocidadFuego));
                    llamas = llamas * 0.7 + fbm(alrededor * 11.0 - float2(_Time.y * _VelocidadFuego * 0.6, 0.0)) * 0.3;
                    float forma = smoothstep(0.35, 0.85, llamas + mascara * 0.6) * pow(mascara, 1.3);
                    float3 fuego = lerp(_ColorFuegoBorde.rgb, _ColorFuego.rgb, saturate(llamas * 1.4 + mascara - 0.4));
                    cielo += fuego * forma * _IntensidadFuego * (1.0 - opacidad * 0.45);
                }

                float bruma = 1.0 - smoothstep(-0.03, _AlturaBruma, dir.y);
                cielo = lerp(cielo, _ColorBruma.rgb, bruma);

                return float4(cielo * _Exposicion, 1.0);
            }
            ENDCG
        }
    }

    Fallback Off
}
