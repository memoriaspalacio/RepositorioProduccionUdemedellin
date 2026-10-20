using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(fileName = "Perfil_Ambientacion", menuName = "UdeM/Ambientacion/Perfil de ambientacion")]
public class PerfilAmbientacion : ScriptableObject
{
    [Header("Escena")]
    public string rutaEscena;
    [Tooltip("Prefijos de los objetos que delimitan el area jugable (vallas, muros). Se usan para ubicar la niebla y las sondas.")]
    public string[] prefijosLimite = { };
    [Tooltip("Nombres exactos de objetos de iluminaciones anteriores que se desactivan al aplicar.")]
    public string[] desactivarObjetos = { };

    [Header("Cielo")]
    public Material skybox;
    [Range(0f, 2f)] public float intensidadReflejos = 0.5f;

    [Header("Luz ambiental (degradado)")]
    [ColorUsage(false, true)] public Color ambienteCielo = new Color(0.16f, 0.2f, 0.33f);
    [ColorUsage(false, true)] public Color ambienteHorizonte = new Color(0.08f, 0.1f, 0.14f);
    [ColorUsage(false, true)] public Color ambienteSuelo = new Color(0.03f, 0.04f, 0.035f);

    [Header("Luna (luz direccional)")]
    public Color colorLuna = new Color(0.58f, 0.68f, 1f);
    [Range(0f, 8f)] public float intensidadLuna = 1.1f;
    [Range(5f, 90f)] public float elevacionLuna = 32f;
    [Range(-180f, 180f)] public float azimutLuna = 15f;
    [Range(0f, 1f)] public float fuerzaSombras = 0.75f;
    public LightmapBakeType modoLuna = LightmapBakeType.Mixed;

    [Header("Niebla de Unity")]
    public bool niebla = true;
    public FogMode modoNiebla = FogMode.ExponentialSquared;
    public Color colorNiebla = new Color(0.085f, 0.11f, 0.15f);
    [Range(0f, 0.2f)] public float densidadNiebla = 0.025f;

    [Header("Luces de acento")]
    public LuzAcento[] lucesAcento = { };

    [Header("Materiales que brillan")]
    public MaterialEmisivo[] materialesEmisivos = { };

    [Header("Haces de luz por las ventanas")]
    public HacesDeLuz haces = new HacesDeLuz();

    [Header("Niebla de particulas")]
    public bool nieblaParticulas = true;
    public Material materialNiebla;
    public Color colorParticulas = new Color(0.55f, 0.64f, 0.78f);
    public float alturaNieblaSuelo = 1.2f;
    public int particulasSuelo = 70;
    public Vector2 tamanoSuelo = new Vector2(5f, 9f);
    [Range(0f, 1f)] public float opacidadSuelo = 0.3f;
    public int particulasExterior = 50;
    public Vector2 tamanoExterior = new Vector2(10f, 18f);
    [Range(0f, 1f)] public float opacidadExterior = 0.45f;
    public Vector2 viento = new Vector2(0.15f, 0.05f);

    [Header("Post proceso")]
    public bool postProceso = true;
    public VolumeProfile perfilVolumen;

    [Header("Horneado")]
    public LightingSettings ajustesHorneado;
    [Range(2f, 60f)] public float texelesPorUnidad = 16f;
    public int tamanoAtlas = 2048;
    public bool oclusionAmbiental = true;
    [Range(0, 4)] public int rebotes = 2;
    public float separacionSondas = 3f;
    public float[] alturasSondas = { 0.3f, 1.5f, 3f };

    [Header("Rendimiento por plataforma")]
    [Tooltip("Indices de Quality Settings donde se apagan las particulas (0 = Mobile, 1 = PC).")]
    public int[] calidadesSinParticulas = { };
    [Tooltip("Indices de Quality Settings donde se apaga el post proceso (0 = Mobile, 1 = PC).")]
    public int[] calidadesSinPostProceso = { };
    [Tooltip("Indices de Quality Settings donde se apagan los haces de luz (0 = Mobile, 1 = PC).")]
    public int[] calidadesSinVolumetricos = { };
}

[System.Serializable]
public class MaterialEmisivo
{
    public Material material;
    [ColorUsage(false, true)] public Color color = Color.white;
    [Tooltip("Usa la textura base como mapa de emision (brilla con sus propios colores).")]
    public bool usarTextura = true;
}

[System.Serializable]
public class HacesDeLuz
{
    public bool activo;
    public Material material;
    [Tooltip("Los objetos con este material se consideran ventanas.")]
    public Material materialVentanas;
    [ColorUsage(false, true)] public Color color = new Color(0.55f, 0.7f, 1f);
    [Range(0f, 2f)] public float opacidad = 0.12f;
    [Range(1f, 30f)] public float largo = 9f;
    [Tooltip("Grados hacia abajo desde la horizontal.")]
    [Range(0f, 80f)] public float inclinacion = 38f;
    [Tooltip("Cuanto se abre el haz al final (1 = no se abre).")]
    [Range(1f, 4f)] public float apertura = 1.8f;

    [Header("Luz horneada en el piso")]
    public bool crearLuces = true;
    [Range(0f, 200f)] public float intensidadLuz = 30f;
    [Range(1f, 40f)] public float rangoLuz = 13f;
    [Range(5f, 170f)] public float anguloLuz = 50f;
}

[System.Serializable]
public class LuzAcento
{
    public string prefijoObjeto = "lampara";
    public string[] excluirPrefijos = { };
    public Color color = Color.white;
    [Range(0f, 30f)] public float intensidad = 5f;
    [Range(0.5f, 30f)] public float rango = 8f;
    [Tooltip("Solo para luces nuevas: 0 = centro del objeto, 1 = parte superior.")]
    [Range(0f, 1.5f)] public float alturaRelativa = 0.7f;
    [Tooltip("Solo para luces nuevas: metros que se separa hacia el centro de la escena (para que no quede dentro de una pared).")]
    [Range(0f, 3f)] public float separacionInterior = 0f;
    public LightmapBakeType modo = LightmapBakeType.Baked;
    [Tooltip("Brillo del material del objeto. 0 = no tocar el material.")]
    [Range(0f, 10f)] public float emision = 0f;
}
