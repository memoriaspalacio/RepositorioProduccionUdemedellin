using UnityEngine;

/// <summary>
/// Monjas y Monk: el cuerpo se carboniza con brasas, arde y se consume de abajo hacia arriba
/// soltando llamas, chispas, humo y ceniza. Las monjas quedan negras y sueltan ceniza oscura;
/// el Monk se calcina en blanco y suelta ceniza blanca.
/// </summary>
public class MuerteArder : MuerteBase
{
    const float DuracionCarbonizado = 0.9f;
    const float InicioDisolucion = 0.3f;
    const float DuracionDisolucion = 1.8f;
    const float FinFuegoEnSuelo = 2.7f;
    const float AnchoBorde = 0.07f;
    const float DuracionDestello = 0.25f;

    static readonly Color ColorFuego = new Color(1f, 0.55f, 0.22f);

    /// <summary>Ceniza blanca y cuerpo calcinado en blanco (Monk) en vez de negro (monjas).</summary>
    public bool cenizaBlanca;

    ParticleSystem llamas;
    ParticleSystem chispas;
    ParticleSystem ceniza;
    ParticleSystem humo;
    Light luz;
    float intensidadLuz;
    float semillaLuz;

    float acumuladoLlamas;
    float acumuladoChispas;
    float acumuladoCeniza;
    float acumuladoHumo;

    Color colorHumo;
    float probabilidadCeniza;

    float FinDisolucion => InicioDisolucion + DuracionDisolucion;
    protected override float Duracion => FinFuegoEnSuelo + 0.3f;

    protected override void AlIniciar()
    {
        Color borde = new Color(1f, 0.42f, 0.08f) * 4.5f;
        borde.a = 1f;
        cuerpo.ConfigurarDisolucion(0.4f, false, 3.5f / escala, AnchoBorde, borde);
        cuerpo.Fijar(CuerpoMuerte.IdAnchoQuemado, 0.3f);
        cuerpo.Fijar(CuerpoMuerte.IdColorQuemado, cenizaBlanca ? new Color(0.78f, 0.76f, 0.72f) : new Color(0.045f, 0.035f, 0.03f));
        cuerpo.Fijar(CuerpoMuerte.IdColorBrasas, new Color(1f, 0.38f, 0.08f) * 3.5f);
        cuerpo.Fijar(CuerpoMuerte.IdColorDestello, new Color(1f, 0.5f, 0.15f));

        colorHumo = cenizaBlanca ? new Color(0.5f, 0.48f, 0.46f, 0.35f) : new Color(0.1f, 0.09f, 0.085f, 0.45f);
        probabilidadCeniza = cenizaBlanca ? 0.75f : 0.5f;

        CrearLlamas();
        CrearChispas();
        CrearCeniza();
        CrearHumo();
        CrearLuz();
    }

    #region Sistemas

    void CrearLlamas()
    {
        llamas = CrearSistema("Llamas", RecursosMuerte.Particula("Llama", RecursosMuerte.Llama, RecursosMuerte.Mezcla.Aditiva, new Color(1.5f, 1.5f, 1.5f, 1f)), 900);
        Gravedad(llamas, -0.35f);
        Ruido(llamas, 0.5f, 2.2f, 1.2f);
        TamanoEnVida(llamas, RecursosMuerte.Curva(0f, 0.5f, 0.25f, 1f, 1f, 0.1f));
        ColorEnVida(llamas, RecursosMuerte.Degradado(
            new[] { new Color(1f, 0.92f, 0.7f), new Color(1f, 0.62f, 0.18f), new Color(0.85f, 0.22f, 0.05f), new Color(0.3f, 0.06f, 0.02f) },
            new[] { 0f, 0.3f, 0.65f, 1f },
            new[] { 0f, 1f, 0.85f, 0f },
            new[] { 0f, 0.12f, 0.55f, 1f }));
    }

    void CrearChispas()
    {
        chispas = CrearSistema("Chispas", RecursosMuerte.Particula("Chispa", RecursosMuerte.Punto, RecursosMuerte.Mezcla.Aditiva, new Color(3f, 3f, 3f, 1f)), 800);
        Gravedad(chispas, -0.12f);
        Ruido(chispas, 0.9f, 1.2f, 0.8f);
        TamanoEnVida(chispas, RecursosMuerte.Curva(0f, 1f, 1f, 0.25f));
        ColorEnVida(chispas, RecursosMuerte.Degradado(
            new[] { new Color(1f, 0.85f, 0.55f), new Color(1f, 0.45f, 0.12f), new Color(0.7f, 0.12f, 0.03f) },
            new[] { 0f, 0.4f, 1f },
            new[] { 1f, 1f, 0f },
            new[] { 0f, 0.75f, 1f }));
    }

    void CrearCeniza()
    {
        ceniza = CrearSistema("Ceniza", RecursosMuerte.Particula("Ceniza", RecursosMuerte.Escama, RecursosMuerte.Mezcla.Alfa, Color.white), 3000);
        ParticleSystem.MainModule principal = ceniza.main;
        principal.startSize3D = true;
        Gravedad(ceniza, 0.05f);
        Ruido(ceniza, 0.45f, 0.9f, 0.4f);
        Arrastre(ceniza, 1.5f);
        TamanoEnVida(ceniza, RecursosMuerte.Curva(0f, 1f, 1f, 0.7f));
        // Al salir del borde encendido la ceniza todavia lleva un tinte naranja.
        ColorEnVida(ceniza, RecursosMuerte.Degradado(
            new[] { new Color(1f, 0.6f, 0.3f), Color.white },
            new[] { 0f, 0.2f },
            new[] { 0f, 1f, 1f, 0f },
            new[] { 0f, 0.06f, 0.7f, 1f }));
    }

    void CrearHumo()
    {
        humo = CrearSistema("Humo", RecursosMuerte.Particula("Humo", RecursosMuerte.Humo, RecursosMuerte.Mezcla.Alfa, Color.white), 300);
        Gravedad(humo, -0.03f);
        Ruido(humo, 0.25f, 0.5f, 0.3f);
        TamanoEnVida(humo, RecursosMuerte.Curva(0f, 0.45f, 1f, 1.6f));
        ColorEnVida(humo, RecursosMuerte.SoloAlfa(0f, 0f, 0.15f, 1f, 1f, 0f));
    }

    void CrearLuz()
    {
        GameObject objeto = new GameObject("Luz del fuego");
        objeto.transform.SetParent(transform, false);
        objeto.transform.position = centro;

        luz = objeto.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.color = ColorFuego;
        luz.range = 3.5f * escala;
        luz.intensity = 0f;
        luz.shadows = LightShadows.None;
        luz.renderMode = LightRenderMode.ForcePixel;

        intensidadLuz = 2.4f;
        semillaLuz = Random.value * 100f;
    }

    #endregion

    protected override void Actualizar(float dt)
    {
        cuerpo.Fijar(CuerpoMuerte.IdDestello, Mathf.Clamp01(1f - tiempo / DuracionDestello) * 0.5f);

        float carbonizado = Mathf.Clamp01(tiempo / DuracionCarbonizado);
        cuerpo.Fijar(CuerpoMuerte.IdQuemado, 1f - (1f - carbonizado) * (1f - carbonizado));
        cuerpo.Fijar(CuerpoMuerte.IdBrasas, Mathf.Clamp01(tiempo / 0.6f) * 2f);

        float progreso = Progreso(InicioDisolucion, DuracionDisolucion, AnchoBorde);
        cuerpo.Fijar(CuerpoMuerte.IdProgreso, progreso);
        LiberarHasta(progreso);

        if (tiempo >= FinDisolucion)
            cuerpo.Mostrar(false);

        // Fuerza del fuego: sube rapido, se mantiene mientras hay cuerpo y se apaga en el suelo.
        float fuego = Mathf.Clamp01(tiempo / 0.15f);
        if (tiempo > FinDisolucion)
            fuego *= 1f - Mathf.Clamp01((tiempo - FinDisolucion) / (FinFuegoEnSuelo - FinDisolucion));

        EmitirLlamas(fuego, dt);
        EmitirHumo(fuego, dt);
        ActualizarLuz(fuego);
    }

    void EmitirLlamas(float fuego, float dt)
    {
        int cuantas = Cuantas(ref acumuladoLlamas, 140f * cantidad * fuego, dt);
        for (int i = 0; i < cuantas; i++)
        {
            Vector3 posicion;
            Vector3 normal;
            float tamano = Azar(0.16f, 0.34f) * escala;

            if (QuedanMuestrasVivas)
            {
                CuerpoMuerte.Muestra muestra = MuestraViva(2f);
                posicion = muestra.posicion + muestra.normal * (0.04f * escala);
                normal = muestra.normal;
            }
            else
            {
                posicion = PuntoEnElSuelo(0.3f);
                normal = Vector3.up;
                tamano *= 0.6f;
            }

            Emitir(llamas,
                posicion,
                (Vector3.up * Azar(0.3f, 0.8f) + normal * 0.12f) * escala,
                tamano,
                Azar(0.35f, 0.75f),
                Color.white,
                Azar(-12f, 12f));
        }

        // Chispas sueltas del fuego, ademas de las que saltan del borde al consumirse.
        int sueltas = Cuantas(ref acumuladoChispas, 30f * cantidad * fuego, dt);
        for (int i = 0; i < sueltas; i++)
        {
            Vector3 posicion = QuedanMuestrasVivas ? MuestraViva(2f).posicion : PuntoEnElSuelo(0.3f);
            EmitirChispa(posicion);
        }

        // Ceniza que se desprende del cuerpo ya carbonizado antes de que llegue el borde.
        if (tiempo > 0.4f && QuedanMuestrasVivas)
        {
            int copos = Cuantas(ref acumuladoCeniza, 20f * cantidad, dt);
            for (int i = 0; i < copos; i++)
            {
                CuerpoMuerte.Muestra muestra = MuestraViva();
                EmitirCeniza(muestra.posicion, muestra.normal);
            }
        }
    }

    void EmitirHumo(float fuego, float dt)
    {
        if (tiempo < 0.1f)
            return;

        int cuantas = Cuantas(ref acumuladoHumo, 16f * cantidad * fuego, dt);
        for (int i = 0; i < cuantas; i++)
        {
            Vector3 posicion = QuedanMuestrasVivas ? MuestraViva().posicion : PuntoEnElSuelo(0.3f);
            posicion += Vector3.up * (0.25f * escala);

            Emitir(humo,
                posicion,
                (Vector3.up * Azar(0.35f, 0.7f) + HorizontalAlAzar() * 0.1f) * escala,
                Azar(0.45f, 0.8f) * escala,
                Azar(1.8f, 2.8f),
                colorHumo,
                Azar(0f, 360f),
                Azar(-25f, 25f));
        }
    }

    void ActualizarLuz(float fuego)
    {
        if (luz == null)
            return;

        float parpadeo = 0.75f + 0.25f * Mathf.PerlinNoise(tiempo * 9f, semillaLuz);
        luz.intensity = intensidadLuz * fuego * parpadeo;

        // La luz baja con el fuego a medida que el cuerpo se consume.
        float bajada = Mathf.Clamp01((tiempo - InicioDisolucion) / DuracionDisolucion);
        luz.transform.position = Vector3.Lerp(centro, new Vector3(centro.x, suelo + 0.3f * escala, centro.z), bajada * 0.6f);
    }

    protected override void AlLiberar(CuerpoMuerte.Muestra muestra)
    {
        if (Random.value < probabilidadCeniza)
            EmitirCeniza(muestra.posicion, muestra.normal);

        if (Random.value < 0.3f)
            EmitirChispa(muestra.posicion);
    }

    void EmitirChispa(Vector3 posicion)
    {
        Emitir(chispas,
            posicion,
            (Vector3.up * Azar(0.6f, 1.6f) + Random.insideUnitSphere * 0.5f) * escala,
            Azar(0.015f, 0.035f) * escala,
            Azar(0.8f, 1.8f),
            Color.white);
    }

    void EmitirCeniza(Vector3 posicion, Vector3 normal)
    {
        float tono = cenizaBlanca ? Azar(0.8f, 0.97f) : Azar(0.05f, 0.16f);
        Color color = cenizaBlanca
            ? new Color(tono, tono * 0.99f, tono * 0.96f, 1f)
            : new Color(tono * 1.1f, tono, tono * 0.95f, 1f);

        float ancho = Azar(0.02f, 0.05f) * escala;
        ParticleSystem.EmitParams parametros = new ParticleSystem.EmitParams
        {
            position = posicion,
            velocity = (Vector3.up * Azar(0.4f, 1.1f) + normal * 0.2f + HorizontalAlAzar() * 0.25f) * escala,
            startSize3D = new Vector3(ancho, ancho * Azar(0.55f, 1f), ancho),
            startLifetime = Azar(1.6f, 2.8f),
            startColor = color,
            rotation = Azar(0f, 360f),
            angularVelocity = Azar(-200f, 200f)
        };
        ceniza.Emit(parametros, 1);
    }

    Vector3 PuntoEnElSuelo(float radio)
    {
        return new Vector3(centro.x, suelo + 0.05f * escala, centro.z) + HorizontalAlAzar() * (radio * escala);
    }
}
