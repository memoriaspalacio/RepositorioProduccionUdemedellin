using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Indicador circular en el suelo para el personaje en juego de ritmo:
/// sigue sus pies, se alinea con el suelo bajo ellos y se rellena en sentido
/// horario sincronizado con el compás, con feedback de acierto/fallo.
/// </summary>
[ExecuteAlways]
[SelectionBase]
[DisallowMultipleComponent]
public class RhythmGroundIndicator : MonoBehaviour
{
    [Header("--- Referencias del Ritmo ---")]
    [Tooltip("Referencia al juez de ritmo de la escena. Si es nulo, se busca automáticamente.")]
    [SerializeField] private BeatActionJudge beatJudge;

    [Tooltip("Referencia al gestor del compás musical. Si es nulo, usa BeatManager.Instance.")]
    [SerializeField] private BeatManager beatManager;

    [Header("--- Seguimiento del Personaje ---")]
    [Tooltip("Transform del personaje a seguir. Si es nulo y este objeto es hijo, sigue al padre; si no, a sí mismo.")]
    [SerializeField] private Transform followTarget;

    [Tooltip("Distancia vertical desde el pivote del personaje hacia los pies.")]
    [SerializeField] private float feetOffset = 0.8f;

    [Tooltip("Diámetro del círculo indicador en unidades de mundo.")]
    [Range(0.2f, 6.0f)]
    [SerializeField] private float circleDiameter = 2.0f;

    [Tooltip("Elevación en Y sobre la superficie del suelo, para evitar Z-fighting.")]
    [Range(-1.0f, 2.0f)]
    [SerializeField] private float groundYOffset = 0.05f;

    [Tooltip("Alinea el plano del círculo con la pendiente del terreno detectada por Raycast.")]
    [SerializeField] private bool alignWithGroundNormal = true;

    [Tooltip("Capas consideradas suelo para el Raycast.")]
    [SerializeField] private LayerMask groundLayer = ~0;

    [Header("--- Estilo Visual y Material ---")]
    [Tooltip("Material con el shader Custom/RhythmCircleIndicator. Si es nulo, se genera o carga automáticamente.")]
    [SerializeField] private Material indicatorMaterial;

    [Tooltip("Renderer del Quad visual. Si no se asigna, se autodetecta o creará uno automáticamente.")]
    [SerializeField] private MeshRenderer visualRenderer;

    [Header("--- Sincronización del Relleno (Timing) ---")]
    [Tooltip("Duración en beats sobre la cual el círculo se llena antes de llegar al beat de impacto.")]
    [Range(0.25f, 4.0f)]
    [SerializeField] private float fillDurationBeats = 1.0f;

    [Tooltip("Si está activo, adapta la duración del llenado al intervalo de notas configurado en BeatActionJudge.")]
    [SerializeField] private bool adaptToNoteInterval = true;

    [Header("--- Retroalimentación de Golpe (Hit Feedback) ---")]
    [Tooltip("Duración en segundos de la onda expansiva y destello visual al acertar una nota.")]
    [Range(0.1f, 0.8f)]
    [SerializeField] private float hitFeedbackDuration = 0.32f;

    [Tooltip("Multiplicador de escala de impacto físico (punch scale) en el suelo.")]
    [Range(1.0f, 1.5f)]
    [SerializeField] private float hitPunchScale = 1.2f;

    [Header("--- Retroalimentación de Fallo (Miss Feedback) ---")]
    [Tooltip("Si está activo, muestra un breve destello rojo cuando el jugador presiona fuera de tiempo (whiff).")]
    [SerializeField] private bool enableMissFeedback = true;

    [Tooltip("Duración en segundos del feedback de fallo.")]
    [Range(0.05f, 0.5f)]
    [SerializeField] private float missFeedbackDuration = 0.22f;

    [Header("--- Eventos Rítmicos ---")]
    public UnityEvent OnNoteHitSuccess;
    public UnityEvent OnNoteHitMiss;

    // Estado interno
    private MaterialPropertyBlock propertyBlock;
    private GameObject visualChildObj;
    private Transform visualTransform;
    private static Mesh cachedQuadMesh;
    private CharacterController cachedCharacterController;
    private Quaternion currentSlopeRotation = Quaternion.identity;

    private float currentFillProgress = 0f;
    private float currentWindowOpen = 0f;
    private float hitFeedbackTimer = 0f;
    private float missFeedbackTimer = 0f;
    private float currentPunchFactor = 1.0f;

    private static readonly int FillProgressProp = Shader.PropertyToID("_FillProgress");
    private static readonly int WindowOpenProp = Shader.PropertyToID("_WindowOpen");
    private static readonly int HitFeedbackProp = Shader.PropertyToID("_HitFeedback");
    private static readonly int MissFeedbackProp = Shader.PropertyToID("_MissFeedback");
    private static readonly int BeatPulseProp = Shader.PropertyToID("_BeatPulse");

    private void Awake()
    {
        ResolveTargetReferences();
        ResolveRhythmReferences();
        EnsureVisualQuad();
    }

    private void OnEnable()
    {
        ResolveTargetReferences();
        ResolveRhythmReferences();
        EnsureVisualQuad();

        if (Application.isPlaying)
        {
            SubscribeEvents();
        }
    }

    private void OnDisable()
    {
        if (Application.isPlaying)
        {
            UnsubscribeEvents();
        }
    }

    private void Start()
    {
        ResolveTargetReferences();
        ResolveRhythmReferences();
        EnsureVisualQuad();
    }

    private void ResolveTargetReferences()
    {
        if (followTarget == null)
        {
            if (transform.parent != null)
            {
                if (transform.parent.GetComponent<GridPlayerMovement>() != null || transform.parent.CompareTag("Player"))
                {
                    followTarget = transform.parent;
                }
            }

            if (followTarget == null)
            {
                GridPlayerMovement playerMovement = FindFirstObjectByType<GridPlayerMovement>();
                if (playerMovement != null)
                {
                    followTarget = playerMovement.transform;
                }
                else
                {
                    GameObject playerObj = GameObject.FindWithTag("Player");
                    if (playerObj != null)
                        followTarget = playerObj.transform;
                }
            }

            if (followTarget == null)
            {
                followTarget = transform;
            }
        }

        if (followTarget != null)
        {
            cachedCharacterController = followTarget.GetComponent<CharacterController>();
        }
    }

    private void ResolveRhythmReferences()
    {
        if (beatJudge == null)
            beatJudge = FindFirstObjectByType<BeatActionJudge>();

        if (beatManager == null)
            beatManager = BeatManager.Instance != null ? BeatManager.Instance : FindFirstObjectByType<BeatManager>();
    }

    private void SubscribeEvents()
    {
        if (beatJudge != null)
        {
            beatJudge.OnBeatConsumed -= HandleBeatConsumed;
            beatJudge.OnBeatConsumed += HandleBeatConsumed;

            beatJudge.OnNoteDestroyed -= HandleNoteDestroyed;
            beatJudge.OnNoteDestroyed += HandleNoteDestroyed;
        }
    }

    private void UnsubscribeEvents()
    {
        if (beatJudge != null)
        {
            beatJudge.OnBeatConsumed -= HandleBeatConsumed;
            beatJudge.OnNoteDestroyed -= HandleNoteDestroyed;
        }
    }

    /// <summary>
    /// Crea o garantiza la existencia del Quad visual del indicador en el suelo.
    /// </summary>
    public void EnsureVisualQuad()
    {
        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        if (visualRenderer == null)
        {
            visualRenderer = GetComponent<MeshRenderer>();
            if (visualRenderer != null)
            {
                visualTransform = transform;
                visualChildObj = gameObject;
            }
        }

        if (visualRenderer == null)
        {
            Transform existingChild = transform.Find("RhythmGroundVisual");
            if (existingChild != null)
            {
                visualChildObj = existingChild.gameObject;
                visualRenderer = visualChildObj.GetComponent<MeshRenderer>();
                visualTransform = visualChildObj.transform;
            }
        }

        if (visualRenderer == null)
        {
            visualChildObj = new GameObject("RhythmGroundVisual");
            visualChildObj.transform.SetParent(transform, false);
            visualTransform = visualChildObj.transform;

            visualChildObj.layer = 2; // Ignore Raycast

            MeshFilter mf = visualChildObj.AddComponent<MeshFilter>();
            mf.sharedMesh = GetOrCreateHorizontalQuadMesh();

            visualRenderer = visualChildObj.AddComponent<MeshRenderer>();
            visualRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            visualRenderer.receiveShadows = false;
            visualRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            visualRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }
        else
        {
            visualTransform = visualRenderer.transform;
            visualChildObj = visualRenderer.gameObject;
        }

        if (indicatorMaterial == null && visualRenderer.sharedMaterial == null)
        {
            Shader circleShader = Shader.Find("Custom/RhythmCircleIndicator");
            if (circleShader != null)
            {
                indicatorMaterial = new Material(circleShader) { name = "M_RhythmCircleIndicator_Runtime" };
                visualRenderer.sharedMaterial = indicatorMaterial;
            }
        }
        else if (indicatorMaterial != null && visualRenderer.sharedMaterial != indicatorMaterial)
        {
            visualRenderer.sharedMaterial = indicatorMaterial;
        }
    }

    /// <summary>
    /// Genera una malla Quad horizontal (plano XZ mirando hacia +Y) con coordenadas UV completas.
    /// </summary>
    private static Mesh GetOrCreateHorizontalQuadMesh()
    {
        if (cachedQuadMesh != null)
            return cachedQuadMesh;

        Mesh mesh = new Mesh
        {
            name = "RhythmHorizontalQuadMesh",
            hideFlags = HideFlags.DontSave
        };

        Vector3[] vertices = new Vector3[]
        {
            new Vector3(-0.5f, 0f, -0.5f),
            new Vector3( 0.5f, 0f, -0.5f),
            new Vector3( 0.5f, 0f,  0.5f),
            new Vector3(-0.5f, 0f,  0.5f)
        };

        Vector2[] uvs = new Vector2[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f)
        };

        int[] triangles = new int[]
        {
            0, 2, 1,
            0, 3, 2
        };

        Vector3[] normals = new Vector3[]
        {
            Vector3.up,
            Vector3.up,
            Vector3.up,
            Vector3.up
        };

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.normals = normals;
        mesh.RecalculateBounds();

        cachedQuadMesh = mesh;
        return cachedQuadMesh;
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            EnsureVisualQuad();
            UpdatePositionAndOrientation();

            if (visualRenderer != null)
            {
                if (propertyBlock == null)
                    propertyBlock = new MaterialPropertyBlock();

                visualRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetFloat(FillProgressProp, 0.75f);
                propertyBlock.SetFloat(WindowOpenProp, 1.0f);
                propertyBlock.SetFloat(HitFeedbackProp, 0.0f);
                propertyBlock.SetFloat(MissFeedbackProp, 0.0f);
                propertyBlock.SetFloat(BeatPulseProp, 0.5f);
                visualRenderer.SetPropertyBlock(propertyBlock);
            }
            return;
        }

        UpdateRhythmState();
        UpdateFeedbackDecay();
        ApplyShaderProperties();
        UpdatePositionAndOrientation();
    }

    private void LateUpdate()
    {
        // Se ejecuta también en LateUpdate para evitar desfasaje con el movimiento del personaje
        UpdatePositionAndOrientation();
    }

    /// <summary>
    /// Calcula el progreso de llenado antes de la próxima nota y el estado de la ventana de acción.
    /// </summary>
    private void UpdateRhythmState()
    {
        if (beatManager == null)
            beatManager = BeatManager.Instance;

        if (beatManager == null || !beatManager.IsPlaying)
        {
            currentFillProgress = 0f;
            currentWindowOpen = 0f;
            return;
        }

        float nowBeat = (float)(beatManager.CurrentSongTime / beatManager.SecPerBeat);

        if (beatJudge != null)
        {
            float earlyTol = beatJudge.EarlyTolerance;
            float lateTol = beatJudge.LateTolerance;

            float interval = adaptToNoteInterval ? Mathf.Max(0.5f, (float)beatJudge.NoteInterval) : fillDurationBeats;

            float targetBeat = -1f;
            float startScan = Mathf.Floor((nowBeat - lateTol) * 2f) / 2f;
            float endScan = nowBeat + interval + 1f;

            for (float b = startScan; b <= endScan; b += 0.5f)
            {
                if (b < (nowBeat - lateTol)) continue;
                if (!beatJudge.IsNoteBeat(b)) continue;
                if (beatJudge.IsBeatResolved(b)) continue;

                targetBeat = b;
                break;
            }

            if (targetBeat >= 0f)
            {
                float beatsUntilTarget = targetBeat - nowBeat;

                if (beatsUntilTarget <= 0f)
                {
                    currentFillProgress = 1.0f;
                }
                else
                {
                    currentFillProgress = 1.0f - Mathf.Clamp01(beatsUntilTarget / interval);
                }

                bool isWindowOpen = (beatsUntilTarget <= earlyTol) && ((nowBeat - targetBeat) <= lateTol);
                currentWindowOpen = isWindowOpen ? 1.0f : 0.0f;
            }
            else
            {
                currentFillProgress = 0f;
                currentWindowOpen = 0f;
            }
        }
        else
        {
            float cycle = (float)(nowBeat % 1.0);
            currentFillProgress = cycle;
            currentWindowOpen = cycle > 0.8f ? 1.0f : 0.0f;
        }
    }

    /// <summary>
    /// Decae las intensidades de hit feedback, miss feedback y el rebote físico.
    /// </summary>
    private void UpdateFeedbackDecay()
    {
        if (hitFeedbackTimer > 0f)
        {
            hitFeedbackTimer = Mathf.MoveTowards(hitFeedbackTimer, 0f, Time.deltaTime / Mathf.Max(0.01f, hitFeedbackDuration));
            float normalizedT = 1.0f - hitFeedbackTimer; // de 0 a 1
            currentPunchFactor = Mathf.Lerp(hitPunchScale, 1.0f, normalizedT);
        }
        else
        {
            currentPunchFactor = 1.0f;
        }

        if (missFeedbackTimer > 0f)
        {
            missFeedbackTimer = Mathf.MoveTowards(missFeedbackTimer, 0f, Time.deltaTime / Mathf.Max(0.01f, missFeedbackDuration));
        }
    }

    /// <summary>
    /// Envía todos los valores dinámicos al shader a través de MaterialPropertyBlock (cero basura en memoria).
    /// </summary>
    private void ApplyShaderProperties()
    {
        if (visualRenderer == null) return;

        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        visualRenderer.GetPropertyBlock(propertyBlock);

        propertyBlock.SetFloat(FillProgressProp, currentFillProgress);
        propertyBlock.SetFloat(WindowOpenProp, currentWindowOpen);
        propertyBlock.SetFloat(HitFeedbackProp, hitFeedbackTimer);
        propertyBlock.SetFloat(MissFeedbackProp, missFeedbackTimer);

        float beatPulse = 0f;
        if (beatManager != null && beatManager.IsPlaying)
        {
            double nowBeat = beatManager.CurrentSongTime / beatManager.SecPerBeat;
            beatPulse = (float)(1.0 - (nowBeat % 1.0));
        }
        propertyBlock.SetFloat(BeatPulseProp, beatPulse);

        visualRenderer.SetPropertyBlock(propertyBlock);
    }

    /// <summary>
    /// Posiciona el indicador en los pies del personaje, alineándolo con el suelo detectado por Raycast.
    /// </summary>
    private void UpdatePositionAndOrientation()
    {
        if (visualTransform == null) return;

        Transform targetT = followTarget != null ? followTarget : transform;
        Vector3 targetPos = targetT.position;

        float calculatedFeetY = targetPos.y - feetOffset;

        if (cachedCharacterController != null)
        {
            calculatedFeetY = targetPos.y + cachedCharacterController.center.y - (cachedCharacterController.height * 0.5f);
        }
        else if (targetT != null && targetT.TryGetComponent<CharacterController>(out var cc))
        {
            cachedCharacterController = cc;
            calculatedFeetY = targetPos.y + cc.center.y - (cc.height * 0.5f);
        }

        float finalGroundY = calculatedFeetY + groundYOffset;
        Vector3 groundNormal = Vector3.up;

        int playerLayer = targetT != null ? targetT.gameObject.layer : gameObject.layer;
        int ignoreMask = (1 << playerLayer) | (1 << 2); // 2 = Ignore Raycast
        int effectiveLayerMask = groundLayer.value & ~ignoreMask;

        Vector3 rayStart = new Vector3(targetPos.x, calculatedFeetY + 0.35f, targetPos.z);

        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 3.5f, effectiveLayerMask, QueryTriggerInteraction.Ignore))
        {
            finalGroundY = hit.point.y + groundYOffset;
            groundNormal = hit.normal;
        }

        visualTransform.position = new Vector3(targetPos.x, finalGroundY, targetPos.z);

        if (alignWithGroundNormal)
        {
            Quaternion targetSlope = Quaternion.FromToRotation(Vector3.up, groundNormal);
            currentSlopeRotation = Application.isPlaying
                ? Quaternion.Slerp(currentSlopeRotation, targetSlope, Time.deltaTime * 18f)
                : targetSlope;
            visualTransform.rotation = currentSlopeRotation;
        }
        else
        {
            visualTransform.rotation = Quaternion.identity;
        }

        float finalSize = circleDiameter * currentPunchFactor;
        visualTransform.localScale = new Vector3(finalSize, 1.0f, finalSize);
    }

    #region Manejo de Eventos de Acierto y Fallo

    private void HandleBeatConsumed(float beat)
    {
        TriggerHitFeedback();
    }

    /// <summary>
    /// Dispara manualmente la retroalimentación visual de acierto (flash, onda expansiva y rebote).
    /// </summary>
    public void TriggerHitFeedback()
    {
        hitFeedbackTimer = 1.0f;
        OnNoteHitSuccess?.Invoke();
    }

    private void HandleNoteDestroyed(float beat, DestroyReason reason)
    {
        if (enableMissFeedback && reason == DestroyReason.Penalized)
        {
            TriggerMissFeedback();
        }
    }

    /// <summary>
    /// Dispara manualmente el feedback visual de penalización o fallo (whiff).
    /// </summary>
    public void TriggerMissFeedback()
    {
        missFeedbackTimer = 1.0f;
        OnNoteHitMiss?.Invoke();
    }

    #endregion

    #region Configuración en Editor

    private void OnValidate()
    {
        if (circleDiameter < 0.2f) circleDiameter = 0.2f;
        hitFeedbackDuration = Mathf.Max(0.05f, hitFeedbackDuration);
        missFeedbackDuration = Mathf.Max(0.05f, missFeedbackDuration);

        if (visualRenderer != null && indicatorMaterial != null)
        {
            visualRenderer.sharedMaterial = indicatorMaterial;
        }
    }

    #endregion
}
