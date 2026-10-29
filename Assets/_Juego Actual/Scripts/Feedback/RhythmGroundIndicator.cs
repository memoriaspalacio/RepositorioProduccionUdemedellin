using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Indicador de ritmo proyectado en el suelo, bajo los pies del personaje.
///
/// Usa el estándar de los juegos de ritmo (osu! / NecroDancer / Beat Saber): un anillo
/// de aproximación se contrae desde afuera hacia un anillo objetivo fijo, y cuando ambos
/// coinciden es el momento de pulsar. El movimiento convergente se lee con visión
/// periférica, así el jugador no necesita mirarse los pies.
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

    [Tooltip("Diámetro total del indicador en unidades de mundo. El anillo objetivo ocupa " +
             "cerca de un 40% de este valor; el resto es el recorrido del anillo de aproximación.")]
    [Range(0.2f, 6.0f)]
    [SerializeField] private float circleDiameter = 2.8f;

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

    [Tooltip("Si está activo, adapta la duración del llenado al intervalo de notas configurado en BeatActionJudge. " +
             "Con un chart de notas no hay intervalo fijo y se usa fillDurationBeats.")]
    [SerializeField] private bool adaptToNoteInterval = true;

    [Tooltip("Si la nota siguiente cae a esta distancia en beats o menos, el anillo objetivo late " +
             "para avisar que viene otra pegada detrás.")]
    [Range(0.5f, 4.0f)]
    [SerializeField] private float nextNoteWarnBeats = 1.0f;

    [Header("--- Retroalimentación de Golpe (Hit Feedback) ---")]
    [Tooltip("Duración en segundos de la onda expansiva y destello visual al acertar una nota.")]
    [Range(0.1f, 0.8f)]
    [SerializeField] private float hitFeedbackDuration = 0.32f;

    [Tooltip("Multiplicador de escala de impacto físico (punch scale) en el suelo.")]
    [Range(1.0f, 1.5f)]
    [SerializeField] private float hitPunchScale = 1.2f;

    [Header("--- Retroalimentación de Fallo (Miss Feedback) ---")]
    [Tooltip("Si está activo, los anillos destellan con el color de fallo y se encogen un poco cada vez " +
             "que el juez cobra una penalización de tiempo (presionar fuera de tiempo o recibir un golpe).")]
    [SerializeField] private bool enableMissFeedback = true;

    [Tooltip("Duración en segundos del feedback de fallo.")]
    [Range(0.05f, 0.5f)]
    [SerializeField] private float missFeedbackDuration = 0.22f;

    [Tooltip("Escala a la que se encoge el indicador al fallar (1 = no se encoge). Es lo contrario " +
             "del rebote de acierto, para que el fallo se sienta como un golpe hacia adentro.")]
    [Range(0.7f, 1.0f)]
    [SerializeField] private float missShrinkScale = 0.88f;

    [Header("--- Retroalimentación de Daño Recibido ---")]
    [Tooltip("Duración en segundos del feedback al recibir daño.")]
    [Range(0.1f, 1.2f)]
    [SerializeField] private float damageFeedbackDuration = 0.55f;

    [Header("--- Luz de Retroalimentación ---")]
    [Tooltip("Enciende una luz puntual en los pies al acertar una nota o al recibir daño.")]
    [SerializeField] private bool enableFeedbackLight = true;

    [Tooltip("Color del destello de luz al acertar.")]
    [SerializeField] private Color hitLightColor = new Color(1f, 0.78f, 0.25f);

    [Tooltip("Color del destello de luz al recibir daño.")]
    [SerializeField] private Color damageLightColor = new Color(0.95f, 0.06f, 0.12f);

    [Tooltip("Intensidad máxima del destello.")]
    [Range(0f, 30f)]
    [SerializeField] private float feedbackLightIntensity = 9f;

    [Tooltip("Alcance de la luz en unidades de mundo.")]
    [Range(0.5f, 14f)]
    [SerializeField] private float feedbackLightRange = 5f;

    [Tooltip("Altura de la luz sobre el suelo.")]
    [Range(0f, 3f)]
    [SerializeField] private float feedbackLightHeight = 0.6f;

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

    private Light feedbackLight;

    private float currentFillProgress = 0f;
    private float currentWindowOpen = 0f;
    private float currentNextNoteSoon = 0f;

    // Nota que el anillo persigue ahora mismo, y el beat en que empezó a perseguirla.
    // Juntos definen cuánto dura el barrido, para que una nota que aparece de golpe
    // muy cerca igual se vea entrar desde el borde en vez de nacer a medio camino.
    private float trackedTargetBeat = -1f;
    private float approachStartBeat = 0f;
    private float hitFeedbackTimer = 0f;
    private float missFeedbackTimer = 0f;
    private float damageFeedbackTimer = 0f;
    private float currentPunchFactor = 1.0f;

    private static readonly int FillProgressProp = Shader.PropertyToID("_FillProgress");
    private static readonly int WindowOpenProp = Shader.PropertyToID("_WindowOpen");
    private static readonly int HitFeedbackProp = Shader.PropertyToID("_HitFeedback");
    private static readonly int MissFeedbackProp = Shader.PropertyToID("_MissFeedback");
    private static readonly int DamageFeedbackProp = Shader.PropertyToID("_DamageFeedback");
    private static readonly int NextNoteSoonProp = Shader.PropertyToID("_NextNoteSoon");

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

            beatJudge.OnPenaltyApplied -= HandlePenaltyApplied;
            beatJudge.OnPenaltyApplied += HandlePenaltyApplied;
        }
    }

    private void UnsubscribeEvents()
    {
        if (beatJudge != null)
        {
            beatJudge.OnBeatConsumed -= HandleBeatConsumed;
            beatJudge.OnPenaltyApplied -= HandlePenaltyApplied;
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

            visualChildObj.AddComponent<MeshFilter>();

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

        // El Quad integrado de Unity es vertical (plano XY). Si queda asignado, el
        // indicador se dibuja de canto y se entierra en el suelo, así que siempre
        // imponemos una malla horizontal propia: con ella, rotación identidad = plano.
        MeshFilter meshFilter = visualChildObj.GetComponent<MeshFilter>();
        if (meshFilter == null)
            meshFilter = visualChildObj.AddComponent<MeshFilter>();

        Mesh horizontalQuad = GetOrCreateHorizontalQuadMesh();
        if (meshFilter.sharedMesh != horizontalQuad)
            meshFilter.sharedMesh = horizontalQuad;

        EnsureFeedbackLight();

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
    /// Crea la luz puntual que destella al acertar una nota o al recibir daño.
    /// </summary>
    private void EnsureFeedbackLight()
    {
        if (!enableFeedbackLight)
        {
            if (feedbackLight != null)
                feedbackLight.enabled = false;

            return;
        }

        Transform lightParent = visualTransform != null ? visualTransform : transform;

        if (feedbackLight == null)
        {
            Transform existing = lightParent.Find("RhythmFeedbackLight");
            if (existing != null)
                feedbackLight = existing.GetComponent<Light>();
        }

        if (feedbackLight == null)
        {
            GameObject lightObj = new GameObject("RhythmFeedbackLight");
            lightObj.transform.SetParent(lightParent, false);

            feedbackLight = lightObj.AddComponent<Light>();
            feedbackLight.type = LightType.Point;
            feedbackLight.shadows = LightShadows.None;
        }

        feedbackLight.range = feedbackLightRange;
        feedbackLight.transform.localPosition = new Vector3(0f, feedbackLightHeight, 0f);
    }

    /// <summary>
    /// Alimenta la luz con los mismos temporizadores que recolorean los anillos, para que
    /// el destello y el cambio de color ocurran exactamente a la vez.
    /// </summary>
    private void UpdateFeedbackLight()
    {
        if (feedbackLight == null) return;

        if (!enableFeedbackLight)
        {
            feedbackLight.enabled = false;
            return;
        }

        float strength = Mathf.Max(hitFeedbackTimer, damageFeedbackTimer);

        feedbackLight.enabled = strength > 0.001f;
        if (!feedbackLight.enabled) return;

        feedbackLight.color = damageFeedbackTimer > hitFeedbackTimer ? damageLightColor : hitLightColor;

        // Cuadrático: el destello pega fuerte y se apaga rápido en vez de desvanecerse plano.
        feedbackLight.intensity = feedbackLightIntensity * strength * strength;
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
                propertyBlock.SetFloat(FillProgressProp, 0.6f);
                propertyBlock.SetFloat(WindowOpenProp, 0.0f);
                propertyBlock.SetFloat(HitFeedbackProp, 0.0f);
                propertyBlock.SetFloat(MissFeedbackProp, 0.0f);
                propertyBlock.SetFloat(DamageFeedbackProp, 0.0f);
                visualRenderer.SetPropertyBlock(propertyBlock);
            }
            return;
        }

        UpdateRhythmState();
        UpdateFeedbackDecay();
        ApplyShaderProperties();
        UpdateFeedbackLight();
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

            float interval = adaptToNoteInterval && !beatJudge.UsesNoteChart
                ? Mathf.Max(0.5f, (float)beatJudge.NoteInterval)
                : fillDurationBeats;

            // Buscamos la nota objetivo y la que viene inmediatamente después, para
            // poder avisar cuando vengan dos pegadas. Las notas caen en semicorcheas.
            const float paso = 1f / BeatActionJudge.SlotsPerBeat;
            float targetBeat = -1f;
            float followingBeat = -1f;
            float startScan = Mathf.Floor((nowBeat - lateTol) * BeatActionJudge.SlotsPerBeat) * paso;
            float endScan = nowBeat + interval + nextNoteWarnBeats + 1f;

            for (float b = startScan; b <= endScan; b += paso)
            {
                if (b < (nowBeat - lateTol)) continue;
                if (!beatJudge.IsNoteBeat(b)) continue;
                if (beatJudge.IsBeatResolved(b)) continue;

                if (targetBeat < 0f)
                {
                    targetBeat = b;
                    continue;
                }

                followingBeat = b;
                break;
            }

            if (targetBeat >= 0f)
            {
                // Al cambiar de nota objetivo anotamos desde dónde arranca su barrido.
                if (!Mathf.Approximately(targetBeat, trackedTargetBeat))
                {
                    trackedTargetBeat = targetBeat;
                    approachStartBeat = nowBeat;
                }

                // El barrido dura lo que haya de margen real, con un tope para que en
                // huecos largos el anillo no se arrastre. Así dos notas seguidas son dos
                // barridos rápidos y completos, en vez de un anillo que nace a medio camino.
                float span = Mathf.Clamp(targetBeat - approachStartBeat, 0.2f, interval);
                float sweepStart = targetBeat - span;
                currentFillProgress = Mathf.Clamp01((nowBeat - sweepStart) / span);

                float beatsUntilTarget = targetBeat - nowBeat;
                bool isWindowOpen = (beatsUntilTarget <= earlyTol) && ((nowBeat - targetBeat) <= lateTol);
                currentWindowOpen = isWindowOpen ? 1.0f : 0.0f;

                bool followsClosely = followingBeat >= 0f && (followingBeat - targetBeat) <= nextNoteWarnBeats;
                currentNextNoteSoon = followsClosely ? 1.0f : 0.0f;
            }
            else
            {
                trackedTargetBeat = -1f;
                currentFillProgress = 0f;
                currentWindowOpen = 0f;
                currentNextNoteSoon = 0f;
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

            // Encogimiento breve: se multiplica sobre el rebote de acierto en vez de pisarlo.
            currentPunchFactor *= Mathf.Lerp(1.0f, missShrinkScale, missFeedbackTimer);
        }

        if (damageFeedbackTimer > 0f)
        {
            damageFeedbackTimer = Mathf.MoveTowards(damageFeedbackTimer, 0f, Time.deltaTime / Mathf.Max(0.01f, damageFeedbackDuration));
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
        propertyBlock.SetFloat(DamageFeedbackProp, damageFeedbackTimer);
        propertyBlock.SetFloat(NextNoteSoonProp, currentNextNoteSoon);

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

    /// <summary>
    /// El juez avisa solo cuando de verdad cobra tiempo, así que mientras la penalización
    /// está bloqueada (machacar teclas) el indicador tampoco repite el destello.
    /// </summary>
    private void HandlePenaltyApplied()
    {
        if (enableMissFeedback)
        {
            TriggerMissFeedback();
        }
    }

    /// <summary>
    /// Dispara manualmente el feedback visual de fallo (whiff o nota que se dejó pasar).
    /// </summary>
    public void TriggerMissFeedback()
    {
        missFeedbackTimer = 1.0f;
        OnNoteHitMiss?.Invoke();
    }

    /// <summary>
    /// Dispara las ondas concéntricas de daño recibido. La llama PlayerHealth al herir al jugador.
    /// </summary>
    public void TriggerDamageFeedback()
    {
        damageFeedbackTimer = 1.0f;
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
