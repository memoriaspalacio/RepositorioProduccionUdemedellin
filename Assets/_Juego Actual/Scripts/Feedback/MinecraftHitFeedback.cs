using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Componente modular que proporciona el efecto de retroalimentación de golpe estilo Minecraft:
/// - Flash rojo característico sobre los renderers (Shader / MaterialPropertyBlock / MaterialSwap).
/// - Hurt Tilt: Inclinación física del personaje opuesta al origen del daño usando la curva de Minecraft.
/// - Hurt Shake: Vibración posicional de alta frecuencia en el modelo visual (sin desfasar la grilla).
/// - HurtCam / Camera Shake: Inclinación lateral de la cámara si el receptor es el jugador.
/// - SFX y partículas integradas.
/// </summary>
[DisallowMultipleComponent]
public class MinecraftHitFeedback : MonoBehaviour
{
    public enum FlashMode
    {
        Adaptive,             // Usa shader de daño si existe, o MaterialPropertyBlock / Swap automáticamente
        MaterialPropertyBlock,// Tinta _HurtAmount, _BaseColor y _EmissionColor sin instanciar materiales
        MaterialSwap          // Reemplaza temporalmente con un material rojo puro estilo Minecraft
    }

    [Header("--- Flash Rojo Estilo Minecraft ---")]
    [Tooltip("Modo de aplicación del flash rojo sobre los renderers.")]
    [SerializeField] private FlashMode flashMode = FlashMode.Adaptive;

    [Tooltip("Color rojo icónico del daño de Minecraft.")]
    [SerializeField] private Color flashColor = new Color(1f, 0.08f, 0.08f, 1f);

    [Tooltip("Duración en segundos del destello rojo (en Minecraft dura aprox 0.15 - 0.2s).")]
    [Range(0.05f, 0.6f)]
    [SerializeField] private float flashDuration = 0.18f;

    [Tooltip("Material opcional con shader rojo de impacto. Si es nulo, se genera uno automáticamente con Custom/MinecraftHurt.")]
    [SerializeField] private Material customFlashMaterial;

    [Header("--- Shake y Tilt de Daño (Minecraft Physics) ---")]
    [Tooltip("Si está activo, inclina el personaje en dirección contraria al impacto (Damage Tilt).")]
    [SerializeField] private bool enableTilt = true;

    [Tooltip("Ángulo de inclinación en grados (en Minecraft es aprox 14 - 18 grados).")]
    [Range(5f, 35f)]
    [SerializeField] private float tiltAngle = 16f;

    [Tooltip("Duración total de la inclinación y recuperación.")]
    [Range(0.1f, 0.6f)]
    [SerializeField] private float tiltDuration = 0.28f;

    [Tooltip("Si está activo, sacude posicionalmente el modelo 3D con micro-vibraciones.")]
    [SerializeField] private bool enablePositionShake = true;

    [Tooltip("Amplitud del shake posicional.")]
    [Range(0.01f, 0.3f)]
    [SerializeField] private float shakeIntensity = 0.08f;

    [Tooltip("Frecuencia de la vibración rápida.")]
    [SerializeField] private float shakeFrequency = 45f;

    [Tooltip("Duración de la vibración posicional.")]
    [Range(0.05f, 0.5f)]
    [SerializeField] private float shakeDuration = 0.22f;

    [Tooltip("Transform del modelo visual o Animator hijo. Si es nulo, se detecta automáticamente para proteger las coordenadas de la grilla.")]
    [SerializeField] private Transform visualTransform;

    [Header("--- HurtCam (Cámara del Jugador) ---")]
    [Tooltip("Indica si este objeto es el Jugador para aplicar sacudida e inclinación de cámara.")]
    [SerializeField] private bool isPlayer = false;

    [Tooltip("Si está activo y es el jugador, sacude e inclina la cámara principal al recibir daño.")]
    [SerializeField] private bool shakeCameraOnHit = true;

    [Tooltip("Ángulo de inclinación de la cámara (roll estilo Minecraft HurtCam).")]
    [Range(2f, 20f)]
    [SerializeField] private float cameraTiltAngle = 8f;

    [Tooltip("Duración de la inclinación de cámara.")]
    [Range(0.1f, 0.5f)]
    [SerializeField] private float cameraShakeDuration = 0.22f;

    [Header("--- Audio y SFX ---")]
    [Tooltip("Si está activo, reproduce un efecto de sonido de impacto.")]
    [SerializeField] private bool playHitSound = true;

    [Tooltip("Nombre del sonido en SoundManager (ej. 'danoJugador', 'ataqueJugador').")]
    [SerializeField] private string soundName = "danoJugador";

    [Tooltip("AudioClip opcional directo si no se utiliza SoundManager.")]
    [SerializeField] private AudioClip directAudioClip;

    [Header("--- Partículas de Impacto (Opcional) ---")]
    [Tooltip("Prefab opcional de partículas de golpe o crítico estilo Minecraft.")]
    [SerializeField] private GameObject hitParticlesPrefab;

    [SerializeField] private Vector3 particlesOffset = new Vector3(0f, 1f, 0f);

    // Identificadores de Shader Properties
    private static readonly int HurtAmountProp = Shader.PropertyToID("_HurtAmount");
    private static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorProp = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorProp = Shader.PropertyToID("_EmissionColor");

    // Referencias internas
    private Renderer[] renderers;
    private MaterialPropertyBlock propertyBlock;
    private Material runtimeFlashMaterial;
    private Dictionary<Renderer, Material[]> originalMaterialsMap = new Dictionary<Renderer, Material[]>();

    // Estado del modelo visual
    private Vector3 visualOriginalLocalPosition;
    private Quaternion visualOriginalLocalRotation;
    private bool hasVisualBase = false;

    // Corrutinas activas
    private Coroutine flashRoutine;
    private Coroutine tiltAndShakeRoutine;
    private Coroutine cameraShakeRoutine;

    private void Awake()
    {
        InitializeComponents();
    }

    private void OnDisable()
    {
        ResetVisualState();
    }

    private void InitializeComponents()
    {
        // 1. Detectar el transform visual para no alterar la raíz que maneja el NavMesh/Grilla
        if (visualTransform == null)
        {
            Animator anim = GetComponentInChildren<Animator>();
            if (anim != null && anim.transform != transform)
            {
                visualTransform = anim.transform;
            }
            else
            {
                // Buscar el primer hijo con renderer
                Renderer firstChildRenderer = GetComponentInChildren<Renderer>();
                if (firstChildRenderer != null && firstChildRenderer.transform != transform)
                {
                    visualTransform = firstChildRenderer.transform;
                }
                else
                {
                    visualTransform = transform;
                }
            }
        }

        if (visualTransform != null)
        {
            visualOriginalLocalPosition = visualTransform.localPosition;
            visualOriginalLocalRotation = visualTransform.localRotation;
            hasVisualBase = true;
        }

        // Auto-detección del Jugador si no se configuró manualmente
        if (!isPlayer && (GetComponent<PlayerHealth>() != null || CompareTag("Player")))
        {
            isPlayer = true;
        }

        // 2. Obtener los renderers del cuerpo (MeshRenderer y SkinnedMeshRenderer)
        renderers = FilterBodyRenderers(GetComponentsInChildren<Renderer>(true));
        propertyBlock = new MaterialPropertyBlock();

        // 3. Cachear materiales originales para el modo MaterialSwap
        originalMaterialsMap.Clear();
        foreach (Renderer rend in renderers)
        {
            if (rend != null)
            {
                originalMaterialsMap[rend] = rend.sharedMaterials;
            }
        }

        // 4. Asegurar material de flash rojo
        if (customFlashMaterial != null)
        {
            runtimeFlashMaterial = customFlashMaterial;
        }
        else
        {
            Shader shader = Shader.Find("Custom/MinecraftHurtOverlay");
            if (shader == null) shader = Shader.Find("Custom/MinecraftHurt");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");

            if (shader != null)
            {
                runtimeFlashMaterial = new Material(shader)
                {
                    name = "Runtime_MinecraftHurtMaterial"
                };

                if (runtimeFlashMaterial.HasProperty("_Color"))
                    runtimeFlashMaterial.SetColor("_Color", flashColor);
                if (runtimeFlashMaterial.HasProperty("_HurtColor"))
                    runtimeFlashMaterial.SetColor("_HurtColor", flashColor);
                if (runtimeFlashMaterial.HasProperty("_HurtAmount"))
                    runtimeFlashMaterial.SetFloat("_HurtAmount", 1f);
                if (runtimeFlashMaterial.HasProperty("_BaseColor"))
                    runtimeFlashMaterial.SetColor("_BaseColor", flashColor);
            }
        }
    }

    /// <summary>
    /// Deja solo los renderers del cuerpo del personaje. Partículas, sprites, trails y
    /// cualquier malla con material en cola transparente quedan fuera: el flash los
    /// reemplaza por un material rojo opaco, y un quad de efecto así se ve como un
    /// cuadrado rojo sólido tapando la pantalla.
    /// </summary>
    private static Renderer[] FilterBodyRenderers(Renderer[] all)
    {
        List<Renderer> body = new List<Renderer>(all.Length);

        foreach (Renderer rend in all)
        {
            if (rend == null) continue;
            if (!(rend is SkinnedMeshRenderer) && !(rend is MeshRenderer)) continue;

            Material mat = rend.sharedMaterial;
            if (mat != null && mat.renderQueue >= 3000) continue;

            body.Add(rend);
        }

        return body.ToArray();
    }

    /// <summary>
    /// Ejecuta la retroalimentación completa de golpe estilo Minecraft.
    /// </summary>
    public void PlayHitFeedback()
    {
        // Si no se especifica dirección, se calcula respecto a la orientación frontal
        Vector3 defaultHitDirection = -transform.forward;
        PlayHitFeedback(defaultHitDirection);
    }

    /// <summary>
    /// Ejecuta la retroalimentación de golpe especificando la posición desde donde provino el ataque.
    /// </summary>
    public void PlayHitFeedbackFromAttacker(Vector3 attackerWorldPosition)
    {
        Vector3 dir = (transform.position - attackerWorldPosition).normalized;
        PlayHitFeedback(dir);
    }

    /// <summary>
    /// Ejecuta la retroalimentación completa calculando la inclinación física opuesta a la dirección recibida.
    /// </summary>
    public void PlayHitFeedback(Vector3 hitDirection)
    {
        if (!gameObject.activeInHierarchy) return;

        // 1. Flash Rojo
        TriggerRedFlash();

        // 2. Hurt Tilt y Shake
        TriggerTiltAndShake(hitDirection);

        // 3. HurtCam (Cámara)
        if (isPlayer && shakeCameraOnHit)
        {
            TriggerCameraShake();
        }

        // 4. Sonido de impacto
        TriggerAudio();

        // 5. Partículas
        TriggerParticles();
    }

    #region Flash Rojo

    private void TriggerRedFlash()
    {
        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
            CleanUpFlash();
        }

        flashRoutine = StartCoroutine(RedFlashCoroutine());
    }

    private IEnumerator RedFlashCoroutine()
    {
        bool useSwap = (flashMode == FlashMode.MaterialSwap);

        if (flashMode == FlashMode.Adaptive)
        {
            // Si el renderer actual tiene shader con _HurtAmount, usamos PropertyBlock;
            // Si no, usamos MaterialSwap para garantizar el rojo plano 100% puro de Minecraft
            bool supportsHurtAmount = false;
            if (renderers != null && renderers.Length > 0 && renderers[0].sharedMaterial != null)
            {
                supportsHurtAmount = renderers[0].sharedMaterial.HasProperty(HurtAmountProp);
            }
            useSwap = !supportsHurtAmount;
        }

        if (useSwap && runtimeFlashMaterial != null)
        {
            // Reemplazo temporal de materiales a rojo puro Minecraft
            foreach (Renderer rend in renderers)
            {
                if (rend == null) continue;
                int count = rend.sharedMaterials.Length;
                Material[] flashMats = new Material[count];
                for (int i = 0; i < count; i++)
                {
                    flashMats[i] = runtimeFlashMaterial;
                }
                rend.sharedMaterials = flashMats;
            }

            yield return new WaitForSeconds(flashDuration);

            // Restauración exacta de materiales originales
            RestoreOriginalMaterials();
        }
        else
        {
            // MaterialPropertyBlock tint
            float elapsed = 0f;
            while (elapsed < flashDuration)
            {
                elapsed += Time.deltaTime;
                float t = 1f - Mathf.Clamp01(elapsed / flashDuration);

                Color currentTint = Color.Lerp(Color.white, flashColor, t);
                Color currentEmission = flashColor * (t * 2.0f);

                propertyBlock.SetFloat(HurtAmountProp, t);
                propertyBlock.SetColor(BaseColorProp, currentTint);
                propertyBlock.SetColor(ColorProp, currentTint);
                propertyBlock.SetColor(EmissionColorProp, currentEmission);

                foreach (Renderer rend in renderers)
                {
                    if (rend != null)
                        rend.SetPropertyBlock(propertyBlock);
                }

                yield return null;
            }

            CleanUpPropertyBlock();
        }

        flashRoutine = null;
    }

    private void RestoreOriginalMaterials()
    {
        foreach (Renderer rend in renderers)
        {
            if (rend != null && originalMaterialsMap.TryGetValue(rend, out Material[] origMats))
            {
                rend.sharedMaterials = origMats;
            }
        }
    }

    private void CleanUpPropertyBlock()
    {
        foreach (Renderer rend in renderers)
        {
            if (rend != null)
                rend.SetPropertyBlock(null);
        }
    }

    private void CleanUpFlash()
    {
        RestoreOriginalMaterials();
        CleanUpPropertyBlock();
    }

    #endregion

    #region Shake y Tilt Física Minecraft

    private void TriggerTiltAndShake(Vector3 hitDirection)
    {
        if (visualTransform == null) return;

        if (tiltAndShakeRoutine != null)
        {
            StopCoroutine(tiltAndShakeRoutine);
            // Restaurar antes de un nuevo golpe para no acumular desfasajes
            visualTransform.localPosition = visualOriginalLocalPosition;
            visualTransform.localRotation = visualOriginalLocalRotation;
        }

        tiltAndShakeRoutine = StartCoroutine(TiltAndShakeCoroutine(hitDirection));
    }

    private IEnumerator TiltAndShakeCoroutine(Vector3 hitDirection)
    {
        // Calcular eje de inclinación:
        // En Minecraft, el mob rota sobre el eje perpendicular a la dirección del daño recibido
        Vector3 localHitDir = visualTransform.InverseTransformDirection(hitDirection).normalized;
        if (localHitDir.sqrMagnitude < 0.001f)
            localHitDir = Vector3.back;

        // Eje de inclinación perpendicular a la dirección en el plano XZ
        Vector3 tiltAxis = Vector3.Cross(Vector3.up, localHitDir).normalized;
        if (tiltAxis.sqrMagnitude < 0.001f)
            tiltAxis = Vector3.right;

        float maxDuration = Mathf.Max(tiltDuration, shakeDuration);
        float elapsed = 0f;

        while (elapsed < maxDuration)
        {
            elapsed += Time.deltaTime;

            Quaternion tiltRot = Quaternion.identity;
            Vector3 shakePos = Vector3.zero;

            // 1. Curva de Inclinación exacta de Minecraft:
            // MathHelper.sin(progress * progress * PI) produce un retroceso rápido y un retorno suave
            if (enableTilt && elapsed < tiltDuration)
            {
                float progress = 1f - (elapsed / tiltDuration);
                float tiltCurve = Mathf.Sin(progress * progress * Mathf.PI);
                float currentAngle = tiltCurve * tiltAngle;

                tiltRot = Quaternion.AngleAxis(currentAngle, tiltAxis);
            }

            // 2. Vibración / Shake de alta frecuencia
            if (enablePositionShake && elapsed < shakeDuration)
            {
                float progressShake = 1f - (elapsed / shakeDuration);
                float sinFactor = Mathf.Sin(elapsed * shakeFrequency);
                float currentShake = sinFactor * shakeIntensity * progressShake;

                shakePos = tiltAxis * currentShake;
            }

            visualTransform.localRotation = visualOriginalLocalRotation * tiltRot;
            visualTransform.localPosition = visualOriginalLocalPosition + shakePos;

            yield return null;
        }

        // Restaurar estado original garantizado
        visualTransform.localPosition = visualOriginalLocalPosition;
        visualTransform.localRotation = visualOriginalLocalRotation;
        tiltAndShakeRoutine = null;
    }

    #endregion

    #region Cámara (HurtCam)

    private void TriggerCameraShake()
    {
        Camera mainCam = Camera.main;
        if (mainCam == null) return;

        if (cameraShakeRoutine != null)
        {
            StopCoroutine(cameraShakeRoutine);
        }

        cameraShakeRoutine = StartCoroutine(CameraShakeCoroutine(mainCam));
    }

    private IEnumerator CameraShakeCoroutine(Camera cam)
    {
        Transform camTransform = cam.transform;
        Quaternion originalCamRot = camTransform.localRotation;
        float elapsed = 0f;

        // Dirección aleatoria de inclinación de la cámara estilo Minecraft (izq o der)
        float rollDirection = (Random.value > 0.5f) ? 1f : -1f;

        while (elapsed < cameraShakeDuration)
        {
            elapsed += Time.deltaTime;
            float progress = 1f - (elapsed / cameraShakeDuration);
            float curve = Mathf.Sin(progress * progress * Mathf.PI);

            float rollAngle = curve * cameraTiltAngle * rollDirection;
            Quaternion tiltRot = Quaternion.Euler(0f, 0f, rollAngle);

            camTransform.localRotation = originalCamRot * tiltRot;
            yield return null;
        }

        camTransform.localRotation = originalCamRot;
        cameraShakeRoutine = null;
    }

    #endregion

    #region Audio y Partículas

    private void TriggerAudio()
    {
        if (!playHitSound) return;

        if (directAudioClip != null)
        {
            AudioSource.PlayClipAtPoint(directAudioClip, transform.position);
            return;
        }

        if (SoundManager.singleton != null && !string.IsNullOrEmpty(soundName))
        {
            SoundManager.singleton.PlaySFX(soundName);
        }
    }

    private void TriggerParticles()
    {
        if (hitParticlesPrefab == null) return;

        Vector3 spawnPos = transform.position + particlesOffset;
        GameObject particles = Instantiate(hitParticlesPrefab, spawnPos, Quaternion.identity);
        Destroy(particles, 1.5f);
    }

    #endregion

    private void ResetVisualState()
    {
        if (hasVisualBase && visualTransform != null)
        {
            visualTransform.localPosition = visualOriginalLocalPosition;
            visualTransform.localRotation = visualOriginalLocalRotation;
        }
        CleanUpFlash();
    }
}
