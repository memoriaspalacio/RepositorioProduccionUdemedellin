using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public enum FiltroColor { Ninguno, Barroco, VitralNocturno, JuicioFinal, Noir }
public enum IntensidadFiltro { Suave, Media, Completa }

/// <summary>
/// Filtros de color que el jugador elige en el menu de filtros (F2). Como GestorGraficos, se
/// crea solo al arrancar, no hay que ponerlo en ninguna escena y recuerda la eleccion.
///
/// El filtro es un Volume global propio con prioridad por encima del de cada escena: sus
/// ajustes reemplazan (mezclados segun la intensidad) los del volumen de la escena, sin tocar
/// los assets de perfil. Necesita el post proceso de la camara: con "Post proceso: Apagado"
/// no se ve, y el menu lo indica.
/// </summary>
[DefaultExecutionOrder(-990)]
public class GestorFiltros : MonoBehaviour
{
    const string ClaveFiltro = "Graficos.Filtro";
    const string ClaveIntensidad = "Graficos.FiltroIntensidad";
    const float PrioridadVolumen = 1000f;

    public static GestorFiltros Instancia { get; private set; }

    /// <summary>Despues de cambiar el filtro o su intensidad.</summary>
    public static event Action AlCambiar;

    public static FiltroColor Filtro => Instancia != null ? Instancia.filtro : FiltroColor.Ninguno;
    public static IntensidadFiltro Intensidad => Instancia != null ? Instancia.intensidad : IntensidadFiltro.Media;

    /// <summary>Con el post proceso apagado la camara no dibuja ningun volumen.</summary>
    public static bool Disponible => GestorGraficos.Ajustes.postProceso != CalidadPostProceso.Apagado;

    FiltroColor filtro;
    IntensidadFiltro intensidad;
    Volume volumen;
    VolumeProfile[] perfiles;

    #region Arranque

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar()
    {
        Instancia = null;
        AlCambiar = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Crear()
    {
        if (Instancia != null)
            return;

        GameObject objeto = new GameObject("Gestor Filtros");
        DontDestroyOnLoad(objeto);
        objeto.AddComponent<GestorFiltros>();
    }

    void Awake()
    {
        if (Instancia != null && Instancia != this)
        {
            Destroy(gameObject);
            return;
        }

        Instancia = this;
        filtro = (FiltroColor)Mathf.Clamp(PlayerPrefs.GetInt(ClaveFiltro, 0), 0, (int)FiltroColor.Noir);
        intensidad = (IntensidadFiltro)Mathf.Clamp(PlayerPrefs.GetInt(ClaveIntensidad, (int)IntensidadFiltro.Media), 0, 2);

        volumen = gameObject.AddComponent<Volume>();
        volumen.isGlobal = true;
        volumen.priority = PrioridadVolumen;

        Aplicar();
    }

    void OnDestroy()
    {
        if (Instancia != this)
            return;

        Instancia = null;
        if (perfiles == null)
            return;

        for (int i = 0; i < perfiles.Length; i++)
        {
            if (perfiles[i] == null)
                continue;

            for (int c = 0; c < perfiles[i].components.Count; c++)
                Destroy(perfiles[i].components[c]);
            Destroy(perfiles[i]);
        }
    }

    void Update()
    {
        Keyboard teclado = Keyboard.current;
        if (teclado != null && teclado.f2Key.wasPressedThisFrame)
            MenuFiltros.Alternar();
    }

    #endregion

    #region API

    public static void Usar(FiltroColor nuevo)
    {
        if (Instancia == null || Instancia.filtro == nuevo)
            return;

        Instancia.filtro = nuevo;
        Instancia.Guardar();
        Instancia.Aplicar();
    }

    public static void UsarIntensidad(IntensidadFiltro nueva)
    {
        if (Instancia == null || Instancia.intensidad == nueva)
            return;

        Instancia.intensidad = nueva;
        Instancia.Guardar();
        Instancia.Aplicar();
    }

    /// <summary>True si el volumen es el de los filtros: GestorGraficos no debe rebajarlo.</summary>
    public static bool EsVolumenFiltro(Volume volumen)
    {
        return Instancia != null && volumen == Instancia.volumen;
    }

    #endregion

    void Aplicar()
    {
        volumen.enabled = filtro != FiltroColor.Ninguno;
        volumen.weight = Peso(intensidad);
        volumen.sharedProfile = filtro != FiltroColor.Ninguno ? Perfil(filtro) : null;

        AlCambiar?.Invoke();
    }

    static float Peso(IntensidadFiltro intensidad)
    {
        switch (intensidad)
        {
            case IntensidadFiltro.Suave: return 0.4f;
            case IntensidadFiltro.Media: return 0.7f;
            default: return 1f;
        }
    }

    void Guardar()
    {
        PlayerPrefs.SetInt(ClaveFiltro, (int)filtro);
        PlayerPrefs.SetInt(ClaveIntensidad, (int)intensidad);
        PlayerPrefs.Save();
    }

    VolumeProfile Perfil(FiltroColor cual)
    {
        if (perfiles == null)
            perfiles = new VolumeProfile[(int)FiltroColor.Noir + 1];

        int indice = (int)cual;
        if (perfiles[indice] == null)
            perfiles[indice] = CrearPerfil(cual);

        return perfiles[indice];
    }

    /// <summary>
    /// Crea el perfil de un filtro a partir de sus Parametros. Solo toca color (ajustes, viraje,
    /// sombras/luces, balance de blancos, vineta y grano): nada que cambie variantes de shader ni
    /// cueste mas que el post proceso que ya tiene el juego.
    /// </summary>
    static VolumeProfile CrearPerfil(FiltroColor cual)
    {
        Parametros p = ParametrosDe(cual);
        VolumeProfile perfil = ScriptableObject.CreateInstance<VolumeProfile>();
        perfil.name = "Filtro " + cual;

        ColorAdjustments color = perfil.Add<ColorAdjustments>();
        color.postExposure.Override(p.exposicion);
        color.contrast.Override(p.contraste);
        color.saturation.Override(p.saturacion);
        color.colorFilter.Override(p.filtro);

        SplitToning viraje = perfil.Add<SplitToning>();
        viraje.shadows.Override(p.sombras);
        viraje.highlights.Override(p.luces);
        viraje.balance.Override(p.balance);

        ShadowsMidtonesHighlights tonos = perfil.Add<ShadowsMidtonesHighlights>();
        tonos.shadows.Override(p.tonoSombras);
        tonos.highlights.Override(p.tonoLuces);

        if (p.temperatura != 0f || p.tinte != 0f)
        {
            WhiteBalance balance = perfil.Add<WhiteBalance>();
            balance.temperature.Override(p.temperatura);
            balance.tint.Override(p.tinte);
        }

        if (p.vineta > 0f)
        {
            Vignette vineta = perfil.Add<Vignette>();
            vineta.color.Override(p.colorVineta);
            vineta.intensity.Override(p.vineta);
            vineta.smoothness.Override(0.5f);
        }

        if (p.grano > 0f)
        {
            FilmGrain grano = perfil.Add<FilmGrain>();
            grano.type.Override(FilmGrainLookup.Medium2);
            grano.intensity.Override(p.grano);
        }

        return perfil;
    }

    #region Vista previa

    /// <summary>
    /// Dibuja la camara en una textura sin el filtro de color, para la vista previa del menu.
    /// La interfaz (canvas en overlay) no sale porque no la dibuja la camara.
    /// </summary>
    public static bool CapturarSinFiltro(Camera camara, RenderTexture destino)
    {
        if (camara == null || destino == null)
            return false;

        bool conFiltro = Instancia != null && Instancia.volumen.enabled;
        if (conFiltro)
            Instancia.volumen.enabled = false;

        RenderPipeline.StandardRequest pedido = new RenderPipeline.StandardRequest { destination = destino };
        if (RenderPipeline.SupportsRenderRequest(camara, pedido))
        {
            RenderPipeline.SubmitRenderRequest(camara, pedido);
        }
        else
        {
            RenderTexture anterior = camara.targetTexture;
            camara.targetTexture = destino;
            camara.Render();
            camara.targetTexture = anterior;
        }

        if (conFiltro)
            Instancia.volumen.enabled = true;

        return true;
    }

    #endregion

    #region Parametros

    /// <summary>
    /// Los valores de un filtro. Los usan el volumen del juego y la vista previa del menu, asi la
    /// muestra se parece a lo que se vera. Los colores del viraje son tintes: el gris medio (0.5)
    /// no tine.
    /// </summary>
    public struct Parametros
    {
        public float exposicion;
        public float contraste;
        public float saturacion;
        public Color filtro;
        public Color sombras;
        public Color luces;
        public float balance;
        public Vector4 tonoSombras;
        public Vector4 tonoLuces;
        public float temperatura;
        public float tinte;
        public float vineta;
        public Color colorVineta;
        public float grano;
    }

    public static Parametros ParametrosDe(FiltroColor cual)
    {
        Parametros p = new Parametros
        {
            filtro = Color.white,
            sombras = new Color(0.5f, 0.5f, 0.5f),
            luces = new Color(0.5f, 0.5f, 0.5f),
            tonoSombras = new Vector4(1f, 1f, 1f, 0f),
            tonoLuces = new Vector4(1f, 1f, 1f, 0f),
            colorVineta = Color.black
        };

        switch (cual)
        {
            // Oleo barroco: claroscuro de Caravaggio. Sombras hondas y calidas, luces doradas,
            // color contenido como un barniz viejo.
            case FiltroColor.Barroco:
                p.exposicion = -0.05f;
                p.contraste = 24f;
                p.saturacion = -20f;
                p.filtro = new Color(1f, 0.9f, 0.74f);
                p.sombras = new Color(0.42f, 0.3f, 0.2f);
                p.luces = new Color(1f, 0.82f, 0.55f);
                p.balance = 10f;
                p.tonoSombras = new Vector4(1f, 0.92f, 0.82f, -0.12f);
                p.tonoLuces = new Vector4(1f, 0.95f, 0.85f, 0.05f);
                p.vineta = 0.45f;
                p.colorVineta = new Color(0.12f, 0.06f, 0.02f);
                break;

            // Vitral nocturno: luna fria atravesando vitrales. Sombras azules, luces violeta y
            // magenta (las de las lamparas del cementerio), algo mas de color.
            case FiltroColor.VitralNocturno:
                p.exposicion = 0.05f;
                p.contraste = 12f;
                p.saturacion = 15f;
                p.filtro = new Color(0.84f, 0.9f, 1f);
                p.sombras = new Color(0.22f, 0.32f, 0.65f);
                p.luces = new Color(0.88f, 0.55f, 0.85f);
                p.balance = -12f;
                p.tonoSombras = new Vector4(0.85f, 0.92f, 1.1f, -0.05f);
                p.tonoLuces = new Vector4(1.05f, 0.95f, 1.08f, 0f);
                p.temperatura = -18f;
                p.tinte = 8f;
                break;

            // Juicio final: el apocalipsis de la mazmorra. Sombras carmesi, luces de fuego y
            // contraste duro.
            case FiltroColor.JuicioFinal:
                p.exposicion = 0.05f;
                p.contraste = 30f;
                p.saturacion = 8f;
                p.filtro = new Color(1f, 0.84f, 0.72f);
                p.sombras = new Color(0.48f, 0.12f, 0.16f);
                p.luces = new Color(1f, 0.6f, 0.25f);
                p.balance = 20f;
                p.tonoSombras = new Vector4(1f, 0.82f, 0.82f, -0.15f);
                p.tonoLuces = new Vector4(1.1f, 0.95f, 0.8f, 0.05f);
                break;

            // Noir: blanco y negro de cine clasico. Sin color, negros profundos, luces limpias,
            // vineta y grano de pelicula.
            case FiltroColor.Noir:
                p.exposicion = 0.1f;
                p.contraste = 40f;
                p.saturacion = -100f;
                p.tonoSombras = new Vector4(1f, 1f, 1f, -0.18f);
                p.tonoLuces = new Vector4(1f, 1f, 1f, 0.08f);
                p.vineta = 0.5f;
                p.grano = 0.35f;
                break;
        }

        return p;
    }

    /// <summary>
    /// Aproximacion en CPU de lo que hace el filtro con un color (en espacio gamma), para la
    /// vista previa y las muestras del menu. uv y ruido solo sirven para la vineta y el grano.
    /// </summary>
    public static Color Transformar(Color original, Parametros p, float peso, Vector2 uv, float ruido)
    {
        if (peso <= 0f)
            return original;

        Vector3 c = new Vector3(original.r, original.g, original.b) * Mathf.Pow(2f, p.exposicion);
        c = Vector3.Scale(c, new Vector3(p.filtro.r, p.filtro.g, p.filtro.b));

        float temperatura = p.temperatura / 100f;
        c.x *= 1f + temperatura * 0.25f;
        c.z *= 1f - temperatura * 0.25f;
        c.y *= 1f - p.tinte / 100f * 0.15f;

        float luma = Luma(c);
        c = Vector3.LerpUnclamped(new Vector3(luma, luma, luma), c, 1f + p.saturacion / 100f);
        c = (c - new Vector3(0.5f, 0.5f, 0.5f)) * (1f + p.contraste / 100f) + new Vector3(0.5f, 0.5f, 0.5f);

        float nivel = Mathf.Clamp01(Luma(c));
        float mezcla = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(nivel + p.balance / 200f));
        Color tinte = Color.Lerp(p.sombras, p.luces, mezcla) * 2f;
        c = Vector3.Lerp(c, Vector3.Scale(c, new Vector3(tinte.r, tinte.g, tinte.b)), 0.5f);

        float enSombra = (1f - nivel) * (1f - nivel);
        float enLuz = nivel * nivel;
        c = Vector3.Scale(c, Vector3.Lerp(Vector3.one, p.tonoSombras, enSombra));
        c = Vector3.Scale(c, Vector3.Lerp(Vector3.one, p.tonoLuces, enLuz));
        c += Vector3.one * (p.tonoSombras.w * enSombra + p.tonoLuces.w * enLuz) * 0.5f;

        if (p.vineta > 0f)
        {
            float distancia = (uv - new Vector2(0.5f, 0.5f)).magnitude * 2f;
            float borde = p.vineta * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1.3f, distancia));
            c = Vector3.Lerp(c, new Vector3(p.colorVineta.r, p.colorVineta.g, p.colorVineta.b), borde);
        }

        c += Vector3.one * (ruido - 0.5f) * p.grano * 0.18f;

        Color final = new Color(Mathf.Clamp01(c.x), Mathf.Clamp01(c.y), Mathf.Clamp01(c.z), original.a);
        return Color.Lerp(original, final, peso);
    }

    static float Luma(Vector3 c)
    {
        return c.x * 0.2126f + c.y * 0.7152f + c.z * 0.0722f;
    }

    public static float PesoDe(IntensidadFiltro intensidad) => Peso(intensidad);

    #endregion
}
