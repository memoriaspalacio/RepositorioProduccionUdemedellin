// Funciones compartidas por los efectos del sacerdote (infierno, invocacion, enlace y escudo divino).
#ifndef EFECTOS_SACERDOTE_INCLUDED
#define EFECTOS_SACERDOTE_INCLUDED

float HashSacerdote(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

// Ruido de valor suave entre 0 y 1.
float RuidoSacerdote(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float a = HashSacerdote(i);
    float b = HashSacerdote(i + float2(1, 0));
    float c = HashSacerdote(i + float2(0, 1));
    float d = HashSacerdote(i + float2(1, 1));
    float2 u = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

// Tres octavas de ruido: llamas, grietas y humo.
float FbmSacerdote(float2 p)
{
    return RuidoSacerdote(p) * 0.55
         + RuidoSacerdote(p * 2.1 + 3.7) * 0.3
         + RuidoSacerdote(p * 4.3 + 1.3) * 0.15;
}

// Vetas brillantes donde el ruido cruza 0.5 (grietas de brasa).
float VetasSacerdote(float2 p, float grosor)
{
    float n = FbmSacerdote(p);
    return saturate(1.0 - abs(n - 0.5) / grosor);
}

#endif
