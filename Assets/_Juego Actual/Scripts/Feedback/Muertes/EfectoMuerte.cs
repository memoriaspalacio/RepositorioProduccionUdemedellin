using UdeM.Characters;
using UnityEngine;

/// <summary>
/// Efecto de particulas al morir, personalizado por personaje. EnemyHealth lo dispara en
/// Death(); no hace falta agregarlo a mano: si el enemigo no lo tiene se elige el estilo segun
/// su comportamiento. Agregalo a un enemigo solo para forzar otro estilo o ajustar el efecto.
/// </summary>
[DisallowMultipleComponent]
public class EfectoMuerte : MonoBehaviour
{
    public enum Estilo
    {
        Automatico,        // Segun el comportamiento del enemigo
        Ninguno,
        Desintegrarse,     // Polvo de sus propios colores (esqueleto)
        Arder,             // Llamas, queda negro y suelta ceniza oscura (monjas)
        ArderCenizaBlanca, // Llamas, se calcina en blanco y suelta ceniza blanca (Monk)
        Romperse           // Se agrieta y revienta en rocas (estatuas de angel)
    }

    // Puntos de superficie que se reparten sobre el cuerpo segun el estilo.
    const int MuestrasDesintegrarse = 1800;
    const int MuestrasArder = 1300;
    const int MuestrasRomperse = 500;

    [Tooltip("Automatico: el esqueleto se desintegra, las monjas arden, el Monk arde con ceniza blanca y los angeles se rompen en rocas.")]
    public Estilo estilo = Estilo.Automatico;

    [Tooltip("Multiplica la cantidad de particulas (1 = normal).")]
    [Range(0.25f, 2f)] public float cantidad = 1f;

    [Tooltip("Velocidad del efecto (1 = normal, 2 = el doble de rapido).")]
    [Range(0.5f, 2f)] public float velocidad = 1f;

    [Tooltip("Modelo que se captura al morir. Vacio = las mallas animadas del enemigo.")]
    public Transform raizVisual;

    bool reproducido;

    public Estilo EstiloFinal => estilo != Estilo.Automatico ? estilo : Detectar(gameObject);

    public static Estilo Detectar(GameObject objeto)
    {
        if (objeto.GetComponent<SkeletonBehaviour>() != null)
            return Estilo.Desintegrarse;
        if (objeto.GetComponent<SisterBehaviour>() != null)
            return Estilo.Arder;
        if (objeto.GetComponent<MonkBehaviour>() != null)
            return Estilo.ArderCenizaBlanca;
        if (objeto.GetComponent<AngelBehaviour>() != null)
            return Estilo.Romperse;
        return Estilo.Ninguno;
    }

    /// <summary>
    /// Reproduce la muerte del enemigo. Hay que llamarlo antes de destruirlo: copia su cuerpo
    /// en un objeto aparte que sigue vivo hasta que terminan las particulas.
    /// </summary>
    public static void Reproducir(GameObject enemigo, Vector3? origenGolpe)
    {
        EfectoMuerte efecto = enemigo.GetComponent<EfectoMuerte>();
        if (efecto != null)
        {
            efecto.Reproducir(origenGolpe);
            return;
        }

        // Sin componente solo se agrega si el enemigo tiene un estilo propio (las cruces no).
        if (Detectar(enemigo) != Estilo.Ninguno)
            enemigo.AddComponent<EfectoMuerte>().Reproducir(origenGolpe);
    }

    public void Reproducir(Vector3? origenGolpe)
    {
        if (reproducido)
            return;

        Estilo final = EstiloFinal;
        if (final == Estilo.Ninguno)
            return;

        reproducido = true;

        // Quita el flash rojo y la inclinacion del golpe para capturar el aspecto real.
        MinecraftHitFeedback golpe = GetComponent<MinecraftHitFeedback>();
        if (golpe != null)
            golpe.RestaurarAspecto();

        GameObject objeto = new GameObject("Muerte " + name);
        objeto.transform.position = transform.position;

        // Si algo falla a medias no debe quedar una copia congelada del cuerpo en la escena.
        try
        {
            Crear(objeto, final, origenGolpe);
        }
        catch
        {
            Destroy(objeto);
            throw;
        }
    }

    void Crear(GameObject objeto, Estilo final, Vector3? origenGolpe)
    {
        Transform contenedor = new GameObject("Cuerpo").transform;
        contenedor.SetParent(objeto.transform, false);

        int muestras = Mathf.RoundToInt(MuestrasDe(final) * cantidad);
        CuerpoMuerte cuerpo = CuerpoMuerte.Capturar(gameObject, raizVisual, contenedor, muestras);
        if (cuerpo == null)
        {
            Debug.LogWarning($"EfectoMuerte: {name} no tiene mallas visibles para el efecto de muerte.", this);
            Destroy(objeto);
            return;
        }

        MuerteBase muerte;
        switch (final)
        {
            case Estilo.Desintegrarse:
                muerte = objeto.AddComponent<MuerteDesintegrar>();
                break;
            case Estilo.Romperse:
                muerte = objeto.AddComponent<MuerteRomperse>();
                break;
            default:
                MuerteArder arder = objeto.AddComponent<MuerteArder>();
                arder.cenizaBlanca = final == Estilo.ArderCenizaBlanca;
                muerte = arder;
                break;
        }

        muerte.Iniciar(cuerpo, DireccionGolpe(cuerpo.Limites.center, origenGolpe), cantidad, velocidad);
        cuerpo.OcultarOriginal();
    }

    static int MuestrasDe(Estilo estiloFinal)
    {
        switch (estiloFinal)
        {
            case Estilo.Desintegrarse: return MuestrasDesintegrarse;
            case Estilo.Romperse: return MuestrasRomperse;
            default: return MuestrasArder;
        }
    }

    Vector3 DireccionGolpe(Vector3 centroCuerpo, Vector3? origenGolpe)
    {
        Vector3 direccion = origenGolpe.HasValue ? centroCuerpo - origenGolpe.Value : -transform.forward;
        direccion.y = 0f;
        if (direccion.sqrMagnitude < 0.0001f)
        {
            direccion = -transform.forward;
            direccion.y = 0f;
        }
        return direccion.sqrMagnitude > 0.0001f ? direccion.normalized : Vector3.forward;
    }
}
