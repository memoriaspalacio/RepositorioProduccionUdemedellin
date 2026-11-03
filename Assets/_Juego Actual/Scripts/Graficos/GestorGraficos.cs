using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Aplica la calidad grafica elegida en el menu. Se crea solo al arrancar el juego (no hay
/// que ponerlo en ninguna escena), recuerda la eleccion en PlayerPrefs y la vuelve a aplicar
/// en cada escena que se carga. F1 abre el menu en cualquier escena.
///
/// Hay dos tipos de ajuste:
/// - El nivel (Alto, Medio, Bajo, Muy bajo) cambia el nivel de Quality Settings, y con el el
///   asset de URP. Es la unica forma segura de cambiar lo que decide variantes de shader (SSAO,
///   sombras suaves, cascadas, Forward+): el proyecto tiene activo el stripping de variantes y
///   en una build no existen las combinaciones que ningun asset usa. En el editor se veria bien
///   igual, por eso hay que tener cuidado al agregar ajustes nuevos aqui.
/// - Todo lo demas (resolucion, sombras, texturas, FPS, post proceso, particulas, luces) se
///   ajusta encima del nivel sin cambiar variantes, asi que se puede combinar libremente.
///
/// Alto es el nivel "PC": con todo en Alto solo se aplican sus propios valores.
///
/// Ojo con "FPS: Sin limite": la GPU dibuja todos los fotogramas que puede y trabaja al 100 %
/// por ligero que sea el juego. Medido en una RTX 3050: 99 % y 63 W sin limite, 52 W a 144 FPS
/// (Pantalla en un monitor de 144 Hz) y 22 W a 60.
/// </summary>
[DefaultExecutionOrder(-1000)]
public class GestorGraficos : MonoBehaviour
{
    const string ClaveAjustes = "Graficos.Ajustes";

    /// <summary>Nombre del nivel de Quality Settings de cada NivelGrafico, en el mismo orden.</summary>
    public static readonly string[] NombresNiveles = { "PC", "Medio", "Bajo", "Muy bajo" };

    /// <summary>
    /// Escenas con el boton "Graficos" en la esquina si no tienen un panel de opciones donde
    /// agregar la fila (FilaGraficosOpciones). En las demas: F1 o un BotonAbrirGraficos.
    /// </summary>
    public static readonly string[] EscenasConBoton = { "menu" };

    public static GestorGraficos Instancia { get; private set; }

    /// <summary>Copia de los ajustes en uso. Para cambiarlos: Aplicar, Modificar o UsarNivel.</summary>
    public static AjustesGraficos Ajustes => Instancia != null ? Instancia.ajustes.Copia() : AjustesGraficos.DePreset(NivelGrafico.Alto);

    /// <summary>Despues de aplicar cualquier cambio.</summary>
    public static event Action AlCambiar;

    /// <summary>Multiplica las particulas de los efectos que se crean en juego (muertes).</summary>
    public static float MultiplicadorEfectos { get; private set; } = 1f;

    /// <summary>False con luces minimas: los efectos no encienden luces dinamicas propias.</summary>
    public static bool LucesDeEfectos { get; private set; } = true;

    struct ParticulasOriginales
    {
        public int maximo;
        public float tasa;
        public float tasaDistancia;
        public bool luces;
        public int maxLuces;
    }

    AjustesGraficos ajustes;
    bool avisoNivel;

    // Lo que se cambio en la escena actual, con sus valores originales
    readonly Dictionary<Volume, VolumeProfile> perfilesPropios = new Dictionary<Volume, VolumeProfile>();
    readonly Dictionary<ParticleSystem, ParticulasOriginales> particulas = new Dictionary<ParticleSystem, ParticulasOriginales>();
    readonly Dictionary<Light, bool> lucesContorno = new Dictionary<Light, bool>();
    readonly List<UniversalAdditionalCameraData> camarasSinPost = new List<UniversalAdditionalCameraData>();

    #region Arranque

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar()
    {
        // Por si el editor entra a Play sin recargar el dominio
        Instancia = null;
        AlCambiar = null;
        MultiplicadorEfectos = 1f;
        LucesDeEfectos = true;
        RespaldoGraficos.Olvidar();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Crear()
    {
        if (Instancia != null)
            return;

        GameObject objeto = new GameObject("Gestor Graficos");
        DontDestroyOnLoad(objeto);
        objeto.AddComponent<GestorGraficos>();
    }

    void Awake()
    {
        if (Instancia != null && Instancia != this)
        {
            Destroy(gameObject);
            return;
        }

        Instancia = this;
        RespaldoGraficos.GuardarNivel();
        ajustes = Cargar();

        SceneManager.sceneLoaded += AlCargarEscena;
        Application.quitting += AlSalir;

        AplicarTodo();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"Graficos: calidad {ajustes.nivel} (nivel '{NombreNivelActivo}'). F1 abre el menu de graficos.");
#endif
    }

    void OnDestroy()
    {
        if (Instancia != this)
            return;

        SceneManager.sceneLoaded -= AlCargarEscena;
        Application.quitting -= AlSalir;
        Instancia = null;
    }

    void AlSalir()
    {
        RespaldoGraficos.RestaurarTodo();
    }

    void Update()
    {
        Keyboard teclado = Keyboard.current;
        if (teclado != null && teclado.f1Key.wasPressedThisFrame)
            MenuGraficos.Alternar();
    }

    void AlCargarEscena(Scene escena, LoadSceneMode modo)
    {
        OlvidarDestruidos();

        // La distancia de las sombras apagadas depende de la camara de la escena
        AplicarPipeline();
        AplicarEscena();

        bool hayOpciones = FilaGraficosOpciones.Integrar(escena);
        if (!hayOpciones && Array.IndexOf(EscenasConBoton, escena.name) >= 0)
            MenuGraficos.CrearBotonFlotante(escena);
    }

    #endregion

    #region API

    public static void Aplicar(AjustesGraficos nuevos)
    {
        if (Instancia == null)
            return;

        Instancia.ajustes = nuevos.Copia();
        Instancia.Guardar();
        Instancia.AplicarTodo();
    }

    /// <summary>Cambia algunos ajustes y deja el resto como esta. Ej: Modificar(a => a.fps = LimiteFps.Fps60).</summary>
    public static void Modificar(Action<AjustesGraficos> cambio)
    {
        AjustesGraficos nuevos = Ajustes;
        cambio(nuevos);
        Aplicar(nuevos);
    }

    /// <summary>Pasa a todos los valores de un nivel (conserva el contador de FPS).</summary>
    public static void UsarNivel(NivelGrafico nivel)
    {
        AjustesGraficos nuevos = AjustesGraficos.DePreset(nivel);
        nuevos.mostrarFps = Ajustes.mostrarFps;
        Aplicar(nuevos);
    }

    /// <summary>
    /// Nivel recomendado para este equipo. Es una estimacion por memoria y tipo de grafica:
    /// el menu deja corregirla.
    /// </summary>
    public static NivelGrafico DetectarNivel()
    {
        int memoria = SystemInfo.systemMemorySize;
        int memoriaGrafica = SystemInfo.graphicsMemorySize;
        string grafica = SystemInfo.graphicsDeviceName ?? string.Empty;

        if (Application.isMobilePlatform)
        {
            if (memoria >= 7000)
                return NivelGrafico.Medio;
            if (memoria >= 3500)
                return NivelGrafico.Bajo;
            return NivelGrafico.MuyBajo;
        }

        // Las integradas reportan memoria compartida como si fuera propia: no sirve la VRAM
        bool integrada = (grafica.Contains("Intel") && !grafica.Contains("Arc"))
            || (grafica.Contains("Radeon") && grafica.Contains("Graphics") && !grafica.Contains("RX"));
        if (integrada)
        {
            bool potente = grafica.Contains("Iris") || grafica.Contains("680M") || grafica.Contains("780M") || grafica.Contains("890M");
            return potente ? NivelGrafico.Medio : NivelGrafico.Bajo;
        }

        // Las graficas de 4 GB reportan un poco menos (3.9 GB)
        if (memoriaGrafica >= 3500 && SystemInfo.processorCount >= 4)
            return NivelGrafico.Alto;
        if (memoriaGrafica >= 1800)
            return NivelGrafico.Medio;
        return NivelGrafico.Bajo;
    }

    /// <summary>El nivel en uso no tiene sombras en tiempo real (Muy bajo): la opcion no aplica.</summary>
    public static bool SombrasDisponibles
    {
        get
        {
            UniversalRenderPipelineAsset asset = AssetActual();
            return asset != null && asset.supportsMainLightShadows;
        }
    }

    /// <summary>Solo los niveles cuyo renderer trae el SSAO (Alto y Medio) pueden encenderlo.</summary>
    public static bool OclusionDisponible => BuscarOclusion(AssetActual()) != null;

    public static string NombreNivelActivo
    {
        get
        {
            string[] nombres = QualitySettings.names;
            int nivel = QualitySettings.GetQualityLevel();
            return nivel >= 0 && nivel < nombres.Length ? nombres[nivel] : "?";
        }
    }

    #endregion

    #region Aplicar

    void AplicarTodo()
    {
        AplicarNivel();
        AplicarPipeline();
        AplicarCalidad();
        AplicarAtmosfera();
        AplicarEscena();
        ContadorFps.Mostrar(ajustes.mostrarFps, transform);

        AlCambiar?.Invoke();
    }

    void AplicarNivel()
    {
        int indice = IndiceNivel(ajustes.nivel);
        if (indice >= 0 && QualitySettings.GetQualityLevel() != indice)
            QualitySettings.SetQualityLevel(indice, true);
    }

    int IndiceNivel(NivelGrafico nivel)
    {
        string[] nombres = QualitySettings.names;
        int pedido = (int)nivel;

        // Si esta build no tiene el nivel pedido se usa el mas cercano, primero hacia abajo
        for (int distancia = 0; distancia < NombresNiveles.Length; distancia++)
        {
            for (int lado = 0; lado < 2; lado++)
            {
                int candidato = lado == 0 ? pedido + distancia : pedido - distancia;
                if (candidato < 0 || candidato >= NombresNiveles.Length)
                    continue;

                int indice = Array.IndexOf(nombres, NombresNiveles[candidato]);
                if (indice < 0)
                    continue;

                if (distancia > 0 && !avisoNivel)
                {
                    avisoNivel = true;
                    Debug.LogWarning($"Graficos: no existe el nivel '{NombresNiveles[pedido]}' en Quality Settings; se usa '{NombresNiveles[candidato]}'. " +
                                     "Herramientas > Graficos > Configurar niveles de calidad los crea.");
                }

                return indice;
            }
        }

        return -1;
    }

    void AplicarPipeline()
    {
        UniversalRenderPipelineAsset asset = AssetActual();
        if (asset == null)
            return;

        RespaldoGraficos.ValoresPipeline original = RespaldoGraficos.Pipeline(asset);

        asset.renderScale = ajustes.FactorResolucion;

        if (asset.supportsMainLightShadows)
        {
            RespaldoGraficos.ValoresPipeline alto = ValoresAlto() ?? original;
            int resolucion;
            float distancia;
            int cascadas;

            switch (ajustes.sombras)
            {
                case CalidadSombras.Altas:
                    resolucion = alto.resolucionSombras;
                    distancia = alto.distanciaSombras;
                    cascadas = alto.cascadas;
                    break;
                case CalidadSombras.Medias:
                    resolucion = Mathf.Min(1024, alto.resolucionSombras);
                    distancia = Mathf.Min(25f, alto.distanciaSombras);
                    cascadas = 2;
                    break;
                case CalidadSombras.Bajas:
                    resolucion = Mathf.Min(512, alto.resolucionSombras);
                    distancia = Mathf.Min(20f, alto.distanciaSombras);
                    cascadas = 2;
                    break;
                default:
                    // No se apagan en el asset (eso pide la variante sin sombras, que el stripping
                    // quita): se acorta la distancia hasta casi el plano cercano. No queda nada que
                    // proyecte sombra en tiempo real, URP usa un shadowmap vacio y las sombras
                    // horneadas del shadowmask se siguen viendo.
                    resolucion = 256;
                    distancia = DistanciaSinSombras();
                    cascadas = 2;
                    break;
            }

            // Pasar de una cascada a varias (o al reves) tambien cambia de variante: se respeta el nivel
            asset.shadowCascadeCount = original.cascadas > 1 ? Mathf.Clamp(cascadas, 2, 4) : 1;
            asset.mainLightShadowmapResolution = resolucion;
            asset.shadowDistance = distancia;
        }

        ScreenSpaceAmbientOcclusion oclusion = BuscarOclusion(asset);
        if (oclusion != null)
        {
            RespaldoGraficos.GuardarEfecto(oclusion);
            oclusion.SetActive(ajustes.oclusionAmbiental);
        }
    }

    void AplicarCalidad()
    {
        RespaldoGraficos.GuardarMipmaps();
        if (QualitySettings.globalTextureMipmapLimit != ajustes.LimiteMipmaps)
            QualitySettings.globalTextureMipmapLimit = ajustes.LimiteMipmaps;

        Application.targetFrameRate = ajustes.FotogramasObjetivo;
    }

    void AplicarAtmosfera()
    {
        MultiplicadorEfectos = ajustes.MultiplicadorParticulas;
        LucesDeEfectos = ajustes.luces != CalidadLuces.Minimas;

        // AtmosferaEscena ya tenia estos interruptores; aqui solo se les da valor. Cada
        // asignacion reaplica todas las atmosferas, asi que solo se asigna si cambia.
        bool particulasAtmosfera = ajustes.particulas != CalidadParticulas.Apagadas;
        if (AtmosferaEscena.ParticulasPermitidas != particulasAtmosfera)
            AtmosferaEscena.ParticulasPermitidas = particulasAtmosfera;

        bool postProceso = ajustes.postProceso != CalidadPostProceso.Apagado;
        if (AtmosferaEscena.PostProcesoPermitido != postProceso)
            AtmosferaEscena.PostProcesoPermitido = postProceso;

        if (AtmosferaEscena.VolumetricosPermitidos != ajustes.hacesDeLuz)
            AtmosferaEscena.VolumetricosPermitidos = ajustes.hacesDeLuz;

        // El nivel puede haber quitado la textura de profundidad, de la que depende que la
        // niebla y los haces se fundan suave con el suelo
        AtmosferaEscena.AplicarEnTodas();
    }

    void AplicarEscena()
    {
        AplicarPostProceso();
        AplicarCamaras();
        AplicarParticulas();
        AplicarLuces();
    }

    /// <summary>
    /// Ligero abarata el bloom; Minimo lo quita junto con el grano y deja el color, el
    /// tonemapping y la vineta, que salen casi gratis. Los cambios van sobre una copia del
    /// perfil de cada volumen: los assets de perfil no se tocan.
    /// </summary>
    void AplicarPostProceso()
    {
        CalidadPostProceso calidad = ajustes.postProceso;
        bool rebajar = calidad == CalidadPostProceso.Ligero || calidad == CalidadPostProceso.Minimo;

        Volume[] volumenes = FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < volumenes.Length; i++)
        {
            Volume volumen = volumenes[i];
            LiberarPerfil(volumen);

            if (!rebajar || volumen.sharedProfile == null)
                continue;

            // Volume.profile crea una copia del perfil compartido para este volumen
            VolumeProfile copia = volumen.profile;
            perfilesPropios.Add(volumen, copia);

            if (copia.TryGet(out Bloom bloom))
            {
                if (calidad == CalidadPostProceso.Minimo)
                {
                    bloom.active = false;
                }
                else
                {
                    bloom.highQualityFiltering.Override(false);
                    bloom.downscale.Override(BloomDownscaleMode.Quarter);
                    bloom.maxIterations.Override(Mathf.Min(bloom.maxIterations.value, 4));
                }
            }

            if (calidad == CalidadPostProceso.Minimo && copia.TryGet(out FilmGrain grano))
                grano.active = false;
        }
    }

    /// <summary>
    /// Con el post proceso apagado AtmosferaEscena apaga el de la camara principal; esto cubre
    /// las escenas sin atmosfera (menu, victoria, derrota) y las demas camaras.
    /// </summary>
    void AplicarCamaras()
    {
        if (ajustes.postProceso != CalidadPostProceso.Apagado)
        {
            for (int i = 0; i < camarasSinPost.Count; i++)
            {
                if (camarasSinPost[i] != null)
                    camarasSinPost[i].renderPostProcessing = true;
            }

            camarasSinPost.Clear();
            return;
        }

        Camera[] camaras = Camera.allCameras;
        for (int i = 0; i < camaras.Length; i++)
        {
            UniversalAdditionalCameraData datos = camaras[i].GetUniversalAdditionalCameraData();
            if (datos != null && datos.renderPostProcessing)
            {
                datos.renderPostProcessing = false;
                camarasSinPost.Add(datos);
            }
        }
    }

    void AplicarParticulas()
    {
        float multiplicador = ajustes.MultiplicadorParticulas;

        ParticleSystem[] sistemas = FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < sistemas.Length; i++)
        {
            ParticleSystem sistema = sistemas[i];

            // Los efectos de muerte ya nacen con su cantidad ajustada (MultiplicadorEfectos)
            if (sistema.GetComponentInParent<MuerteBase>(true) != null)
                continue;

            if (!particulas.TryGetValue(sistema, out ParticulasOriginales original))
            {
                original = new ParticulasOriginales
                {
                    maximo = sistema.main.maxParticles,
                    tasa = sistema.emission.rateOverTimeMultiplier,
                    tasaDistancia = sistema.emission.rateOverDistanceMultiplier,
                    luces = sistema.lights.enabled,
                    maxLuces = sistema.lights.maxLights
                };
                particulas.Add(sistema, original);
            }

            ParticleSystem.MainModule principal = sistema.main;
            principal.maxParticles = Mathf.Max(1, Mathf.CeilToInt(original.maximo * multiplicador));

            ParticleSystem.EmissionModule emision = sistema.emission;
            emision.rateOverTimeMultiplier = original.tasa * multiplicador;
            emision.rateOverDistanceMultiplier = original.tasaDistancia * multiplicador;

            // Las luciernagas llevan luces reales que iluminan lo que tienen cerca
            if (original.luces)
            {
                ParticleSystem.LightsModule luces = sistema.lights;
                luces.enabled = ajustes.luces != CalidadLuces.Minimas;
                luces.maxLights = ajustes.luces == CalidadLuces.Todas ? original.maxLuces : Mathf.Max(1, original.maxLuces / 2);
            }
        }
    }

    /// <summary>Con luces minimas los personajes conservan la luz de relleno pero pierden el contorno.</summary>
    void AplicarLuces()
    {
        bool contorno = ajustes.luces != CalidadLuces.Minimas;

        LuzPersonajes[] rigs = FindObjectsByType<LuzPersonajes>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < rigs.Length; i++)
        {
            Light luz = rigs[i].contorno;
            if (luz == null)
                continue;

            if (!lucesContorno.TryGetValue(luz, out bool encendida))
            {
                encendida = luz.enabled;
                lucesContorno.Add(luz, encendida);
            }

            luz.enabled = contorno && encendida;
        }
    }

    #endregion

    #region Ayudas

    static UniversalRenderPipelineAsset AssetActual()
    {
        return GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
    }

    /// <summary>Valores originales del asset de Alto: son los de las sombras "Altas" en cualquier nivel.</summary>
    static RespaldoGraficos.ValoresPipeline ValoresAlto()
    {
        int indice = Array.IndexOf(QualitySettings.names, NombresNiveles[(int)NivelGrafico.Alto]);
        if (indice < 0)
            return null;

        UniversalRenderPipelineAsset alto = QualitySettings.GetRenderPipelineAssetAt(indice) as UniversalRenderPipelineAsset;
        return alto != null ? RespaldoGraficos.Pipeline(alto) : null;
    }

    static float DistanciaSinSombras()
    {
        // Por debajo del plano cercano URP pone la intensidad de sombra a cero y con ella
        // desaparecerian tambien las sombras horneadas: hay que quedarse un poco por encima
        Camera camara = Camera.main;
        float cercano = camara != null ? camara.nearClipPlane : 0.3f;
        return cercano + 0.5f;
    }

    static ScreenSpaceAmbientOcclusion BuscarOclusion(UniversalRenderPipelineAsset asset)
    {
        if (asset == null)
            return null;

        foreach (ScriptableRendererData datos in asset.rendererDataList)
        {
            if (datos == null)
                continue;

            List<ScriptableRendererFeature> efectos = datos.rendererFeatures;
            for (int i = 0; i < efectos.Count; i++)
            {
                if (efectos[i] is ScreenSpaceAmbientOcclusion oclusion)
                    return oclusion;
            }
        }

        return null;
    }

    void LiberarPerfil(Volume volumen)
    {
        if (!perfilesPropios.TryGetValue(volumen, out VolumeProfile copia))
            return;

        perfilesPropios.Remove(volumen);

        // Sin perfil propio el volumen vuelve a usar el compartido
        if (volumen != null)
            volumen.profile = null;

        DestruirPerfil(copia);
    }

    static void DestruirPerfil(VolumeProfile perfil)
    {
        if (perfil == null)
            return;

        for (int i = 0; i < perfil.components.Count; i++)
        {
            if (perfil.components[i] != null)
                Destroy(perfil.components[i]);
        }

        Destroy(perfil);
    }

    /// <summary>Al cambiar de escena se olvida lo que pertenecia a la anterior.</summary>
    void OlvidarDestruidos()
    {
        List<Volume> volumenes = new List<Volume>(perfilesPropios.Keys);
        for (int i = 0; i < volumenes.Count; i++)
        {
            if (volumenes[i] == null)
                LiberarPerfil(volumenes[i]);
        }

        List<ParticleSystem> sistemas = new List<ParticleSystem>(particulas.Keys);
        for (int i = 0; i < sistemas.Count; i++)
        {
            if (sistemas[i] == null)
                particulas.Remove(sistemas[i]);
        }

        List<Light> luces = new List<Light>(lucesContorno.Keys);
        for (int i = 0; i < luces.Count; i++)
        {
            if (luces[i] == null)
                lucesContorno.Remove(luces[i]);
        }

        camarasSinPost.RemoveAll(datos => datos == null);
    }

    static AjustesGraficos Cargar()
    {
        string json = PlayerPrefs.GetString(ClaveAjustes, string.Empty);
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                AjustesGraficos guardados = JsonUtility.FromJson<AjustesGraficos>(json);
                if (guardados != null && guardados.version == AjustesGraficos.VersionActual)
                    return guardados;
            }
            catch (ArgumentException)
            {
                // Guardado corrupto: se vuelve a detectar
            }
        }

        // Primera vez: en el editor el aspecto original; en una build, lo que aguante el equipo
        return AjustesGraficos.DePreset(Application.isEditor ? NivelGrafico.Alto : DetectarNivel());
    }

    void Guardar()
    {
        PlayerPrefs.SetString(ClaveAjustes, JsonUtility.ToJson(ajustes));
        PlayerPrefs.Save();
    }

    #endregion
}
