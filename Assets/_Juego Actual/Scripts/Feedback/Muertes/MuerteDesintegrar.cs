using UnityEngine;

/// <summary>
/// Esqueleto: el cuerpo se deshace de arriba hacia abajo en polvo de sus propios colores.
/// Cada mota nace del punto exacto que desaparece, con el color de la textura en ese punto,
/// y el viento la arrastra lejos del atacante.
/// </summary>
public class MuerteDesintegrar : MuerteBase
{
    const float Espera = 0.12f;
    const float DuracionDisolucion = 1.4f;
    const float AnchoBorde = 0.05f;
    const float DuracionDestello = 0.22f;
    const int CadaCuantasNube = 7;

    ParticleSystem polvo;
    ParticleSystem nube;
    Color colorNube;
    int liberadas;

    protected override float Duracion => Espera + DuracionDisolucion + 0.1f;

    protected override void AlIniciar()
    {
        Color promedio = cuerpo.ColorPromedio;
        Color borde = Color.Lerp(promedio, Color.white, 0.4f) * 2.2f;
        borde.a = 1f;

        cuerpo.ConfigurarDisolucion(0.72f, true, 4.5f / escala, AnchoBorde, borde);
        cuerpo.Fijar(CuerpoMuerte.IdColorDestello, Color.Lerp(promedio, Color.white, 0.6f));

        colorNube = promedio * 0.9f;
        colorNube.a = 0.28f;

        polvo = CrearSistema("Polvo", RecursosMuerte.Particula("Polvo", RecursosMuerte.Punto, RecursosMuerte.Mezcla.Alfa, Color.white), 4000);
        Gravedad(polvo, -0.04f);
        Ruido(polvo, 0.55f, 1.4f, 0.5f);
        Arrastre(polvo, 0.7f);
        TamanoEnVida(polvo, RecursosMuerte.Curva(0f, 1f, 0.6f, 0.85f, 1f, 0f));
        ColorEnVida(polvo, RecursosMuerte.SoloAlfa(0f, 1f, 0.55f, 1f, 1f, 0f));

        nube = CrearSistema("Nube de polvo", RecursosMuerte.Particula("Nube", RecursosMuerte.Humo, RecursosMuerte.Mezcla.Alfa, Color.white), 600);
        Gravedad(nube, -0.02f);
        Ruido(nube, 0.2f, 0.6f, 0.3f);
        Arrastre(nube, 0.9f);
        TamanoEnVida(nube, RecursosMuerte.Curva(0f, 0.6f, 1f, 1.5f));
        ColorEnVida(nube, RecursosMuerte.SoloAlfa(0f, 0f, 0.15f, 1f, 1f, 0f));
    }

    protected override void Actualizar(float dt)
    {
        cuerpo.Fijar(CuerpoMuerte.IdDestello, Mathf.Clamp01(1f - tiempo / DuracionDestello) * 0.45f);

        float progreso = Progreso(Espera, DuracionDisolucion, AnchoBorde);
        cuerpo.Fijar(CuerpoMuerte.IdProgreso, progreso);
        LiberarHasta(progreso);

        if (tiempo >= Espera + DuracionDisolucion)
            cuerpo.Mostrar(false);
    }

    protected override void AlLiberar(CuerpoMuerte.Muestra muestra)
    {
        Vector3 viento = direccionGolpe * Azar(0.35f, 1f) + Vector3.up * Azar(0.25f, 0.6f);
        Vector3 impulso = (muestra.normal * Azar(0.08f, 0.3f) + viento) * escala;

        Emitir(polvo,
            muestra.posicion + muestra.normal * (0.01f * escala),
            impulso,
            Azar(0.022f, 0.05f) * escala,
            Azar(0.9f, 1.7f),
            muestra.color,
            Azar(0f, 360f));

        if (++liberadas % CadaCuantasNube != 0)
            return;

        Emitir(nube,
            muestra.posicion,
            (direccionGolpe * Azar(0.2f, 0.5f) + Vector3.up * Azar(0.15f, 0.35f)) * escala,
            Azar(0.25f, 0.45f) * escala,
            Azar(1.2f, 2f),
            colorNube,
            Azar(0f, 360f),
            Azar(-30f, 30f));
    }
}
