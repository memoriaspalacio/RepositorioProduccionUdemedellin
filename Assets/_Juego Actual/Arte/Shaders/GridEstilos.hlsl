#ifndef UDEM_GRID_ESTILOS_INCLUDED
#define UDEM_GRID_ESTILOS_INCLUDED

// Estilos de grid de UdeM/Suelo/SueloGrid (el grid va dentro del suelo).
// Todo trabaja en "unidades de celda": la celda (i, j) del MeshGrid ocupa [i, i+1) x [j, j+1).

#ifndef PI
#define PI 3.14159265359
#endif

#define GRID_LINEAS 0
#define GRID_DISCONTINUAS 1
#define GRID_CRUCES 2
#define GRID_ESQUINAS 3
#define GRID_PUNTOS 4
#define GRID_MARCO 5
#define GRID_RESPLANDOR 6
#define GRID_TABLERO 7

float2 GridCeldas(float3 posicionWS, float4 origen, float tamanoCelda)
{
    return (posicionWS.xz - origen.xy) / max(tamanoCelda, 0.001);
}

float GridHash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float2 GridHash22(float2 p)
{
    float n = GridHash21(p);
    return float2(n, GridHash21(p + n * 17.17 + 3.1));
}

float GridRuido(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    float a = GridHash21(i);
    float b = GridHash21(i + float2(1.0, 0.0));
    float c = GridHash21(i + float2(0.0, 1.0));
    float d = GridHash21(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

// 1 dentro de [0, ancho] y 0 fuera, con el borde suavizado en "aa".
float GridBanda(float distancia, float ancho, float aa)
{
    return 1.0 - smoothstep(ancho, ancho + aa, distancia);
}

float2 GridBanda(float2 distancia, float ancho, float aa)
{
    return 1.0 - smoothstep(ancho, ancho + aa, distancia);
}

// Tramos centrados en los enteros de "s" que cubren la fraccion "largo".
float GridTramos(float s, float largo)
{
    float aa = max(fwidth(s), 1e-5);
    float v = abs(frac(s + 0.5) - 0.5);
    return 1.0 - smoothstep(largo * 0.5 - aa, largo * 0.5 + aa, v);
}

float GridCajaRedondeada(float2 p, float2 mitad, float radio)
{
    float2 q = abs(p) - mitad + radio;
    return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - radio;
}

// Forma del grid en una celda: 1 donde se dibuja, 0 donde no.
// grosor: ancho de linea (fraccion de celda). largo: largo de marcas, guiones y tamano de puntos. margen: separacion del borde.
float GridMascara(float2 celdas, float estilo, float grosor, float largo, float margen)
{
    float2 fw = max(fwidth(celdas), 1e-5);
    float aa = max(fw.x, fw.y);
    float2 f = frac(celdas);
    float2 d = min(f, 1.0 - f);
    float medio = grosor * 0.5;
    int e = (int)round(estilo);

    // Mismo trazo que tenia FloorGrid, para que los materiales viejos no cambien.
    float2 trazo = smoothstep(fw, fw + medio, d);
    float lineaV = 1.0 - trazo.x;
    float lineaH = 1.0 - trazo.y;
    float mascara;

    if (e == GRID_DISCONTINUAS)
    {
        mascara = max(lineaV * GridTramos(celdas.y * 4.0, largo * 2.0), lineaH * GridTramos(celdas.x * 4.0, largo * 2.0));
    }
    else if (e == GRID_CRUCES)
    {
        mascara = max(lineaV * GridBanda(d.y, largo, aa), lineaH * GridBanda(d.x, largo, aa));
    }
    else if (e == GRID_ESQUINAS)
    {
        float2 haciaDentro = smoothstep(margen - medio - aa, margen - medio, d);
        float2 cerca = GridBanda(d, margen + largo, aa);
        float2 linea = GridBanda(abs(d - margen), medio, aa);
        mascara = max(linea.x * haciaDentro.y * cerca.y, linea.y * haciaDentro.x * cerca.x);
    }
    else if (e == GRID_PUNTOS)
    {
        mascara = GridBanda(length(d), largo * 0.5, aa);
    }
    else if (e == GRID_MARCO)
    {
        float mitad = max(0.5 - margen, 0.01);
        float distancia = GridCajaRedondeada(f - 0.5, mitad.xx, min(largo, mitad));
        mascara = GridBanda(abs(distancia), medio, aa);
    }
    else if (e == GRID_RESPLANDOR)
    {
        float borde = min(d.x, d.y) / max(margen, 0.01);
        mascara = pow(saturate(1.0 - borde), 2.0);
    }
    else if (e == GRID_TABLERO)
    {
        // Relleno de celdas alternas, tenue de serie para no tapar el suelo.
        float2 s = sin(celdas * PI);
        float t = s.x * s.y;
        mascara = saturate(0.5 - t / max(fwidth(t) * 1.5, 1e-5)) * 0.35;
    }
    else
    {
        mascara = 1.0 - min(trazo.x, trazo.y);
    }

    // Cuando una celda ocupa pocos pixeles las lineas solo hacen moire.
    return mascara * (1.0 - smoothstep(0.2, 0.45, aa));
}

// Rompe el trazo con ruido para que parezca tiza, pintura vieja o piedra gastada.
float GridDesgaste(float mascara, float2 celdas, float desgaste)
{
    if (desgaste <= 0.0)
        return mascara;
    float ruido = GridRuido(celdas * 7.0) * 0.65 + GridRuido(celdas * 23.0) * 0.35;
    return mascara * smoothstep(desgaste - 0.12, desgaste + 0.12, ruido);
}

// Muestra el grid solo alrededor de un punto (lo mueve GridBeatVisualizer). Radio 0 = desactivado.
float GridRevelado(float3 posicionWS, float4 centro, float radio, float suavidad)
{
    if (radio <= 0.0)
        return 1.0;
    float distancia = length(posicionWS.xz - centro.xz);
    return 1.0 - smoothstep(radio - max(suavidad, 0.001), radio, distancia);
}

#endif
