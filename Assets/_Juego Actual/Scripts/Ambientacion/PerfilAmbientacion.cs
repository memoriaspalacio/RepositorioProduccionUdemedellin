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
    [Tooltip("Nombres de objetos cuyos renderers (y los de sus hijos) se marcan como estaticos para el horneado.")]
    public string[] objetosEstaticos = { };
    [Tooltip("Rutas de modelos (.fbx) a los que se les activa Generate Lightmap UVs.")]
    public string[] modelosLightmap = { };

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
    [Tooltip("Altura sobre el suelo de la niebla exterior.")]
    public float alturaExterior = 1.5f;
    [Tooltip("Radio de la niebla exterior respecto al tamano de la escena.")]
    [Range(0.5f, 4f)] public float radioExterior = 2f;
    [Tooltip("Motas de polvo o brasas flotando. 0 = sin polvo.")]
    public int particulasPolvo = 0;
    public Material materialPolvo;
    [ColorUsage(false, true)] public Color colorPolvo = new Color(3f, 2.2f, 1.2f);
    public Vector2 tamanoPolvo = new Vector2(0.04f, 0.1f);
    public float alturaPolvo = 4f;
    [Tooltip("Velocidad de subida del polvo (minimo y maximo). Brasas: mas rapido.")]
    public Vector2 subidaPolvo = new Vector2(0.04f, 0.18f);
    public Vector2 viento = new Vector2(0.15f, 0.05f);

    [Header("Zonas vivas (luciernagas alrededor de objetos)")]
    public ZonaViva[] zonasVivas = { };
    public Material materialLuciernagas;
    [ColorUsage(false, true)] public Color colorLuciernagas = new Color(5f, 3.6f, 1.6f);

    [Header("Luz de personajes (solo los afecta a ellos)")]
    public bool luzPersonajes = true;
    public Color colorRellenoPersonajes = new Color(1f, 0.95f, 0.88f);
    [Range(0f, 4f)] public float intensidadRellenoPersonajes = 0.6f;
    public Color colorContornoPersonajes = new Color(0.65f, 0.8f, 1f);
    [Range(0f, 8f)] public float intensidadContornoPersonajes = 1.8f;

    [Header("Ojos brillantes de calaveras")]
    [Tooltip("Datos con la posicion de cada ojo (OjosCalaveras.json). Vacio = sin ojos.")]
    public TextAsset datosOjos;
    public Material materialOjos;
    [ColorUsage(false, true)] public Color colorOjos = new Color(0.5f, 1.3f, 4f);
    [Range(0.2f, 3f)] public float tamanoOjos = 1f;
    public bool lucesPilares = true;
    public Color colorLucesPilares = new Color(0.4f, 0.6f, 1f);
    [Range(0f, 10f)] public float intensidadLucesPilares = 2.5f;
    [Range(0.5f, 10f)] public float rangoLucesPilares = 4.5f;

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
    public ModoHaces modo = ModoHaces.Ventanas;
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

    [Header("Solo modo Cielo")]
    [Range(1, 40)] public int cantidadCielo = 8;
    public float alturaCielo = 10f;
    public Vector2 anchoCielo = new Vector2(1.5f, 3f);
    public int semilla = 7;

    [Header("Luz horneada en el piso (solo modo Ventanas)")]
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

[System.Serializable]
public class ZonaViva
{
    public string prefijoObjeto = "armario";
    [Range(0, 60)] public int luciernagas = 14;
    [Tooltip("Metros alrededor del objeto donde revolotean.")]
    public float radio = 1.5f;
    public Vector2 tamano = new Vector2(0.06f, 0.14f);
    [Tooltip("Nacen en las luces hijas del objeto (por ejemplo, las llamas de las antorchas) en vez de alrededor de todo el objeto.")]
    public bool emitirDesdeLuces;
    [Tooltip("Velocidad maxima de subida. Luciernagas: 0.06. Brasas: 0.6 o mas.")]
    public float velocidadSubida = 0.06f;
    [Tooltip("Cuantas luciernagas llevan una luz real que ilumina lo que tienen cerca.")]
    [Range(0, 8)] public int lucesReales = 4;
    public Color colorLuz = new Color(1f, 0.72f, 0.35f);
    [Range(0f, 10f)] public float intensidadLuz = 1.6f;
    [Range(0.5f, 6f)] public float rangoLuz = 2.2f;
    [Tooltip("Agrega un haz del cielo que cae delante del objeto (solo con haces en modo Cielo).")]
    public bool hazDelCielo = true;
    public float anchoHaz = 2.2f;
}

public enum ModoHaces
{
    Ventanas,
    Cielo
}
