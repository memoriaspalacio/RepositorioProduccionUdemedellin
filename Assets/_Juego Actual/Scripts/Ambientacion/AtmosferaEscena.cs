using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[ExecuteAlways]
[DisallowMultipleComponent]
public class AtmosferaEscena : MonoBehaviour
{
    const string ClaveParticulas = "Atmosfera.Particulas";
    const string ClavePostProceso = "Atmosfera.PostProceso";
    const string ClaveVolumetricos = "Atmosfera.Volumetricos";

    static readonly int IdSuave = Shader.PropertyToID("_AtmosferaSuave");

    [Header("Efectos de esta escena")]
    public bool particulas = true;
    public bool postProceso = true;
    [Tooltip("Haces de luz y otros efectos volumetricos falsos.")]
    public bool volumetricos = true;
    public bool nieblaUnity = true;
    [Tooltip("En juego, apaga tambien el post proceso de la camara cuando los volumenes estan apagados.")]
    public bool apagarPostProcesoCamara = true;

    [Header("Apagar por nivel de calidad (0 = Mobile, 1 = PC)")]
    public int[] calidadesSinParticulas = { };
    public int[] calidadesSinPostProceso = { };
    public int[] calidadesSinVolumetricos = { };

    [Header("Referencias")]
    public GameObject[] objetosParticulas = { };
    public Volume[] volumenes = { };
    public GameObject[] objetosVolumetricos = { };

    public static bool ParticulasPermitidas
    {
        get => PlayerPrefs.GetInt(ClaveParticulas, 1) == 1;
        set
        {
            PlayerPrefs.SetInt(ClaveParticulas, value ? 1 : 0);
            AplicarEnTodas();
        }
    }

    public static bool PostProcesoPermitido
    {
        get => PlayerPrefs.GetInt(ClavePostProceso, 1) == 1;
        set
        {
            PlayerPrefs.SetInt(ClavePostProceso, value ? 1 : 0);
            AplicarEnTodas();
        }
    }

    public static bool VolumetricosPermitidos
    {
        get => PlayerPrefs.GetInt(ClaveVolumetricos, 1) == 1;
        set
        {
            PlayerPrefs.SetInt(ClaveVolumetricos, value ? 1 : 0);
            AplicarEnTodas();
        }
    }

    public static void AplicarEnTodas()
    {
        AtmosferaEscena[] todas = FindObjectsByType<AtmosferaEscena>(FindObjectsSortMode.None);
        for (int i = 0; i < todas.Length; i++)
            todas[i].Aplicar();
    }

    void OnEnable()
    {
        Aplicar();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall += AplicarSiExiste;
    }

    void AplicarSiExiste()
    {
        if (this != null)
            Aplicar();
    }
#endif

    public void Aplicar()
    {
        int calidad = QualitySettings.GetQualityLevel();
        bool enJuego = Application.isPlaying;

        bool usarParticulas = particulas
            && (!enJuego || ParticulasPermitidas)
            && !Contiene(calidadesSinParticulas, calidad);

        bool usarPostProceso = postProceso
            && (!enJuego || PostProcesoPermitido)
            && !Contiene(calidadesSinPostProceso, calidad);

        bool usarVolumetricos = volumetricos
            && (!enJuego || VolumetricosPermitidos)
            && !Contiene(calidadesSinVolumetricos, calidad);

        Activar(objetosParticulas, usarParticulas);
        Activar(objetosVolumetricos, usarVolumetricos);

        for (int i = 0; i < volumenes.Length; i++)
        {
            if (volumenes[i] != null)
                volumenes[i].enabled = usarPostProceso;
        }

        if (enJuego && apagarPostProcesoCamara && Camera.main != null)
            Camera.main.GetUniversalAdditionalCameraData().renderPostProcessing = usarPostProceso;

        if (RenderSettings.fog != nieblaUnity)
            RenderSettings.fog = nieblaUnity;

        Shader.SetGlobalFloat(IdSuave, ProfundidadDisponible() ? 1f : 0f);
    }

    static void Activar(GameObject[] objetos, bool activo)
    {
        if (objetos == null)
            return;

        for (int i = 0; i < objetos.Length; i++)
        {
            GameObject objeto = objetos[i];
            if (objeto != null && objeto.activeSelf != activo)
                objeto.SetActive(activo);
        }
    }

    static bool ProfundidadDisponible()
    {
        UniversalRenderPipelineAsset asset = UniversalRenderPipeline.asset;
        return asset != null && asset.supportsCameraDepthTexture;
    }

    static bool Contiene(int[] lista, int valor)
    {
        if (lista == null)
            return false;

        for (int i = 0; i < lista.Length; i++)
        {
            if (lista[i] == valor)
                return true;
        }

        return false;
    }
}
