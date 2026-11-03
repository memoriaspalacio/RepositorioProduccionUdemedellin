using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Angel de piedra: la estatua se agrieta desde el punto del golpe mientras tiembla y luego
/// revienta en rocas con su propio material que rebotan en el suelo, grava y polvo de piedra.
/// </summary>
public class MuerteRomperse : MuerteBase
{
    const float DuracionGrietas = 0.3f;
    const float AmplitudTemblor = 0.03f;

    ParticleSystem rocas;
    ParticleSystem grava;
    ParticleSystem polvo;
    ParticleSystem esquirlas;
    Transform planoSuelo;
    Material materialRocasPropio;
    ParticleSystem.Particle[] bufferRocas;
    ParticleSystem.Particle[] bufferGrava;

    Vector3 impacto;
    float alcanceMaximo;
    bool roto;
    float acumuladoGrava;
    float semillaTemblor;
    Color colorPolvo;

    protected override float Duracion => DuracionGrietas + 0.2f;

    protected override void AlIniciar()
    {
        semillaTemblor = Random.value * 100f;
        colorPolvo = Color.Lerp(cuerpo.ColorPromedio, new Color(0.55f, 0.53f, 0.5f), 0.3f);
        colorPolvo.a = 0.5f;

        BuscarPuntoDeImpacto();

        cuerpo.Fijar(CuerpoMuerte.IdEscalaGrietas, 3f / escala);
        cuerpo.Fijar(CuerpoMuerte.IdAnchoGrieta, 0.08f);
        cuerpo.Fijar(CuerpoMuerte.IdColorGrietas, new Color(0.06f, 0.055f, 0.05f));
        cuerpo.Fijar(CuerpoMuerte.IdBrilloGrietas, new Color(0.5f, 0.6f, 0.75f) * 0.6f);

        GameObject objetoSuelo = new GameObject("Suelo");
        planoSuelo = objetoSuelo.transform;
        planoSuelo.SetParent(transform, false);
        planoSuelo.position = new Vector3(centro.x, suelo, centro.z);

        Material materialRocas = MaterialDeRocas();
        rocas = CrearSistemaDeRocas("Rocas", materialRocas, 200);
        grava = CrearSistemaDeRocas("Grava", materialRocas, 300);
        bufferRocas = new ParticleSystem.Particle[200];
        bufferGrava = new ParticleSystem.Particle[300];

        polvo = CrearSistema("Polvo de piedra", RecursosMuerte.Particula("Polvo de piedra", RecursosMuerte.Humo, RecursosMuerte.Mezcla.Alfa, Color.white), 200);
        Gravedad(polvo, -0.02f);
        Ruido(polvo, 0.2f, 0.5f, 0.3f);
        Arrastre(polvo, 2.2f);
        TamanoEnVida(polvo, RecursosMuerte.Curva(0f, 0.7f, 1f, 1.8f));
        ColorEnVida(polvo, RecursosMuerte.SoloAlfa(0f, 0f, 0.08f, 1f, 1f, 0f));

        esquirlas = CrearSistema("Esquirlas", RecursosMuerte.Particula("Polvo", RecursosMuerte.Punto, RecursosMuerte.Mezcla.Alfa, Color.white), 300);
        Gravedad(esquirlas, 1f);
        Arrastre(esquirlas, 0.6f);
        TamanoEnVida(esquirlas, RecursosMuerte.Curva(0f, 1f, 0.7f, 0.8f, 1f, 0f));
        ColorEnVida(esquirlas, RecursosMuerte.SoloAlfa(0f, 1f, 0.7f, 1f, 1f, 0f));
    }

    // Las grietas nacen en la cara que recibio el golpe, a la altura del pecho.
    void BuscarPuntoDeImpacto()
    {
        impacto = centro - direccionGolpe * (cuerpo.Limites.extents.magnitude * 0.3f);
        alcanceMaximo = cuerpo.Limites.size.magnitude;

        CuerpoMuerte.Muestra[] muestras = cuerpo.Muestras;
        if (muestras.Length == 0)
            return;

        float alturaPecho = centro.y + cuerpo.Limites.extents.y * 0.3f;
        float mejor = float.MinValue;
        for (int i = 0; i < muestras.Length; i++)
        {
            Vector3 p = muestras[i].posicion;
            float puntaje = Vector3.Dot(p - centro, -direccionGolpe) - Mathf.Abs(p.y - alturaPecho) * 0.5f;
            if (puntaje > mejor)
            {
                mejor = puntaje;
                impacto = p;
            }
        }

        alcanceMaximo = 0f;
        for (int i = 0; i < muestras.Length; i++)
            alcanceMaximo = Mathf.Max(alcanceMaximo, Vector3.Distance(impacto, muestras[i].posicion));
        alcanceMaximo += 0.1f * escala;
    }

    Material MaterialDeRocas()
    {
        if (cuerpo.MaterialPrincipal != null)
            return cuerpo.MaterialPrincipal;

        // Sin material original se pintan con el color promedio de la estatua.
        materialRocasPropio = RecursosMuerte.NuevoMaterialCuerpo();
        if (materialRocasPropio != null)
            materialRocasPropio.SetColor(Shader.PropertyToID("_BaseColor"), cuerpo.ColorPromedio);
        return materialRocasPropio;
    }

    ParticleSystem CrearSistemaDeRocas(string nombre, Material material, int maximo)
    {
        ParticleSystem sistema = CrearSistema(nombre, material, maximo);
        sistema.gameObject.layer = cuerpo.Capa;

        ParticleSystem.MainModule principal = sistema.main;
        principal.startRotation3D = true;
        principal.gravityModifier = 1f;

        ParticleSystemRenderer render = sistema.GetComponent<ParticleSystemRenderer>();
        render.renderMode = ParticleSystemRenderMode.Mesh;
        render.SetMeshes(RecursosMuerte.MallasRoca);
        render.meshDistribution = ParticleSystemMeshDistribution.UniformRandom;
        render.enableGPUInstancing = false;
        render.alignment = ParticleSystemRenderSpace.World;
        render.sortMode = ParticleSystemSortMode.None;
        render.shadowCastingMode = ShadowCastingMode.On;
        render.receiveShadows = true;
        render.lightProbeUsage = LightProbeUsage.BlendProbes;
        render.renderingLayerMask = cuerpo.CapasDeLuz;

        ParticleSystem.CollisionModule choque = sistema.collision;
        choque.enabled = true;
        choque.type = ParticleSystemCollisionType.Planes;
        choque.SetPlane(0, planoSuelo);
        choque.bounce = new ParticleSystem.MinMaxCurve(0.25f, 0.4f);
        choque.dampen = new ParticleSystem.MinMaxCurve(0.3f);
        choque.lifetimeLoss = new ParticleSystem.MinMaxCurve(0f);
        choque.radiusScale = 0.8f;

        // Las rocas se encogen al final de su vida en vez de desaparecer de golpe.
        TamanoEnVida(sistema, RecursosMuerte.Curva(0f, 1f, 0.85f, 1f, 1f, 0f));
        return sistema;
    }

    protected override void Actualizar(float dt)
    {
        if (!roto)
        {
            float t = Mathf.Clamp01(tiempo / DuracionGrietas);
            float alcance = alcanceMaximo * Mathf.Pow(t, 1.5f);
            cuerpo.Fijar(CuerpoMuerte.IdPuntoImpacto, new Vector4(impacto.x, impacto.y, impacto.z, Mathf.Max(0.0002f, alcance)));

            float amplitud = AmplitudTemblor * escala * t;
            float fase = tiempo * 45f;
            cuerpo.Desplazar(new Vector3(
                (Mathf.PerlinNoise(fase, semillaTemblor) - 0.5f) * 2f * amplitud,
                (Mathf.PerlinNoise(semillaTemblor, fase) - 0.5f) * amplitud,
                (Mathf.PerlinNoise(fase + 17f, semillaTemblor + 3f) - 0.5f) * 2f * amplitud));

            // Mientras se agrieta se le caen pedacitos.
            int pedacitos = Cuantas(ref acumuladoGrava, 40f * cantidad * t, dt);
            for (int i = 0; i < pedacitos && cuerpo.Muestras.Length > 0; i++)
                EmitirRoca(grava, MuestraCualquiera().posicion, Vector3.down * 0.2f + HorizontalAlAzar() * 0.4f, Azar(0.025f, 0.05f), Azar(1.2f, 2f));

            if (tiempo >= DuracionGrietas)
                Romper();

            return;
        }

        AsentarEnElSuelo(rocas, bufferRocas, dt);
        AsentarEnElSuelo(grava, bufferGrava, dt);
    }

    void Romper()
    {
        roto = true;
        cuerpo.Desplazar(Vector3.zero);
        cuerpo.Mostrar(false);

        if (cuerpo.Muestras.Length == 0)
            return;

        int cantidadRocas = Mathf.RoundToInt(26 * cantidad);
        for (int i = 0; i < cantidadRocas; i++)
        {
            Vector3 origen = MuestraCualquiera().posicion;
            Vector3 afuera = Afuera(origen);
            Vector3 impulso = afuera * Azar(1f, 2.6f) + Vector3.up * Azar(0.8f, 2f) + direccionGolpe * Azar(0.4f, 1.4f);
            EmitirRoca(rocas, Vector3.Lerp(origen, centro, 0.2f), impulso, Azar(0.12f, 0.3f), Azar(3.2f, 4.2f));
        }

        int cantidadGrava = Mathf.RoundToInt(50 * cantidad);
        for (int i = 0; i < cantidadGrava; i++)
        {
            Vector3 origen = MuestraCualquiera().posicion;
            Vector3 impulso = Afuera(origen) * Azar(1.5f, 3.8f) + Vector3.up * Azar(0.5f, 2f) + direccionGolpe * Azar(0.2f, 1f);
            EmitirRoca(grava, origen, impulso, Azar(0.035f, 0.08f), Azar(1.8f, 2.8f));
        }

        int cantidadPolvo = Mathf.RoundToInt(20 * cantidad);
        for (int i = 0; i < cantidadPolvo; i++)
        {
            Vector3 origen = MuestraCualquiera().posicion;
            Emitir(polvo,
                origen,
                (Afuera(origen) * Azar(0.3f, 1.2f) + Vector3.up * 0.25f) * escala,
                Azar(0.5f, 0.9f) * escala,
                Azar(1.3f, 2.3f),
                colorPolvo,
                Azar(0f, 360f),
                Azar(-30f, 30f));
        }

        // Anillo de polvo que barre el suelo al caer los trozos.
        const int Anillo = 14;
        Color colorAnillo = colorPolvo;
        colorAnillo.a = 0.45f;
        for (int i = 0; i < Anillo; i++)
        {
            float angulo = (i + Random.value * 0.6f) / Anillo * Mathf.PI * 2f;
            Vector3 direccion = new Vector3(Mathf.Cos(angulo), 0f, Mathf.Sin(angulo));
            Emitir(polvo,
                new Vector3(centro.x, suelo + 0.08f * escala, centro.z) + direccion * (0.25f * escala),
                direccion * (Azar(1f, 2f) * escala),
                Azar(0.35f, 0.6f) * escala,
                Azar(0.9f, 1.5f),
                colorAnillo,
                Azar(0f, 360f),
                Azar(-30f, 30f));
        }

        int cantidadEsquirlas = Mathf.RoundToInt(70 * cantidad);
        for (int i = 0; i < cantidadEsquirlas; i++)
        {
            CuerpoMuerte.Muestra muestra = MuestraCualquiera();
            Vector3 impulso = Random.onUnitSphere * Azar(1.5f, 4f) + Vector3.up * Azar(0.5f, 1.5f);
            Emitir(esquirlas, muestra.posicion, impulso * escala, Azar(0.015f, 0.035f) * escala, Azar(0.5f, 1.1f), muestra.color);
        }
    }

    // Direccion horizontal desde el centro de la estatua hacia el punto, algo inclinada hacia arriba.
    Vector3 Afuera(Vector3 punto)
    {
        Vector3 afuera = punto - centro;
        afuera.y *= 0.3f;
        if (afuera.sqrMagnitude < 0.0001f)
            afuera = HorizontalAlAzar();
        return afuera.normalized;
    }

    void EmitirRoca(ParticleSystem sistema, Vector3 posicion, Vector3 impulso, float tamano, float vida)
    {
        ParticleSystem.EmitParams parametros = new ParticleSystem.EmitParams
        {
            position = posicion,
            velocity = impulso * escala,
            startSize = tamano * escala,
            startLifetime = vida,
            startColor = Color.white,
            rotation3D = Random.rotation.eulerAngles,
            angularVelocity3D = Random.insideUnitSphere * 420f
        };
        sistema.Emit(parametros, 1);
    }

    // El modulo de colisiones hace rebotar las rocas pero no las frena al rodar: aqui se
    // aplica friccion a las que ya tocan el suelo para que dejen de girar y se queden quietas.
    void AsentarEnElSuelo(ParticleSystem sistema, ParticleSystem.Particle[] buffer, float dt)
    {
        int cuantas = sistema.GetParticles(buffer);
        if (cuantas == 0)
            return;

        float friccion = Mathf.Exp(-4f * dt);
        float freno = Mathf.Exp(-6f * dt);
        for (int i = 0; i < cuantas; i++)
        {
            ParticleSystem.Particle particula = buffer[i];
            bool enElSuelo = particula.position.y <= suelo + particula.startSize * 0.6f
                && Mathf.Abs(particula.velocity.y) < 0.6f * escala;
            if (!enElSuelo)
                continue;

            Vector3 velocidadActual = particula.velocity;
            particula.velocity = new Vector3(velocidadActual.x * friccion, velocidadActual.y, velocidadActual.z * friccion);
            particula.angularVelocity3D *= freno;
            buffer[i] = particula;
        }

        sistema.SetParticles(buffer, cuantas);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (materialRocasPropio != null)
            Destroy(materialRocasPropio);
    }
}
