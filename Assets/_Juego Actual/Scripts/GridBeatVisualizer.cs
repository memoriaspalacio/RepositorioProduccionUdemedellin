using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
public class GridBeatVisualizer : MonoBehaviour
{
    [Header("Referencias (Auto-configuración)")]
    [Tooltip("El script buscará automáticamente tu MeshGrid para ajustar el shader al tamaño correcto de las casillas.")]
    public MeshGrid meshGrid;
    
    [Header("Configuración del Pulso Rítmico")]
    [Tooltip("Intensidad cuando no hay beat (ej. 0.05 para que sea casi invisible)")]
    [Range(0f, 1f)] public float minPulse = 0.05f;
    [Tooltip("Intensidad máxima en el impacto del beat")]
    [Range(0f, 1f)] public float maxPulse = 0.8f;
    
    [Tooltip("Curva del destello. 0 = Apagado, 1 = Brillante. (El eje X va de 0 a 1 Beat)")]
    public AnimationCurve pulseCurve;

    [Header("Alineación de Cuadrícula")]
    [Tooltip("Alinea la cuadrícula brillante exactamente con las esquinas del terreno 3D.")]
    public bool autoAlignWithTerrain = true;
    [Tooltip("Usa esto para mover libremente las líneas (desfase X y Z) si apagas la auto-alineación.")]
    public Vector2 manualGridOffset = Vector2.zero;

    private Material gridMaterial;
    private Vector2 autoGridOrigin;
    private static readonly int GridSizeProp = Shader.PropertyToID("_GridSize");
    private static readonly int BeatPulseProp = Shader.PropertyToID("_BeatPulse");

    private void Start()
    {
        // Obtenemos el material del plano donde está puesto este script
        gridMaterial = GetComponent<MeshRenderer>().material;
        
        // Si no asignaron la grid manualmente, la buscamos
        if (meshGrid == null)
            meshGrid = FindFirstObjectByType<MeshGrid>();

        // Pre-calculamos el origen real del mundo para la alineación automática
        if (meshGrid != null)
        {
            gridMaterial.SetFloat(GridSizeProp, meshGrid.CellSize);
            
            Renderer terrainRenderer = meshGrid.GetComponent<Renderer>();
            if (terrainRenderer == null)
                terrainRenderer = meshGrid.GetComponentInChildren<Renderer>();

            if (terrainRenderer != null)
            {
                Vector3 boundsMin = terrainRenderer.bounds.min;
                autoGridOrigin = new Vector2(boundsMin.x, boundsMin.z);
            }
            else
            {
                autoGridOrigin = new Vector2(meshGrid.transform.position.x, meshGrid.transform.position.z);
            }
        }

        // Curva rítmica por defecto: Golpe de luz fuerte y se apaga rápido
        if (pulseCurve == null || pulseCurve.keys.Length == 0)
        {
            pulseCurve = new AnimationCurve(
                new Keyframe(0.0f, 1.0f),    // Máximo brillo en el instante del beat
                new Keyframe(0.3f, 0.0f),    // Se apaga rápidamente
                new Keyframe(1.0f, 0.0f)     // Permanece apagado hasta el próximo beat
            );
        }
    }

    private void Update()
    {
        // 1. Aplicamos el Origen (Permite ver los cambios manuales en tiempo real en Play Mode)
        if (autoAlignWithTerrain)
        {
            gridMaterial.SetVector("_GridOrigin", new Vector4(autoGridOrigin.x, autoGridOrigin.y, 0, 0));
        }
        else
        {
            gridMaterial.SetVector("_GridOrigin", new Vector4(manualGridOffset.x, manualGridOffset.y, 0, 0));
        }

        // 2. Pulso Rítmico
        if (BeatManager.Instance == null || !BeatManager.Instance.IsPlaying) 
            return;

        // Calculamos en qué punto exacto del beat estamos actualmente
        double songTime = BeatManager.Instance.CurrentSongTime;
        double beatsPassed = songTime / BeatManager.Instance.SecPerBeat;
        float percent = (float)(beatsPassed % 1.0);

        // Evaluamos la curva para obtener un valor entre 0 y 1
        float curveValue = pulseCurve.Evaluate(percent);
        
        // Lo mapeamos entre el brillo mínimo y máximo configurado
        float currentPulse = Mathf.Lerp(minPulse, maxPulse, curveValue);
        
        // Enviamos la instrucción de parpadeo directo a la tarjeta gráfica (al shader)
        gridMaterial.SetFloat(BeatPulseProp, currentPulse);
    }
}
