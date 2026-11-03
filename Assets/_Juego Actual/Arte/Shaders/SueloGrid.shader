// Suelo plano que imita las piezas modeladas (losas, ladrillo, piedras) o un terreno de parcelas con senderos,
// con relieve falso y alineado a las celdas del MeshGrid: el borde de cada celda siempre cae en una junta o un sendero.
Shader "UdeM/Suelo/SueloGrid"
{
    Properties
    {
        [Header(Textura)]
        [MainTexture] _BaseMap ("Textura del suelo", 2D) = "white" {}
        [MainColor] _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _EscalaTextura ("Tamano de la textura (unidades)", Float) = 2
        _MezclaTextura ("Cuanto se ve la textura", Range(0, 1)) = 1
        [ToggleUI] _RotarTextura ("Girar la textura en cada pieza", Float) = 1

        [Header(Piezas)]
        [Enum(Losas, 0, Ladrillo, 1, Piedras, 2, Liso, 3, Parcelas, 4)] _Patron ("Patron", Float) = 0
        [IntRange] _Divisiones ("Losas o piedras por celda (por lado)", Range(1, 4)) = 2
        [IntRange] _FilasLadrillo ("Filas de ladrillo por celda", Range(1, 16)) = 8
        [IntRange] _LadrillosFila ("Ladrillos por fila", Range(1, 8)) = 4
        [ToggleUI] _AlternarLadrillo ("Alternar direccion del ladrillo por celda", Float) = 0
        _Junta ("Ancho de junta (unidades)", Range(0, 0.3)) = 0.05
        _Bisel ("Ancho del bisel (unidades)", Range(0.001, 0.4)) = 0.05
        _Relieve ("Altura del relieve", Range(0, 4)) = 1
        _RadioEsquina ("Redondeo de esquinas (unidades)", Range(0, 0.3)) = 0.03
        _Astillas ("Esquinas rotas", Range(0, 1)) = 0
        _Grietas ("Piezas partidas", Range(0, 1)) = 0
        _Irregularidad ("Irregularidad de las piedras", Range(0, 1)) = 0.8

        [Header(Color de las piezas)]
        _ColorJunta ("Color de la junta", Color) = (0.12, 0.11, 0.1, 1)
        _VariacionColor ("Variacion entre piezas", Range(0, 1)) = 0.2
        _Oclusion ("Sombra en juntas", Range(0, 1)) = 0.6
        _Suavidad ("Brillo (smoothness)", Range(0, 1)) = 0.25

        [Header(Parcelas)]
        _ColorSeco ("Color del pasto seco", Color) = (0.62, 0.55, 0.32, 1)
        _VariacionParcela ("Variacion entre parcelas", Range(0, 1)) = 0.5
        _TexturaSenda ("Textura del sendero (alfa = altura de la grava)", 2D) = "gray" {}
        _ColorSenda ("Color del sendero", Color) = (1, 1, 1, 1)
        _EscalaSenda ("Tamano de la textura del sendero (unidades)", Float) = 1.5
        _AnchoSenda ("Ancho del sendero (unidades)", Range(0, 1)) = 0.35
        _IrregularSenda ("Borde irregular del sendero", Range(0, 1)) = 0.6
        _TexturaPiedra ("Textura de los mojones", 2D) = "gray" {}
        _ColorPiedra ("Color de los mojones", Color) = (0.6, 0.6, 0.62, 1)
        _TamanoMojon ("Mojon en cada cruce (unidades, 0 = sin mojon)", Range(0, 0.8)) = 0.3

        [Header(Grid)]
        [Enum(Brillo, 0, Juntas, 1, Grabado, 2, Pintura, 3, Tinte, 4, Oculto, 5)] _MezclaGrid ("Como se integra el grid", Float) = 1
        [Enum(Lineas, 0, Discontinuas, 1, Cruces, 2, Esquinas, 3, Puntos, 4, Marco, 5, Resplandor, 6, Tablero, 7)] _EstiloGrid ("Estilo del grid", Float) = 0
        [HDR] _GridColor ("Color del grid", Color) = (1, 0.45, 0.15, 1)
        _IntensidadGrid ("Intensidad", Range(0, 4)) = 1
        _LineThickness ("Grosor de linea (fraccion de celda)", Range(0.001, 0.5)) = 0.03
        _LargoMarca ("Largo de marcas y puntos", Range(0.02, 0.5)) = 0.15
        _MargenGrid ("Margen desde el borde", Range(0, 0.45)) = 0.08
        _DesgasteGrid ("Desgaste", Range(0, 1)) = 0
        _ReaccionBeat ("Cuanto reacciona al beat", Range(0, 1)) = 1
        _RelieveGrabado ("Profundidad del grabado", Range(0, 2)) = 1
        _RadioRevelado ("Radio visible alrededor del jugador (0 = todo)", Float) = 0
        _SuavidadRevelado ("Suavidad del borde visible", Float) = 2

        [Header(Lo ajustan SueloGrid y GridBeatVisualizer)]
        _GridSize ("Tamano de celda", Float) = 2
        _GridOrigin ("Origen del grid", Vector) = (0, 0, 0, 0)
        _BeatPulse ("Pulso del beat", Range(0, 1)) = 1
        _CentroRevelado ("Centro del revelado", Vector) = (0, 0, 0, 0)
        [NoScaleOffset] _LuzSuelo ("Luz horneada capturada", 2D) = "white" {}
        _LuzSueloRect ("Rect de la luz capturada", Vector) = (0, 0, 0, 0)
        _UsarLuzSuelo ("Usar luz capturada", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "GridEstilos.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            float _EscalaTextura;
            half _MezclaTextura;
            float _RotarTextura;
            float _Patron;
            float _Divisiones;
            float _FilasLadrillo;
            float _LadrillosFila;
            float _AlternarLadrillo;
            float _Junta;
            float _Bisel;
            float _Relieve;
            float _RadioEsquina;
            float _Astillas;
            float _Grietas;
            float _Irregularidad;
            half4 _ColorJunta;
            half _VariacionColor;
            half _Oclusion;
            half _Suavidad;
            half4 _ColorSeco;
            half _VariacionParcela;
            half4 _ColorSenda;
            float _EscalaSenda;
            float _AnchoSenda;
            float _IrregularSenda;
            half4 _ColorPiedra;
            float _TamanoMojon;
            float _MezclaGrid;
            float _EstiloGrid;
            half4 _GridColor;
            half _IntensidadGrid;
            float _LineThickness;
            float _LargoMarca;
            float _MargenGrid;
            float _DesgasteGrid;
            half _ReaccionBeat;
            half _RelieveGrabado;
            float _RadioRevelado;
            float _SuavidadRevelado;
            float _GridSize;
            float4 _GridOrigin;
            half _BeatPulse;
            float4 _CentroRevelado;
            float4 _LuzSueloRect;
            float _UsarLuzSuelo;
        CBUFFER_END

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        TEXTURE2D(_LuzSuelo);
        SAMPLER(sampler_LuzSuelo);
        TEXTURE2D(_TexturaSenda);
        SAMPLER(sampler_TexturaSenda);
        TEXTURE2D(_TexturaPiedra);
        SAMPLER(sampler_TexturaPiedra);

        #define PATRON_LISO 3
        #define PATRON_PARCELAS 4

        #define MEZCLA_BRILLO 0
        #define MEZCLA_JUNTAS 1
        #define MEZCLA_GRABADO 2
        #define MEZCLA_PINTURA 3
        #define MEZCLA_TINTE 4

        // Una pieza del suelo vista desde el pixel actual.
        struct Pieza
        {
            float2 id;          // identifica la pieza (semilla de azar)
            float interior;     // distancia en unidades desde el borde de la pieza hacia dentro
            float2 haciaFuera;  // direccion xz hacia el borde mas cercano
        };

        struct Suelo
        {
            half3 albedo;
            half3 emision;
            float3 normalWS;
            half suavidad;
            half oclusion;
        };

        void CajaRedondeada(float2 p, float2 mitad, float radio, out float distancia, out float2 haciaFuera)
        {
            radio = min(radio, min(mitad.x, mitad.y) * 0.9);
            float2 q = abs(p) - (mitad - radio);
            distancia = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - radio;
            float2 signo = float2(p.x < 0.0 ? -1.0 : 1.0, p.y < 0.0 ? -1.0 : 1.0);
            if (q.x > 0.0 && q.y > 0.0)
                haciaFuera = normalize(q) * signo;
            else
                haciaFuera = q.x > q.y ? float2(signo.x, 0.0) : float2(0.0, signo.y);
        }

        Pieza PiezaLosas(float2 w, float tamano)
        {
            float lado = tamano / max(1.0, round(_Divisiones));
            float2 t = w / lado;
            float2 id = floor(t);
            float2 p = (frac(t) - 0.5) * lado;
            float mitad = lado * 0.5;

            float distancia;
            float2 haciaFuera;
            CajaRedondeada(p, mitad.xx, _RadioEsquina, distancia, haciaFuera);

            // Esquina rota: un corte en diagonal que se come una punta.
            float2 azar = GridHash22(id + 0.37);
            if (azar.x < _Astillas)
            {
                float2 esquina = float2(azar.y < 0.5 ? -1.0 : 1.0, frac(azar.y * 7.13) < 0.5 ? -1.0 : 1.0);
                float2 normal = esquina * 0.70710678;
                float corte = lado * lerp(0.15, 0.45, frac(azar.x * 13.7));
                float distanciaCorte = dot(p, normal) - (mitad * 1.41421356 - corte * 0.70710678);
                if (distanciaCorte > distancia)
                {
                    distancia = distanciaCorte;
                    haciaFuera = normal;
                }
            }

            // Pieza partida: una grieta quebrada la divide en dos.
            float azarGrieta = GridHash21(id + 11.3);
            if (azarGrieta < _Grietas)
            {
                float angulo = azarGrieta * 41.0;
                float2 normal = float2(-sin(angulo), cos(angulo));
                float desplazamiento = (GridHash21(id + 5.1) - 0.5) * lado * 0.5;
                float quiebre = (GridRuido(p / lado * 5.0 + id * 3.1) - 0.5) * lado * 0.15;
                float lado_ = dot(p, normal) - desplazamiento + quiebre;
                float distanciaGrieta = -abs(lado_) - _Junta * 0.2;
                if (distanciaGrieta > distancia)
                {
                    distancia = distanciaGrieta;
                    haciaFuera = lado_ < 0.0 ? normal : -normal;
                }
                id += lado_ < 0.0 ? 0.0 : 0.5;
            }

            Pieza pieza = (Pieza)0;
            pieza.id = id;
            pieza.interior = -distancia;
            pieza.haciaFuera = haciaFuera;
            return pieza;
        }

        Pieza PiezaLadrillo(float2 w, float tamano)
        {
            float2 celda = floor(w / tamano);
            float2 local = w - celda * tamano;
            bool girar = _AlternarLadrillo > 0.5 && fmod(abs(celda.x + celda.y), 2.0) > 0.5;
            if (girar)
                local = local.yx;

            float filas = max(1.0, round(_FilasLadrillo));
            float porFila = max(1.0, round(_LadrillosFila));
            float alto = tamano / filas;
            float largo = tamano / porFila;

            // Las filas impares van corridas medio ladrillo, con medios ladrillos en los bordes de la celda.
            float fila = min(floor(local.y / alto), filas - 1.0);
            float desfase = fmod(fila, 2.0) * 0.5;
            float x = min(local.x / largo + desfase, porFila + desfase - 0.0001);
            float k = floor(x);
            float inicio = (max(k, desfase) - desfase) * largo;
            float fin = (min(k + 1.0, porFila + desfase) - desfase) * largo;

            float2 centro = float2(inicio + fin, (fila * 2.0 + 1.0) * alto) * 0.5;
            float2 mitad = float2(fin - inicio, alto) * 0.5;

            float distancia;
            float2 haciaFuera;
            CajaRedondeada(local - centro, mitad, _RadioEsquina, distancia, haciaFuera);

            Pieza pieza = (Pieza)0;
            pieza.id = celda * 64.0 + float2(k, fila);
            pieza.interior = -distancia;
            pieza.haciaFuera = girar ? haciaFuera.yx : haciaFuera;
            return pieza;
        }

        float2 PuntoPiedra(float2 celda, float2 casilla, float divisiones)
        {
            return (casilla + 0.5 + (GridHash22(celda * 7.0 + casilla) - 0.5) * _Irregularidad) / divisiones;
        }

        // Piedras irregulares (Voronoi) que nunca cruzan el borde de la celda.
        Pieza PiezaPiedras(float2 w, float tamano)
        {
            float2 celda = floor(w / tamano);
            float2 local = w / tamano - celda;
            float divisiones = clamp(round(_Divisiones), 1.0, 4.0);
            int n = (int)divisiones;

            float mejor = 1e5;
            float2 puntoMejor = 0.0;
            float2 casillaMejor = 0.0;
            for (int j = 0; j < n; j++)
            {
                for (int i = 0; i < n; i++)
                {
                    float2 casilla = float2(i, j);
                    float2 punto = PuntoPiedra(celda, casilla, divisiones);
                    float2 delta = local - punto;
                    float d2 = dot(delta, delta);
                    if (d2 < mejor)
                    {
                        mejor = d2;
                        puntoMejor = punto;
                        casillaMejor = casilla;
                    }
                }
            }

            float2 bordeCelda = min(local, 1.0 - local);
            float borde = min(bordeCelda.x, bordeCelda.y);
            float2 haciaFuera = bordeCelda.x < bordeCelda.y
                ? float2(local.x < 0.5 ? -1.0 : 1.0, 0.0)
                : float2(0.0, local.y < 0.5 ? -1.0 : 1.0);

            for (int jj = 0; jj < n; jj++)
            {
                for (int ii = 0; ii < n; ii++)
                {
                    float2 casilla = float2(ii, jj);
                    if (all(casilla == casillaMejor))
                        continue;
                    float2 punto = PuntoPiedra(celda, casilla, divisiones);
                    float2 eje = normalize(punto - puntoMejor);
                    float distancia = dot((puntoMejor + punto) * 0.5 - local, eje);
                    if (distancia < borde)
                    {
                        borde = distancia;
                        haciaFuera = eje;
                    }
                }
            }

            Pieza pieza = (Pieza)0;
            pieza.id = celda * 16.0 + casillaMejor;
            pieza.interior = borde * tamano;
            pieza.haciaFuera = haciaFuera;
            return pieza;
        }

        float2 GirarCuarto(float2 v, int giro)
        {
            if (giro == 1) return float2(-v.y, v.x);
            if (giro == 2) return -v;
            if (giro == 3) return float2(v.y, -v.x);
            return v;
        }

        // Gradiente xz en mundo de un valor por pixel (para tallar el grid sin conocer su forma).
        float2 GradienteMundo(float valor, float3 posicionWS)
        {
            float3 dpx = ddx(posicionWS);
            float3 dpy = ddy(posicionWS);
            float dvx = ddx(valor);
            float dvy = ddy(valor);
            float det = dpx.x * dpy.z - dpx.z * dpy.x;
            if (abs(det) < 1e-10)
                return 0.0;
            return float2(dvx * dpy.z - dvy * dpx.z, dpx.x * dvy - dpy.x * dvx) / det;
        }

        // Los patrones van en una rama dinamica, asi que no usan ddx/fwidth: reciben las derivadas de w
        // (dwdx, dwdy) y el tamano de un pixel en unidades de mundo calculados fuera de la rama.
        // juntaBorde: la junta que cae sobre el borde de celda (para el modo Juntas); negativa si no hay juntas.
        Suelo SueloPiezas(float2 w, float tamano, int patron, float2 dwdx, float2 dwdy, float pixel, out float2 inclinacion, out float juntaBorde)
        {
            Pieza pieza = (Pieza)0;
            if (patron == 1)
                pieza = PiezaLadrillo(w, tamano);
            else if (patron == 2)
                pieza = PiezaPiedras(w, tamano);
            else if (patron == PATRON_LISO)
            {
                pieza.id = 0.0;
                pieza.interior = 1e3;
                pieza.haciaFuera = 0.0;
            }
            else
                pieza = PiezaLosas(w, tamano);

            // Perfil de la pieza: 0 en el fondo de la junta, 1 arriba.
            float aa = max(pixel, 1e-5);
            float mediaJunta = _Junta * 0.5;
            float bisel = max(_Bisel, aa * 1.5);
            float x = saturate((pieza.interior - mediaJunta) / bisel);
            float altura = x * x * (3.0 - 2.0 * x);
            float pendiente = 6.0 * x * (1.0 - x) / bisel * _Relieve * 0.05;
            float esJunta = 1.0 - smoothstep(mediaJunta - aa, mediaJunta + aa, pieza.interior);

            inclinacion = pieza.haciaFuera * pendiente;

            // Cada pieza toma un trozo distinto (y girado) de la textura, como piedras sueltas.
            float2 azar = GridHash22(pieza.id);
            float2 uv = w / max(_EscalaTextura, 0.01);
            float2 dx = dwdx / max(_EscalaTextura, 0.01);
            float2 dy = dwdy / max(_EscalaTextura, 0.01);
            if (_RotarTextura > 0.5)
            {
                int giro = (int)floor(azar.x * 4.0);
                uv = GirarCuarto(uv, giro);
                dx = GirarCuarto(dx, giro);
                dy = GirarCuarto(dy, giro);
            }
            uv = (uv + azar * 17.0) * _BaseMap_ST.xy + _BaseMap_ST.zw;
            half3 textura = SAMPLE_TEXTURE2D_GRAD(_BaseMap, sampler_BaseMap, uv, dx * _BaseMap_ST.xy, dy * _BaseMap_ST.xy).rgb;
            textura = lerp(half3(1.0, 1.0, 1.0), textura, _MezclaTextura);

            half variacion = 1.0 + (azar.y - 0.5) * _VariacionColor;
            half3 albedoPieza = _BaseColor.rgb * textura * variacion;
            half3 albedoJunta = _ColorJunta.rgb * lerp(half3(1.0, 1.0, 1.0), textura, 0.5);

            Suelo suelo = (Suelo)0;
            suelo.albedo = lerp(albedoPieza, albedoJunta, esJunta);
            suelo.oclusion = lerp(1.0 - _Oclusion, 1.0, altura);
            suelo.suavidad = lerp(_Suavidad * 0.3, _Suavidad * (0.8 + 0.4 * azar.x), altura);

            // Solo las juntas que coinciden con el borde de la celda, subiendo un poco por el bisel.
            float2 f = frac(w / tamano);
            float bordeCelda = min(min(f.x, 1.0 - f.x), min(f.y, 1.0 - f.y)) * tamano;
            juntaBorde = patron == PATRON_LISO ? -1.0
                : (1.0 - altura) * (1.0 - smoothstep(mediaJunta + bisel * 0.5, mediaJunta + bisel + aa, bordeCelda));
            return suelo;
        }

        // Cementerio: cada celda es una parcela de cesped con su propio tono, los bordes son senderos
        // de tierra y grava, y en cada cruce hay un mojon de piedra.
        Suelo SueloParcelas(float2 w, float tamano, float2 dwdx, float2 dwdy, float pixel, out float2 inclinacion, out float juntaBorde)
        {
            float2 celdas = w / tamano;
            float2 celda = floor(celdas);
            float2 f = frac(celdas);
            float2 d = min(f, 1.0 - f) * tamano;
            float2 haciaSenda = d.x < d.y ? float2(f.x < 0.5 ? -1.0 : 1.0, 0.0) : float2(0.0, f.y < 0.5 ? -1.0 : 1.0);

            // Cesped: unas parcelas mas secas o mas oscuras que otras para que cada celda se lea sola.
            float2 escalaCesped = _BaseMap_ST.xy / max(_EscalaTextura, 0.01);
            half3 cesped = SAMPLE_TEXTURE2D_GRAD(_BaseMap, sampler_BaseMap, w * escalaCesped + _BaseMap_ST.zw, dwdx * escalaCesped, dwdy * escalaCesped).rgb;
            cesped = lerp(half3(1.0, 1.0, 1.0), cesped, _MezclaTextura);
            float2 azar = GridHash22(celda);
            cesped *= lerp(_BaseColor.rgb, _ColorSeco.rgb, azar.x * _VariacionParcela);
            cesped *= 1.0 + (azar.y - 0.5) * 0.5 * _VariacionParcela;

            // Borde del sendero: ruido grande para que serpentee y ruido fino que imita briznas que lo invaden.
            float ruido = GridRuido(w * 2.7) * 0.6 + GridRuido(w * 8.3) * 0.4 - 0.5;
            float distancia = min(d.x, d.y) + ruido * _IrregularSenda * _AnchoSenda * 0.8;
            float mitad = _AnchoSenda * 0.5;
            float aa = max(pixel, 1e-4);
            float borde = max(_Bisel, aa);
            float x = saturate((distancia - mitad) / borde);
            float pasto = x * x * (3.0 - 2.0 * x);
            float briznas = (GridRuido(w * 25.0) - 0.5) * 0.9 * _IrregularSenda * (1.0 - abs(pasto * 2.0 - 1.0));
            pasto = saturate(pasto + briznas);
            float senda = 1.0 - pasto;

            // Tierra con grava; en alfa viene la altura de las piedritas.
            float escalaSenda = max(_EscalaSenda, 0.01);
            float2 uvSenda = w / escalaSenda;
            float2 dxSenda = dwdx / escalaSenda;
            float2 dySenda = dwdy / escalaSenda;
            half4 tierra = SAMPLE_TEXTURE2D_GRAD(_TexturaSenda, sampler_TexturaSenda, uvSenda, dxSenda, dySenda);
            float paso = 1.0 / 256.0;
            float altoX = SAMPLE_TEXTURE2D_GRAD(_TexturaSenda, sampler_TexturaSenda, uvSenda + float2(paso, 0.0), dxSenda, dySenda).a;
            float altoZ = SAMPLE_TEXTURE2D_GRAD(_TexturaSenda, sampler_TexturaSenda, uvSenda + float2(0.0, paso), dxSenda, dySenda).a;
            float2 pendienteGrava = float2(altoX - tierra.a, altoZ - tierra.a) / (paso * escalaSenda);
            half3 albedoSenda = tierra.rgb * _ColorSenda.rgb;

            // El sendero esta hundido: el cesped hace un escalon hacia el y la grava sobresale un poco.
            inclinacion = haciaSenda * (6.0 * x * (1.0 - x) / borde * _Relieve * 0.03);
            inclinacion -= pendienteGrava * 0.015 * _Relieve * senda;

            Suelo suelo = (Suelo)0;
            suelo.albedo = lerp(albedoSenda, cesped, pasto);
            suelo.oclusion = lerp(1.0 - _Oclusion * 0.6, 1.0, pasto);
            suelo.suavidad = lerp(0.1 + 0.25 * tierra.a, _Suavidad, pasto);

            // Mojon: losa algo girada y gastada en cada cruce de senderos.
            float2 cruce = round(celdas);
            float2 p = (celdas - cruce) * tamano;
            float2 azarMojon = GridHash22(cruce + 0.7);
            float angulo = (azarMojon.x - 0.5) * 0.6;
            float c = cos(angulo);
            float s = sin(angulo);
            float2 q = float2(c * p.x - s * p.y, s * p.x + c * p.y);
            float tamanoMojon = _TamanoMojon * (0.85 + 0.3 * azarMojon.y);
            float distanciaMojon;
            float2 fueraMojon;
            CajaRedondeada(q, float2(tamanoMojon, tamanoMojon * 0.8) * 0.5, tamanoMojon * 0.18, distanciaMojon, fueraMojon);
            distanciaMojon += (GridRuido(p * 9.0 + cruce) - 0.5) * tamanoMojon * 0.12;
            fueraMojon = float2(c * fueraMojon.x + s * fueraMojon.y, -s * fueraMojon.x + c * fueraMojon.y);

            float aaMojon = max(pixel, 1e-4);
            float biselMojon = max(tamanoMojon * 0.12, aaMojon);
            float cubre = _TamanoMojon > 0.001 ? 1.0 - smoothstep(-aaMojon, aaMojon, distanciaMojon) : 0.0;
            float xm = saturate(-distanciaMojon / biselMojon);
            float2 inclinacionMojon = fueraMojon * (6.0 * xm * (1.0 - xm) / biselMojon * 0.04);
            half3 piedra = SAMPLE_TEXTURE2D_GRAD(_TexturaPiedra, sampler_TexturaPiedra, w * 0.8 + azarMojon * 13.0, dwdx * 0.8, dwdy * 0.8).rgb;
            half3 albedoMojon = piedra * _ColorPiedra.rgb * (0.8 + 0.4 * azarMojon.x);
            // Sombra de contacto alrededor de la losa.
            float contacto = _TamanoMojon > 0.001 ? 1.0 - 0.5 * (1.0 - smoothstep(0.0, biselMojon * 3.0, distanciaMojon)) : 1.0;

            suelo.albedo = lerp(suelo.albedo, albedoMojon, cubre);
            suelo.oclusion = lerp(suelo.oclusion * contacto, 1.0, cubre);
            suelo.suavidad = lerp(suelo.suavidad, _Suavidad * 1.5, cubre);
            inclinacion = lerp(inclinacion, inclinacionMojon, cubre);

            // En modo Juntas jirones de niebla espectral recorren los senderos.
            float niebla = GridRuido(w * 1.3 + _Time.y * float2(0.21, 0.13)) * 0.6 + GridRuido(w * 3.1 - _Time.y * 0.17) * 0.4;
            juntaBorde = senda * (1.0 - cubre) * smoothstep(0.5, 0.95, niebla);
            return suelo;
        }

        Suelo EvaluarSuelo(float3 posicionWS)
        {
            float tamano = max(_GridSize, 0.001);
            float2 w = posicionWS.xz - _GridOrigin.xy;
            int patron = (int)round(_Patron);

            float2 dwdx = ddx(w);
            float2 dwdy = ddy(w);
            float2 huella = abs(dwdx) + abs(dwdy);
            float pixel = max(huella.x, huella.y);

            float2 inclinacion;
            float juntaBorde;
            Suelo suelo;
            UNITY_BRANCH
            if (patron == PATRON_PARCELAS)
                suelo = SueloParcelas(w, tamano, dwdx, dwdy, pixel, inclinacion, juntaBorde);
            else
                suelo = SueloPiezas(w, tamano, patron, dwdx, dwdy, pixel, inclinacion, juntaBorde);

            // Grid
            int mezcla = (int)round(_MezclaGrid);
            float2 celdas = w / tamano;
            float mascara = GridMascara(celdas, _EstiloGrid, _LineThickness, _LargoMarca, _MargenGrid);
            if (mezcla == MEZCLA_JUNTAS && juntaBorde >= 0.0)
                mascara = juntaBorde;
            mascara = GridDesgaste(mascara, celdas, _DesgasteGrid);
            mascara *= GridRevelado(posicionWS, _CentroRevelado, _RadioRevelado, _SuavidadRevelado);

            half fuerza = mascara * _GridColor.a * _IntensidadGrid;
            half fuerzaSuperficie = saturate(fuerza);
            half pulso = lerp(1.0, _BeatPulse, _ReaccionBeat);
            half3 colorSuperficie = saturate(_GridColor.rgb);

            if (mezcla == MEZCLA_BRILLO || mezcla == MEZCLA_JUNTAS)
            {
                suelo.emision = _GridColor.rgb * fuerza * pulso;
            }
            else if (mezcla == MEZCLA_GRABADO)
            {
                suelo.albedo *= 1.0 - fuerzaSuperficie * 0.75;
                suelo.oclusion *= 1.0 - fuerzaSuperficie * 0.6;
                suelo.suavidad *= 1.0 - fuerzaSuperficie;
                inclinacion += GradienteMundo(mascara, posicionWS) * 0.02 * _RelieveGrabado;
                // Un rescoldo en el fondo del surco que solo se nota con el beat.
                suelo.emision = _GridColor.rgb * fuerza * _BeatPulse * _ReaccionBeat * 0.15;
            }
            else if (mezcla == MEZCLA_PINTURA)
            {
                suelo.albedo = lerp(suelo.albedo, colorSuperficie, fuerzaSuperficie);
                suelo.suavidad = lerp(suelo.suavidad, 0.1, fuerzaSuperficie);
                suelo.emision = _GridColor.rgb * fuerzaSuperficie * _BeatPulse * _ReaccionBeat * 0.25;
            }
            else if (mezcla == MEZCLA_TINTE)
            {
                suelo.albedo *= lerp(half3(1.0, 1.0, 1.0), colorSuperficie * 1.5, fuerzaSuperficie);
                suelo.emision = _GridColor.rgb * fuerzaSuperficie * _BeatPulse * _ReaccionBeat * 0.25;
            }

            suelo.normalWS = normalize(float3(inclinacion.x, 1.0, inclinacion.y));
            return suelo;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            // Sin esto las luces de LuzPersonajes (que van por rendering layers) tambien pintarian el suelo.
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Atributos
            {
                float4 positionOS : POSITION;
                float2 lightmapUV : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            #if defined(LIGHTMAP_ON)
                float2 lightmapUV : TEXCOORD1;
            #endif
            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                float4 shadowCoord : TEXCOORD2;
            #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Atributos v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs posicion = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = posicion.positionCS;
                o.positionWS = posicion.positionWS;
            #if defined(LIGHTMAP_ON)
                o.lightmapUV = v.lightmapUV * unity_LightmapST.xy + unity_LightmapST.zw;
            #endif
            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                o.shadowCoord = GetShadowCoord(posicion);
            #endif
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                Suelo suelo = EvaluarSuelo(i.positionWS);

                InputData datos = (InputData)0;
                datos.positionWS = i.positionWS;
                datos.normalWS = suelo.normalWS;
                datos.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                datos.shadowCoord = i.shadowCoord;
            #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                datos.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
            #endif
                datos.fogCoord = ComputeFogFactor(TransformWorldToHClip(i.positionWS).z);
            #if defined(_ADDITIONAL_LIGHTS_VERTEX)
                datos.vertexLighting = VertexLighting(i.positionWS, suelo.normalWS);
            #endif
                datos.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);

            #if defined(LIGHTMAP_ON)
                datos.bakedGI = SampleLightmap(i.lightmapUV, suelo.normalWS);
                datos.shadowMask = SAMPLE_SHADOWMASK(i.lightmapUV);
            #else
                // Sin lightmap propio usamos la luz horneada que SueloGrid copio del suelo original.
                if (_UsarLuzSuelo > 0.5)
                {
                    float2 uvLuz = (i.positionWS.xz - _LuzSueloRect.xy) * _LuzSueloRect.zw;
                    datos.bakedGI = SAMPLE_TEXTURE2D(_LuzSuelo, sampler_LuzSuelo, uvLuz).rgb;
                }
                else
                {
                    datos.bakedGI = SampleSH(suelo.normalWS);
                }
                datos.shadowMask = SAMPLE_SHADOWMASK(0);
            #endif

                SurfaceData superficie = (SurfaceData)0;
                superficie.albedo = suelo.albedo;
                superficie.smoothness = suelo.suavidad;
                superficie.occlusion = suelo.oclusion;
                superficie.emission = suelo.emision;
                superficie.normalTS = half3(0.0, 0.0, 1.0);
                superficie.alpha = 1.0;

                half4 color = UniversalFragmentPBR(datos, superficie);
                color.rgb = MixFog(color.rgb, datos.fogCoord);
                return half4(color.rgb, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex VertProfundidad
            #pragma fragment FragProfundidad
            #pragma multi_compile_instancing

            struct Atributos
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings VertProfundidad(Atributos v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                return o;
            }

            half FragProfundidad(Varyings i) : SV_Target
            {
                return i.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex VertNormales
            #pragma fragment FragNormales
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing

            struct Atributos
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings VertNormales(Atributos v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }

            half4 FragNormales(Varyings i) : SV_Target
            {
                float3 normal = EvaluarSuelo(i.positionWS).normalWS;
            #if defined(_GBUFFER_NORMALS_OCT)
                float2 octaedro = PackNormalOctQuadEncode(normal) * 0.5 + 0.5;
                return half4(PackFloat2To888(octaedro), 0.0);
            #else
                return half4(normal, 0.0);
            #endif
            }
            ENDHLSL
        }

        Pass
        {
            Name "Meta"
            Tags { "LightMode" = "Meta" }
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex VertMeta
            #pragma fragment FragMeta

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"

            struct Atributos
            {
                float4 positionOS : POSITION;
                float2 uv1 : TEXCOORD1;
                float2 uv2 : TEXCOORD2;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings VertMeta(Atributos v)
            {
                Varyings o;
                o.positionCS = UnityMetaVertexPosition(v.positionOS.xyz, v.uv1, v.uv2);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                return o;
            }

            half4 FragMeta(Varyings i) : SV_Target
            {
                Suelo suelo = EvaluarSuelo(i.positionWS);
                UnityMetaInput meta = (UnityMetaInput)0;
                meta.Albedo = suelo.albedo;
                meta.Emission = suelo.emision;
                return UnityMetaFragment(meta);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
