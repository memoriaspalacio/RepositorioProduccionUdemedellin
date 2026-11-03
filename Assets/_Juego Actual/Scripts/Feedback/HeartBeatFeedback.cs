using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Hace latir el corazón del metrónomo:
///   - Mientras una nota está dentro de la ventana, se ilumina con el color de la
///     calificación que daría pulsar en ese instante (ColoresCalificacion): lavanda tenue
///     en BAD, menta en GOOD y dorado intenso en PERFECT. Así el momento exacto se ve.
///   - Un pulso suave cada vez que una nota llega al corazón, acierte o no el jugador.
///   - Al acertar late según la calificación del juez (OnBeatGraded), como un dibujo
///     animado: PERFECT es un latido enorme que rebota como gelatina, GOOD uno normal y
///     BAD se desinfla, se hunde y pierde el color.
///   - Al fallar (cuando el juez cobra una penalización) tiembla, se apaga un poco y el
///     halo se vuelve rojo.
///
/// El texto y las partículas de cada calificación los pone EfectosCalificacion, que se
/// añade solo si el corazón no lo tiene.
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

    [Header("--- Zona de calificación ---")]
    [Tooltip("Intensidad del brillo con una nota en la ventana, según lo que daría pulsar ahora: BAD, GOOD, PERFECT.")]
    [SerializeField] private Vector3 brilloPorCalificacion = new Vector3(0.3f, 0.6f, 1f);

    [Tooltip("Cuánto se tiñe el frente del corazón (además del halo) con el brillo al máximo.")]
    [Range(0f, 1f)]
    [SerializeField] private float brilloFrenteZonaSegura = 0.3f;

    [Tooltip("Qué tan rápido cambia el brillo de una calificación a otra, por segundo.")]
    [Range(1f, 40f)]
    [SerializeField] private float velocidadZonaSegura = 20f;

    [Header("--- Latido al acertar ---")]
    [Tooltip("Cuánto crece el corazón en un PERFECT (1 = nada). Rebota como gelatina al volver.")]
    [Range(1f, 2f)]
    [SerializeField] private float escalaPerfect = 1.4f;

    [Tooltip("Cuánto crece el corazón en un GOOD (1 = nada).")]
    [Range(1f, 1.6f)]
    [SerializeField] private float escalaLatido = 1.2f;

    [Tooltip("Segundos que tarda el latido en volver a reposo.")]
    [Range(0.05f, 1f)]
    [SerializeField] private float duracionLatido = 0.4f;

    [Tooltip("Tinte del corazón en el latido; vuelve a su color normal mientras se apaga.")]
    [SerializeField] private Color colorAcierto = new Color(1f, 0.9f, 0.72f, 1f);

    [Tooltip("Destello de luz y halo cálido al acertar, el efecto anterior. Apagado: el acierto es solo el " +
             "latido y los efectos de EfectosCalificacion.")]
    [SerializeField] private bool luzAlAcertar = false;

    [Tooltip("Intensidad del destello de luz sobre el corazón en el latido (0 = nada). Solo con luzAlAcertar.")]
    [Range(0f, 1f)]
    [SerializeField] private float destelloAcierto = 0.45f;

    [Header("--- Golpe BAD ---")]
    [Tooltip("Cuánto se aplasta el corazón en un BAD (0 = nada).")]
    [Range(0f, 0.5f)]
    [SerializeField] private float aplastamientoBad = 0.2f;

    [Tooltip("Cuánto se hunde el corazón en un BAD, en unidades de UI.")]
    [Range(0f, 30f)]
    [SerializeField] private float hundimientoBad = 8f;

    [Tooltip("Tinte apagado del corazón en un BAD.")]
    [SerializeField] private Color colorBad = new Color(0.62f, 0.6f, 0.72f, 1f);

    [Tooltip("Segundos que dura el desánimo de un BAD.")]
    [Range(0.1f, 1.5f)]
    [SerializeField] private float duracionBad = 0.55f;

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
    [Tooltip("Halo de luz detrás del corazón: del color de la calificación con una nota en la ventana, rojo al " +
             "fallar y muy tenue con cada nota.")]
    [SerializeField] private bool usarLuz = true;

    [Tooltip("Color del halo al acertar.")]
    [SerializeField] private Color colorLuzAcierto = new Color(1f, 0.72f, 0.3f, 0.85f);

    [Tooltip("Color del halo al fallar.")]
    [SerializeField] private Color colorLuzFallo = new Color(0.9f, 0.08f, 0.12f, 0.55f);

    [Tooltip("Tamaño del halo respecto al corazón.")]
    [Range(1f, 3f)]
    [SerializeField] private float tamanoLuz = 1.5f;

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
    private float desanimo;

    // Lo que crece el latido en curso: escalaPerfect o escalaLatido
    private float escalaLatidoActual = 1.2f;
    private bool latidoPerfect;

    // 0..1 suavizado: hay una nota dentro de la ventana
    private float zonaSegura;
    private Color colorZona = Color.clear;

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
            beatJudge.OnBeatGraded += HandleBeatGraded;
            beatJudge.OnPenaltyApplied += HandlePenaltyApplied;
        }
    }

    private void OnDisable()
    {
        if (beatJudge != null)
        {
            beatJudge.OnBeatGraded -= HandleBeatGraded;
            beatJudge.OnPenaltyApplied -= HandlePenaltyApplied;
        }

        latido = pulso = fallo = desanimo = zonaSegura = 0f;

        if (iniciado)
            Aplicar(Vector2.one);
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

        if (GetComponent<EfectosCalificacion>() == null)
            gameObject.AddComponent<EfectosCalificacion>();

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
        desanimo = Mathf.MoveTowards(desanimo, 0f, dt / duracionBad);

        // Brilla con el color de lo que daría pulsar ahora, más cuanto mejor sería
        JudgementResult ahora = beatJudge != null ? beatJudge.GradeIfPressedNow : JudgementResult.Miss;
        float brilloObjetivo = ahora == JudgementResult.Miss ? 0f : brilloPorCalificacion[(int)ahora - 1];
        zonaSegura = Mathf.MoveTowards(zonaSegura, brilloObjetivo, dt * velocidadZonaSegura * 0.5f);
        if (ahora != JudgementResult.Miss)
            colorZona = Color.Lerp(colorZona, ColoresCalificacion.De(ahora), Mathf.Clamp01(dt * velocidadZonaSegura));

        // Cuadrático: el golpe se nota de inmediato y se asienta rápido, como un latido
        float fuerzaLatido = latido * latido;
        float fuerzaPulso = pulso * pulso;

        float crecer =
            (escalaLatidoActual - 1f) * fuerzaLatido +
            (escalaPulso - 1f) * fuerzaPulso;

        // Gelatina: ancho y alto se turnan al asentarse. Más fuerte en un PERFECT.
        float t = 1f - latido;
        float gelatina = (escalaLatidoActual - 1f) * (latidoPerfect ? 0.55f : 0.3f) * latido * Mathf.Sin(t * Mathf.PI * 4f);

        // Desánimo: se aplasta hacia abajo de golpe y se recupera despacio
        float caida = Mathf.SmoothStep(0f, 1f, desanimo);

        Vector2 escala = new Vector2(
            1f + crecer + gelatina + aplastamientoBad * 0.4f * caida,
            1f + crecer - gelatina - aplastamientoBad * caida);

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

    private void Aplicar(Vector2 escala)
    {
        rect.localScale = Vector3.Scale(escalaBase, new Vector3(escala.x, escala.y, 1f));

        Vector2 temblor = fallo > 0f
            ? Random.insideUnitCircle * (temblorFallo * fallo)
            : Vector2.zero;

        // El BAD lo hunde un poco, como si se le cayeran los hombros
        Vector2 hundido = Vector2.down * (hundimientoBad * Mathf.SmoothStep(0f, 1f, desanimo));

        rect.anchoredPosition = posicionBase + temblor + hundido;

        // Cálido al acertar, gris al ir mal, apagado al fallar
        if (corazon != null)
        {
            Color color = Color.Lerp(colorBase, colorAcierto, latido);
            color = Color.Lerp(color, colorBad, desanimo);
            corazon.color = Color.Lerp(color, colorFallo, fallo);
        }

        // Sin la luz del acierto, el latido no enciende destello ni halo cálido
        float fuerzaLatido = luzAlAcertar ? latido * latido : 0f;

        // Frente: un velo del color de la calificación actual, que el destello del acierto supera
        if (destello != null)
        {
            Color veloZona = colorZona;
            veloZona.a = brilloFrenteZonaSegura * zonaSegura;

            Color destelloBlanco = new Color(1f, 0.97f, 0.9f, destelloAcierto * fuerzaLatido);

            destello.color = destelloBlanco.a > veloZona.a ? destelloBlanco : veloZona;
        }

        if (luz != null)
        {
            rectLuz.position = rect.TransformPoint(rect.rect.center);
            rectLuz.sizeDelta = rect.rect.size * tamanoLuz;
            rectLuz.localScale = rect.localScale;

            // Base: el color de la calificación actual, o el brillo tenue de cada nota
            Color colorHalo = colorZona;
            colorHalo.a = 0.85f * zonaSegura;

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

    private void HandleBeatGraded(float beat, JudgementResult resultado)
    {
        switch (resultado)
        {
            case JudgementResult.Perfect:
                latido = 1f;
                latidoPerfect = true;
                escalaLatidoActual = escalaPerfect;
                break;

            case JudgementResult.Good:
                latido = 1f;
                latidoPerfect = false;
                escalaLatidoActual = escalaLatido;
                break;

            case JudgementResult.Bad:
                desanimo = 1f;
                break;
        }
    }

    private void HandlePenaltyApplied()
    {
        fallo = 1f;
    }

    #endregion
}
