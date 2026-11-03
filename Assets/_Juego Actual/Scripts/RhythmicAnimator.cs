using UnityEngine;

public class RhythmicAnimator : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("El transform a animar. Preferiblemente la malla/modelo visual del personaje. El pivote DEBE estar en la base si se usa Squash & Stretch.")]
    public Transform modelTransform;

    [Header("Activadores")]
    [Tooltip("Activa el pequeño salto rítmico hacia arriba y abajo.")]
    public bool enableBop = true;
    [Tooltip("Activa la inclinación lateral en los beats.")]
    public bool enableTilt = true;
    [Tooltip("Activa la deformación de Squash & Stretch.")]
    public bool enableSquashAndStretch = false;

    public enum RhythmicStyle
    {
        Custom,
        Subtle,      // Movimiento mínimo, ideal para modelos 3D serios
        Toon,        // Más rebote y energía, ideal para juegos rítmicos estándar
        Exaggerated  // Muy exagerado, estilo caricatura pura o Slime
    }

    [Header("✨ Presets Mágicos (Selecciona uno)")]
    [Tooltip("Al seleccionar un preset, se autoconfigurarán los valores de abajo. Luego volverá a 'Custom' para que los puedas retocar.")]
    public RhythmicStyle applyPreset = RhythmicStyle.Custom;

    [Header("Configuración del Bop (Salto vertical)")]
    [Range(0f, 1f)] public float bopHeight = 0.2f;
    [Tooltip("Curva para el salto. 0 = Abajo, 1 = Arriba. (El eje horizontal va de 0 a 1 Beat)")]
    public AnimationCurve bopCurve;

    [Header("Configuración del Tilt (Inclinación lateral)")]
    [Range(0f, 45f)] public float tiltAngle = 5f;
    [Tooltip("Curva para la inclinación. 0 = Centro, 1 = Inclinado máximo (El eje horizontal va de 0 a 1 Beat)")]
    public AnimationCurve tiltCurve;

    [Header("Configuración de Squash & Stretch")]
    [Tooltip("Nivel de deformación. Mantiene el volumen automáticamente. (0 = Nada, 1 = Aplastado por completo)")]
    [Range(0f, 0.8f)] public float squashAmount = 0.15f;
    [Tooltip("Controla la intensidad a lo largo de 1 Beat. 0 = Normal, 1 = Squash, -1 = Stretch")]
    public AnimationCurve deformationCurve;

    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private Vector3 originalScale;

    private void OnValidate()
    {
        // Truco mágico: Si eligen un preset, aplicamos los valores y lo regresamos a Custom
        // para que puedan seguir modificando los sliders a su gusto.
        if (applyPreset != RhythmicStyle.Custom)
        {
            switch (applyPreset)
            {
                case RhythmicStyle.Subtle:
                    bopHeight = 0.08f;
                    tiltAngle = 2f;
                    squashAmount = 0.05f;
                    break;
                case RhythmicStyle.Toon:
                    bopHeight = 0.25f;
                    tiltAngle = 8f;
                    squashAmount = 0.2f;
                    break;
                case RhythmicStyle.Exaggerated:
                    bopHeight = 0.5f;
                    tiltAngle = 20f;
                    squashAmount = 0.45f;
                    break;
            }
            applyPreset = RhythmicStyle.Custom;
        }
    }

    private void Awake()
    {
        if (modelTransform == null)
            modelTransform = transform;

        originalPosition = modelTransform.localPosition;
        originalRotation = modelTransform.localRotation;
        originalScale = modelTransform.localScale;

        // --- Configurar curvas por defecto si están vacías ---

        if (bopCurve == null || bopCurve.keys.Length == 0)
        {
            bopCurve = new AnimationCurve(
                new Keyframe(0.0f, 0.0f), 
                new Keyframe(0.5f, 1.0f), 
                new Keyframe(1.0f, 0.0f)
            );
        }

        if (tiltCurve == null || tiltCurve.keys.Length == 0)
        {
            tiltCurve = new AnimationCurve(
                new Keyframe(0.0f, 1.0f), 
                new Keyframe(0.5f, 0.0f), 
                new Keyframe(1.0f, 0.0f)
            );
        }

        if (deformationCurve == null || deformationCurve.keys.Length == 0)
        {
            deformationCurve = new AnimationCurve(
                new Keyframe(0.0f, 1.0f),
                new Keyframe(0.2f, -0.4f),
                new Keyframe(0.4f, 0.1f),
                new Keyframe(0.6f, 0.0f)
            );
        }
    }

    private void Update()
    {
        if (BeatManager.Instance == null || !BeatManager.Instance.IsPlaying) 
            return;

        double songTime = BeatManager.Instance.CurrentSongTime;
        double beatsPassed = songTime / BeatManager.Instance.SecPerBeat;
        float percent = (float)(beatsPassed % 1.0);

        int currentBeat = BeatManager.Instance.CurrentBeat;

        Vector3 newPosition = originalPosition;
        Quaternion newRotation = originalRotation;
        Vector3 newScale = originalScale;

        // --- 1. Movimiento Bop (Arriba / Abajo) ---
        if (enableBop)
        {
            float bopValue = bopCurve.Evaluate(percent);
            newPosition += Vector3.up * (bopValue * bopHeight);
        }

        // --- 2. Inclinación Tilt (Izquierda / Derecha) ---
        if (enableTilt)
        {
            float tiltDir = (currentBeat % 2 == 0) ? 1f : -1f;
            float tiltValue = tiltCurve.Evaluate(percent);
            Quaternion tiltRotation = Quaternion.Euler(0, 0, tiltAngle * tiltValue * tiltDir);
            newRotation = originalRotation * tiltRotation;
        }

        // --- 3. Squash and Stretch ---
        if (enableSquashAndStretch && squashAmount > 0f)
        {
            // Fórmula real de conservación de volumen para hacer el setup súper fácil
            float squashY = Mathf.Clamp(1.0f - squashAmount, 0.1f, 1.0f);
            float squashXZ = 1.0f / Mathf.Sqrt(squashY);
            Vector3 calcSquash = new Vector3(squashXZ, squashY, squashXZ);

            float stretchY = 1.0f + (squashAmount / 2f); // El rebote suele ser un poco más débil
            float stretchXZ = 1.0f / Mathf.Sqrt(stretchY);
            Vector3 calcStretch = new Vector3(stretchXZ, stretchY, stretchXZ);

            float curveValue = deformationCurve.Evaluate(percent);
            if (curveValue > 0)
            {
                newScale = Vector3.LerpUnclamped(originalScale, Vector3.Scale(originalScale, calcSquash), curveValue);
            }
            else
            {
                newScale = Vector3.LerpUnclamped(originalScale, Vector3.Scale(originalScale, calcStretch), Mathf.Abs(curveValue));
            }
        }

        // --- Aplicar transformaciones ---
        modelTransform.localPosition = newPosition;
        modelTransform.localRotation = newRotation;
        modelTransform.localScale = newScale;
    }
}
