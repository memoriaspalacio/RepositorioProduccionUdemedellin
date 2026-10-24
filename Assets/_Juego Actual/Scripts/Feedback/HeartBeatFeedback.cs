using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Hace latir el corazón del metrónomo:
///   - Se ilumina en verde mientras una nota está dentro de la ventana de acción: el
///     corazón es la zona segura, pulsar ahora cuenta.
///   - Un pulso suave cada vez que una nota llega al corazón, acierte o no el jugador.
///   - Al acertar, un latido fuerte: crece, se tiñe cálido, destella y enciende un halo
///     de luz detrás.
///   - Al fallar (cuando el juez cobra una penalización) tiembla, se apaga un poco y el
///     halo se vuelve rojo.
///
/// Va en el mismo objeto que la Image del corazón. El destello y el halo se crean al
/// arrancar, sin assets extra (ver SpritesProcedurales).
/// </summary>
[DisallowMultipleComponent]
public class HeartBeatFeedback : MonoBehaviour
{
    [Header("--- Referencias ---")]
    [Tooltip("Juez de ritmo de la escena. Si es nulo se busca al arrancar.")]
    [SerializeField] private BeatActionJudge beatJudge;

    [Tooltip("Imagen del corazón. Si es nula se usa la de este objeto.")]
    [SerializeField] private Image corazon;

    [Header("--- Zona segura ---")]
    [Tooltip("Color con el que se ilumina el corazón mientras una nota está dentro de la ventana de acción.")]
    [SerializeField] private Color colorZonaSegura = new Color(0.3f, 1f, 0.55f, 0.8f);

    [Tooltip("Cuánto se tiñe de verde el frente del corazón (además del halo) con una nota en la ventana.")]
    [Range(0f, 1f)]
    [SerializeField] private float brilloFrenteZonaSegura = 0.25f;

    [Tooltip("Qué tan rápido se enciende y se apaga la luz de zona segura, por segundo.")]
    [Range(1f, 30f)]
    [SerializeField] private float velocidadZonaSegura = 12f;

    [Header("--- Latido al acertar ---")]
    [Tooltip("Cuánto crece el corazón en el latido (1 = nada).")]
    [Range(1f, 1.6f)]
    [SerializeField] private float escalaLatido = 1.2f;

    [Tooltip("Segundos que tarda el latido en volver a reposo.")]
    [Range(0.05f, 1f)]
    [SerializeField] private float duracionLatido = 0.3f;

    [Tooltip("Tinte del corazón en el latido; vuelve a su color normal mientras se apaga.")]
    [SerializeField] private Color colorAcierto = new Color(1f, 0.9f, 0.72f, 1f);

    [Tooltip("Intensidad del destello de luz sobre el corazón en el latido (0 = nada).")]
    [Range(0f, 1f)]
    [SerializeField] private float destelloAcierto = 0.45f;

    [Header("--- Pulso del metrónomo ---")]
    [Tooltip("Pulso suave cada vez que una nota llega al corazón, acierte o no el jugador. 1 = desactivado.")]
    [Range(1f, 1.2f)]
    [SerializeField] private float escalaPulso = 1.05f;

    [Tooltip("Segundos que dura el pulso del metrónomo.")]
    [Range(0.05f, 0.5f)]
    [SerializeField] private float duracionPulso = 0.18f;

    [Header("--- Fallo ---")]
    [Tooltip("Tinte del corazón al fallar.")]
    [SerializeField] private Color colorFallo = new Color(0.55f, 0.42f, 0.48f, 1f);

    [Tooltip("Amplitud del temblor al fallar, en unidades de UI.")]
    [Range(0f, 20f)]
    [SerializeField] private float temblorFallo = 5f;

    [Tooltip("Segundos que dura la reacción de fallo.")]
    [Range(0.05f, 1f)]
    [SerializeField] private float duracionFallo = 0.35f;

    [Header("--- Luz ---")]
    [Tooltip("Halo de luz detrás del corazón: verde con una nota en la ventana, cálido al acertar, rojo al " +
             "fallar y muy tenue con cada nota.")]
    [SerializeField] private bool usarLuz = true;

    [Tooltip("Color del halo al acertar.")]
    [SerializeField] private Color colorLuzAcierto = new Color(1f, 0.72f, 0.3f, 0.85f);

    [Tooltip("Color del halo al fallar.")]
    [SerializeField] private Color colorLuzFallo = new Color(0.9f, 0.08f, 0.12f, 0.55f);

    [Tooltip("Tamaño del halo respecto al corazón.")]
    [Range(1f, 3f)]
    [SerializeField] private float tamanoLuz = 1.9f;

    private RectTransform rect;
    private bool iniciado;

    // Estado del corazón en reposo, al que vuelve siempre
    private Vector3 escalaBase;
    private Vector2 posicionBase;
    private Color colorBase;

    private Image destello;
    private Image luz;
    private RectTransform rectLuz;

    // 1 al dispararse y caen a 0 en su duración
    private float latido;
    private float pulso;
    private float fallo;

    // 0..1 suavizado: hay una nota dentro de la ventana
    private float zonaSegura;

    // Beat del fotograma anterior, para saber qué notas cruzaron el corazón desde entonces
    private float ultimoBeat = float.NaN;

    private void Awake()
    {
        rect = (RectTransform)transform;

        if (corazon == null)
            corazon = GetComponent<Image>();

        if (beatJudge == null)
            beatJudge = FindFirstObjectByType<BeatActionJudge>();
    }

    private void OnEnable()
    {
        if (beatJudge != null)
        {
            beatJudge.OnBeatConsumed += HandleBeatConsumed;
            beatJudge.OnPenaltyApplied += HandlePenaltyApplied;
        }
    }

    private void OnDisable()
    {
        if (beatJudge != null)
        {
            beatJudge.OnBeatConsumed -= HandleBeatConsumed;
            beatJudge.OnPenaltyApplied -= HandlePenaltyApplied;
        }

        latido = pulso = fallo = zonaSegura = 0f;

        if (iniciado)
            Aplicar(1f);
    }

    private void OnDestroy()
    {
        // El halo es hermano del corazón, no hijo: hay que borrarlo a mano
        if (rectLuz != null)
            Destroy(rectLuz.gameObject);
    }

    private void Start()
    {
        escalaBase = rect.localScale;
        posicionBase = rect.anchoredPosition;
        colorBase = corazon != null ? corazon.color : Color.white;

        CrearDestello();

        if (usarLuz)
            CrearLuz();

        iniciado = true;
    }

    private void Update()
    {
        if (!iniciado)
            return;

        DetectarLlegadaDeNotas();

        float dt = Time.unscaledDeltaTime;
        latido = Mathf.MoveTowards(latido, 0f, dt / duracionLatido);
        pulso = Mathf.MoveTowards(pulso, 0f, dt / duracionPulso);
        fallo = Mathf.MoveTowards(fallo, 0f, dt / duracionFallo);

        bool ventanaAbierta = beatJudge != null && beatJudge.IsWindowOpen;
        zonaSegura = Mathf.MoveTowards(zonaSegura, ventanaAbierta ? 1f : 0f, dt * velocidadZonaSegura);

        // Cuadrático: el golpe se nota de inmediato y se asienta rápido, como un latido
        float fuerzaLatido = latido * latido;
        float fuerzaPulso = pulso * pulso;

        float escala =
            1f +
            (escalaLatido - 1f) * fuerzaLatido +
            (escalaPulso - 1f) * fuerzaPulso;

        Aplicar(escala);
    }

    #region Construcción

    // Luz suave por delante del corazón (hijo suyo, así late con él)
    private void CrearDestello()
    {
        RectTransform rectDestello = CrearRect("DestelloCorazon", rect);
        rectDestello.anchorMin = Vector2.zero;
        rectDestello.anchorMax = Vector2.one;
        rectDestello.offsetMin = Vector2.zero;
        rectDestello.offsetMax = Vector2.zero;

        destello = CrearImagen(rectDestello);
    }

    // Halo por detrás del corazón: hermano suyo, justo antes en la jerarquía
    private void CrearLuz()
    {
        rectLuz = CrearRect("LuzCorazon", rect.parent);
        rectLuz.SetSiblingIndex(rect.GetSiblingIndex());
        rectLuz.anchorMin = rect.anchorMin;
        rectLuz.anchorMax = rect.anchorMax;

        luz = CrearImagen(rectLuz);
    }

    private RectTransform CrearRect(string nombre, Transform padre)
    {
        GameObject objeto = new GameObject(nombre, typeof(RectTransform));
        objeto.layer = gameObject.layer;

        RectTransform nuevo = (RectTransform)objeto.transform;
        nuevo.SetParent(padre, false);
        nuevo.pivot = new Vector2(0.5f, 0.5f);

        return nuevo;
    }

    private static Image CrearImagen(RectTransform destino)
    {
        Image imagen = destino.gameObject.AddComponent<Image>();
        imagen.sprite = SpritesProcedurales.BrilloRadial;
        imagen.raycastTarget = false;
        imagen.color = Color.clear;

        return imagen;
    }

    #endregion

    #region Actualización

    /// <summary>Dispara el pulso del metrónomo cuando una nota llega al corazón.</summary>
    private void DetectarLlegadaDeNotas()
    {
        if (beatJudge == null || BeatManager.Instance == null || !BeatManager.Instance.IsPlaying)
        {
            ultimoBeat = float.NaN;
            return;
        }

        float ahora = (float)(BeatManager.Instance.CurrentSongTime / BeatManager.Instance.SecPerBeat);

        if (float.IsNaN(ultimoBeat) || ahora < ultimoBeat)
        {
            ultimoBeat = ahora;
            return;
        }

        // Las notas caen en semicorcheas: se revisa cada posición del juez
        const float paso = 1f / BeatActionJudge.SlotsPerBeat;
        for (float beat = Mathf.Floor(ultimoBeat * BeatActionJudge.SlotsPerBeat) * paso + paso; beat <= ahora; beat += paso)
        {
            if (beatJudge.IsNoteBeat(beat))
                pulso = 1f;
        }

        ultimoBeat = ahora;
    }

    private void Aplicar(float escala)
    {
        rect.localScale = escalaBase * escala;

        Vector2 temblor = fallo > 0f
            ? Random.insideUnitCircle * (temblorFallo * fallo)
            : Vector2.zero;

        rect.anchoredPosition = posicionBase + temblor;

        // Cálido al acertar, apagado al fallar
        if (corazon != null)
        {
            Color color = Color.Lerp(colorBase, colorAcierto, latido);
            corazon.color = Color.Lerp(color, colorFallo, fallo);
        }

        float fuerzaLatido = latido * latido;

        // Frente: un velo verde con la ventana abierta, que el destello del acierto supera
        if (destello != null)
        {
            Color veloVerde = colorZonaSegura;
            veloVerde.a = brilloFrenteZonaSegura * zonaSegura;

            Color destelloBlanco = new Color(1f, 0.97f, 0.9f, destelloAcierto * fuerzaLatido);

            destello.color = destelloBlanco.a > veloVerde.a ? destelloBlanco : veloVerde;
        }

        if (luz != null)
        {
            rectLuz.position = rect.TransformPoint(rect.rect.center);
            rectLuz.sizeDelta = rect.rect.size * tamanoLuz;
            rectLuz.localScale = escalaBase * escala;

            // Base: verde mientras hay una nota en la ventana, o el brillo tenue de cada nota
            Color colorHalo = colorZonaSegura;
            colorHalo.a *= zonaSegura;

            Color brilloPulso = colorLuzAcierto;
            brilloPulso.a *= pulso * pulso * 0.25f;

            if (brilloPulso.a > colorHalo.a)
                colorHalo = brilloPulso;

            // Encima: el acierto lo vuelve cálido y el fallo, rojo
            colorHalo = Color.Lerp(colorHalo, colorLuzAcierto, fuerzaLatido);
            colorHalo = Color.Lerp(colorHalo, colorLuzFallo, fallo);

            luz.color = colorHalo;
        }
    }

    #endregion

    #region Eventos del juez

    private void HandleBeatConsumed(float beat)
    {
        latido = 1f;
    }

    private void HandlePenaltyApplied()
    {
        fallo = 1f;
    }

    #endregion
}
