using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Base de los efectos de muerte. Vive en un objeto propio para sobrevivir a la destruccion
/// del enemigo (ContadorEnemigos lo destruye en el mismo frame en que muere), avanza su linea
/// de tiempo y se borra solo cuando ya no le quedan particulas vivas.
/// </summary>
public abstract class MuerteBase : MonoBehaviour
{
    protected CuerpoMuerte cuerpo;
    protected float tiempo;
    // Mitad de la altura del cuerpo: los tamanos y velocidades se escalan con ella.
    protected float escala = 1f;
    protected float cantidad = 1f;
    protected float velocidad = 1f;
    // Direccion horizontal del golpe, del atacante hacia el cuerpo.
    protected Vector3 direccionGolpe = Vector3.forward;
    protected Vector3 centro;
    protected float suelo;

    readonly List<ParticleSystem> sistemas = new List<ParticleSystem>();
    int siguienteMuestra;

    /// <summary>Segundos de linea de tiempo; despues solo se espera a que mueran las particulas.</summary>
    protected abstract float Duracion { get; }
    protected abstract void AlIniciar();
    protected abstract void Actualizar(float dt);

    /// <summary>Se llama una vez por cada punto de la superficie que la disolucion alcanza.</summary>
    protected virtual void AlLiberar(CuerpoMuerte.Muestra muestra) { }

    public void Iniciar(CuerpoMuerte cuerpoCapturado, Vector3 direccion, float multiplicadorCantidad, float multiplicadorVelocidad)
    {
        cuerpo = cuerpoCapturado;
        direccionGolpe = direccion;
        cantidad = multiplicadorCantidad;
        velocidad = multiplicadorVelocidad;

        Bounds limites = cuerpo.Limites;
        centro = limites.center;
        suelo = limites.min.y;
        escala = Mathf.Clamp(limites.size.y * 0.5f, 0.3f, 4f);

        AlIniciar();
    }

    void Update()
    {
        if (cuerpo == null)
            return;

        float dt = Time.deltaTime * velocidad;
        tiempo += dt;
        Actualizar(dt);

        if (tiempo >= Duracion && !QuedanParticulas())
            Destroy(gameObject);
    }

    protected virtual void OnDestroy()
    {
        cuerpo?.Liberar();
    }

    bool QuedanParticulas()
    {
        for (int i = 0; i < sistemas.Count; i++)
            if (sistemas[i] != null && sistemas[i].particleCount > 0)
                return true;
        return false;
    }

    protected ParticleSystem CrearSistema(string nombre, Material material, int maximo)
    {
        ParticleSystem sistema = RecursosMuerte.CrearSistema(transform, nombre, material, maximo, velocidad);
        sistemas.Add(sistema);
        return sistema;
    }

    #region Disolucion

    /// <summary>Progreso de la disolucion en el tiempo actual, del cuerpo entero a nada.</summary>
    protected float Progreso(float inicio, float duracion, float anchoBorde)
    {
        float t = Mathf.Clamp01((tiempo - inicio) / duracion);
        return Mathf.Lerp(cuerpo.CampoMinimo - anchoBorde - 0.01f, cuerpo.CampoMaximo + 0.02f, t);
    }

    /// <summary>Lanza AlLiberar para cada punto que el progreso ya alcanzo.</summary>
    protected void LiberarHasta(float progreso)
    {
        CuerpoMuerte.Muestra[] muestras = cuerpo.Muestras;
        while (siguienteMuestra < muestras.Length && muestras[siguienteMuestra].campo < progreso)
        {
            AlLiberar(muestras[siguienteMuestra]);
            siguienteMuestra++;
        }
    }

    protected bool QuedanMuestrasVivas => siguienteMuestra < cuerpo.Muestras.Length;

    /// <summary>
    /// Un punto del cuerpo que todavia no desaparece. Con sesgo mayor que 1 los puntos
    /// cercanos al borde que avanza salen con mas frecuencia.
    /// </summary>
    protected CuerpoMuerte.Muestra MuestraViva(float sesgo = 1f)
    {
        CuerpoMuerte.Muestra[] muestras = cuerpo.Muestras;
        int restantes = muestras.Length - siguienteMuestra;
        int indice = siguienteMuestra + Mathf.Min(restantes - 1, (int)(Mathf.Pow(Random.value, sesgo) * restantes));
        return muestras[indice];
    }

    protected CuerpoMuerte.Muestra MuestraCualquiera()
    {
        CuerpoMuerte.Muestra[] muestras = cuerpo.Muestras;
        return muestras[Random.Range(0, muestras.Length)];
    }

    #endregion

    #region Ayudas

    protected static float Azar(float minimo, float maximo) => Random.Range(minimo, maximo);

    protected static Vector3 HorizontalAlAzar()
    {
        Vector2 circulo = Random.insideUnitCircle;
        return new Vector3(circulo.x, 0f, circulo.y);
    }

    /// <summary>Convierte una tasa por segundo en particulas enteras para este frame.</summary>
    protected static int Cuantas(ref float acumulado, float porSegundo, float dt)
    {
        acumulado += Mathf.Max(0f, porSegundo) * dt;
        int cuantas = Mathf.FloorToInt(acumulado);
        acumulado -= cuantas;
        return cuantas;
    }

    protected static void Emitir(ParticleSystem sistema, Vector3 posicion, Vector3 velocidadInicial, float tamano, float vida, Color color, float rotacion = 0f, float giro = 0f)
    {
        ParticleSystem.EmitParams parametros = new ParticleSystem.EmitParams
        {
            position = posicion,
            velocity = velocidadInicial,
            startSize = tamano,
            startLifetime = vida,
            startColor = color,
            rotation = rotacion,
            angularVelocity = giro
        };
        sistema.Emit(parametros, 1);
    }

    protected void Ruido(ParticleSystem sistema, float fuerza, float frecuencia, float desplazamiento)
    {
        ParticleSystem.NoiseModule ruido = sistema.noise;
        ruido.enabled = true;
        ruido.strength = fuerza * escala;
        ruido.frequency = frecuencia / escala;
        ruido.scrollSpeed = desplazamiento;
        ruido.damping = true;
        ruido.octaveCount = 1;
        ruido.quality = ParticleSystemNoiseQuality.Medium;
    }

    protected static void TamanoEnVida(ParticleSystem sistema, ParticleSystem.MinMaxCurve curva)
    {
        ParticleSystem.SizeOverLifetimeModule tamano = sistema.sizeOverLifetime;
        tamano.enabled = true;
        tamano.size = curva;
    }

    protected static void ColorEnVida(ParticleSystem sistema, ParticleSystem.MinMaxGradient degradado)
    {
        ParticleSystem.ColorOverLifetimeModule color = sistema.colorOverLifetime;
        color.enabled = true;
        color.color = degradado;
    }

    protected static void Arrastre(ParticleSystem sistema, float arrastre)
    {
        ParticleSystem.LimitVelocityOverLifetimeModule limite = sistema.limitVelocityOverLifetime;
        limite.enabled = true;
        limite.limit = 1000f;
        limite.dampen = 0f;
        limite.drag = arrastre;
        limite.multiplyDragByParticleSize = false;
        limite.multiplyDragByParticleVelocity = false;
    }

    protected void Gravedad(ParticleSystem sistema, float gravedad)
    {
        ParticleSystem.MainModule principal = sistema.main;
        principal.gravityModifier = gravedad * escala;
    }

    #endregion
}
