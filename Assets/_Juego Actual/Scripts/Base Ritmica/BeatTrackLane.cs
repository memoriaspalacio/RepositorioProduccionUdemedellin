using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dibuja la pista por la que viajan las notas, segmentada por colores para que se lea
/// de un vistazo:
///   - Inicio: la marca donde aparecen las notas, donde empieza el ritmo.
///   - Aproximación: el tramo por el que se acercan, todavía sin poder actuar.
///   - Zona segura: mientras la nota está aquí, pulsar cuenta como acierto. Lleva la
///     línea del beat exacto (donde la nota llega al corazón).
///   - Pérdida: si la nota llega aquí sin pulsar, se perdió.
///
/// Todo sale de los mismos datos que usa el juez: los puntos de BeatTrackUI y las
/// tolerancias de BeatActionJudge (que a su vez dan BeatWindowZone y BeatDestroyZone).
/// Mover esas cajas mueve los segmentos en el siguiente fotograma, así que lo dibujado
/// siempre coincide con lo que de verdad se puede acertar.
///
/// Con el corazón asignado en dibujarDetrasDe, la pista queda detrás de él: la zona segura
/// cae dentro del corazón (ahí la señala HeartBeatFeedback iluminándolo en verde) y la
/// cola roja de pérdida asoma por su borde derecho.
///
/// Es una vista pura: no decide nada. Construye la pista al entrar en Play, antes que
/// HeartBeatFeedback, para quedar también detrás del halo del corazón.
/// </summary>
[DefaultExecutionOrder(-10)]
[DisallowMultipleComponent]
public class BeatTrackLane : MonoBehaviour
{
    [Header("--- Referencias ---")]
    [Tooltip("Pista de notas de la escena (da el punto de aparición y el del corazón). Si es nula se busca al arrancar.")]
    [SerializeField] private BeatTrackUI beatTrackUI;

    [Tooltip("Juez de ritmo de la escena (da la ventana de acción). Si es nulo se busca al arrancar.")]
    [SerializeField] private BeatActionJudge beatJudge;

    [Tooltip("Opcional. Si se asigna (por ejemplo el corazón), la pista se dibuja justo detrás de ese objeto. " +
             "Vacío: se dibuja delante del corazón y detrás de las notas.")]
    [SerializeField] private RectTransform dibujarDetrasDe;

    [Header("--- Forma ---")]
    [Tooltip("Alto de la pista en unidades de UI.")]
    [Range(8f, 80f)]
    [SerializeField] private float altoPista = 30f;

    [Tooltip("Largo de la zona de pérdida después de que se cierra la ventana, en beats.")]
    [Range(0.1f, 2f)]
    [SerializeField] private float beatsPerdida = 0.5f;

    [Tooltip("Con la pista detrás de un objeto (el corazón), cuánto asoma como mínimo la cola roja de " +
             "pérdida por su borde derecho, en unidades de UI.")]
    [Range(0f, 40f)]
    [SerializeField] private float colaVisiblePerdida = 16f;

    [Tooltip("Cuánto asoma el fondo de la pista antes del punto donde aparecen las notas.")]
    [Range(0f, 40f)]
    [SerializeField] private float margenInicio = 12f;

    [Tooltip("Ancho de la línea que marca el beat exacto.")]
    [Range(1f, 10f)]
    [SerializeField] private float anchoLineaBeat = 3f;

    [Header("--- Colores ---")]
    [Tooltip("Fondo de toda la pista.")]
    [SerializeField] private Color colorFondo = new Color(0.04f, 0.05f, 0.12f, 0.5f);

    [Tooltip("Marca donde empieza el ritmo: el punto donde aparecen las notas.")]
    [SerializeField] private Color colorInicio = new Color(0.75f, 0.85f, 1f, 0.95f);

    [Tooltip("Tramo por el que se acercan las notas antes de poder actuar. Entra en degradado desde el inicio.")]
    [SerializeField] private Color colorAproximacion = new Color(0.35f, 0.55f, 1f, 0.35f);

    [Tooltip("Zona segura: mientras la nota está aquí, pulsar cuenta como acierto.")]
    [SerializeField] private Color colorZonaSegura = new Color(0.3f, 1f, 0.55f, 0.5f);

    [Tooltip("Línea del beat exacto, donde la nota llega al corazón.")]
    [SerializeField] private Color colorLineaBeat = new Color(1f, 0.95f, 0.75f, 1f);

    [Tooltip("Zona de pérdida: si la nota llega aquí sin pulsar, se perdió. Se desvanece hacia el final.")]
    [SerializeField] private Color colorPerdida = new Color(1f, 0.18f, 0.22f, 0.6f);

    [Header("--- Reacción ---")]
    [Tooltip("Cuánto se aviva la zona segura (y su halo) mientras hay una nota dentro de la ventana.")]
    [Range(0f, 1f)]
    [SerializeField] private float brilloConNotaDentro = 0.6f;

    [Tooltip("Segundos que dura el destello de la zona segura al acertar, y el de la zona de pérdida al fallar.")]
    [Range(0.05f, 1f)]
    [SerializeField] private float duracionDestello = 0.3f;

    // Objetos generados al arrancar
    private RectTransform contenedor;
    private RectTransform rectBrillo, rectFondo, rectAproximacion, rectZonaSegura, rectLineaBeat, rectPerdida, rectInicio;
    private Image imgBrillo, imgFondo, imgAproximacion, imgZonaSegura, imgLineaBeat, imgPerdida, imgInicio;

    // Borde derecho de dibujarDetrasDe en reposo, en el espacio del padre de la pista
    private Vector2 bordeDerechoTapa;

    // 0..1 suavizado: hay una nota dentro de la ventana ahora mismo
    private float notaDentro;

    // 1 al acertar / al penalizar, y caen a 0 en duracionDestello
    private float destelloAcierto;
    private float destelloFallo;

    private void Awake()
    {
        if (beatTrackUI == null)
            beatTrackUI = FindFirstObjectByType<BeatTrackUI>();

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

        if (contenedor != null)
            contenedor.gameObject.SetActive(true);
    }

    private void OnDisable()
    {
        if (beatJudge != null)
        {
            beatJudge.OnBeatConsumed -= HandleBeatConsumed;
            beatJudge.OnPenaltyApplied -= HandlePenaltyApplied;
        }

        if (contenedor != null)
            contenedor.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        // Puede vivir bajo otro padre (dibujarDetrasDe), así que se limpia a mano
        if (contenedor != null)
            Destroy(contenedor.gameObject);
    }

    private void Start()
    {
        if (beatTrackUI == null ||
            beatTrackUI.SpawnPoint == null ||
            beatTrackUI.HeartPoint == null)
        {
            Debug.LogWarning("BeatTrackLane: no hay un BeatTrackUI con punto de aparición y corazón en la escena.", this);
            enabled = false;
            return;
        }

        Construir();
    }

    private void LateUpdate()
    {
        if (contenedor == null)
            return;

        if (!ActualizarGeometria())
            return;

        ActualizarColores();
    }

    #region Construcción

    private void Construir()
    {
        Transform padre = dibujarDetrasDe != null ? dibujarDetrasDe.parent : transform;

        contenedor = CrearRect("PistaRitmo", padre);

        if (dibujarDetrasDe != null)
        {
            contenedor.SetSiblingIndex(dibujarDetrasDe.GetSiblingIndex());

            // Se mide ahora, antes de que el corazón empiece a latir y cambiar de escala
            Vector3[] esquinas = new Vector3[4];
            dibujarDetrasDe.GetWorldCorners(esquinas);
            bordeDerechoTapa = padre.InverseTransformPoint((esquinas[2] + esquinas[3]) * 0.5f);
        }
        else
        {
            contenedor.SetAsFirstSibling();
        }

        // El halo va fuera del fondo para que no lo recorte la máscara
        imgBrillo = CrearImagen("HaloZonaSegura", contenedor, SpritesProcedurales.BrilloRadial, out rectBrillo);

        imgFondo = CrearImagen("Fondo", contenedor, SpritesProcedurales.Capsula, out rectFondo);
        imgFondo.type = Image.Type.Sliced;

        // Los tramos van dentro del fondo, recortados a su forma de cápsula
        Mask mascara = imgFondo.gameObject.AddComponent<Mask>();
        mascara.showMaskGraphic = true;

        imgAproximacion = CrearImagen("Aproximacion", rectFondo, SpritesProcedurales.DegradadoEntrada, out rectAproximacion);
        imgZonaSegura = CrearImagen("ZonaSegura", rectFondo, null, out rectZonaSegura);
        // Detrás del corazón solo se ve la punta de la cola: va sólida para que no asome ya desvanecida
        Sprite spritePerdida = dibujarDetrasDe != null ? null : SpritesProcedurales.DegradadoSalida;
        imgPerdida = CrearImagen("Perdida", rectFondo, spritePerdida, out rectPerdida);
        imgLineaBeat = CrearImagen("LineaBeat", rectFondo, null, out rectLineaBeat);

        imgInicio = CrearImagen("MarcaInicio", contenedor, SpritesProcedurales.Capsula, out rectInicio);
        imgInicio.type = Image.Type.Sliced;
    }

    // Todos los rects se anclan a la izquierda-centro de su padre y crecen hacia la derecha,
    // así una x es la distancia recorrida desde ese borde.
    private static RectTransform CrearRect(string nombre, Transform padre)
    {
        GameObject objeto = new GameObject(nombre, typeof(RectTransform));
        objeto.layer = padre.gameObject.layer;

        RectTransform rect = (RectTransform)objeto.transform;
        rect.SetParent(padre, false);
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = Vector2.zero;

        return rect;
    }

    private static Image CrearImagen(string nombre, Transform padre, Sprite sprite, out RectTransform rect)
    {
        rect = CrearRect(nombre, padre);

        Image imagen = rect.gameObject.AddComponent<Image>();
        imagen.sprite = sprite;
        imagen.raycastTarget = false;

        return imagen;
    }

    private static void Colocar(RectTransform rect, float x, float ancho, float alto)
    {
        rect.anchoredPosition = new Vector2(x, 0f);
        rect.sizeDelta = new Vector2(Mathf.Max(0f, ancho), alto);
    }

    #endregion

    #region Actualización

    /// <summary>
    /// Coloca la pista sobre la recta aparición → corazón y reparte los tramos con la
    /// misma conversión beats ↔ distancia que usan las notas y las zonas.
    /// </summary>
    private bool ActualizarGeometria()
    {
        Transform padre = contenedor.parent;

        Vector2 aparicion = padre.InverseTransformPoint(beatTrackUI.SpawnPoint.position);
        Vector2 corazon = padre.InverseTransformPoint(beatTrackUI.HeartPoint.position);

        Vector2 recorrido = corazon - aparicion;
        float largo = recorrido.magnitude;

        if (largo < 1f)
            return false;

        // La pista sigue la misma recta que recorren las notas
        contenedor.localPosition = aparicion;
        contenedor.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(recorrido.y, recorrido.x) * Mathf.Rad2Deg);
        contenedor.localScale = Vector3.one;

        // Una nota recorre 'largo' en TravelBeats beats
        float unidadesPorBeat = largo / Mathf.Max(0.01f, beatTrackUI.TravelBeats);
        float toleranciaTemprana = beatJudge != null ? beatJudge.EarlyTolerance : 0f;
        float toleranciaTardia = beatJudge != null ? beatJudge.LateTolerance : 0f;

        float xApertura = Mathf.Clamp(largo - toleranciaTemprana * unidadesPorBeat, 0f, largo);
        float xCierre = largo + toleranciaTardia * unidadesPorBeat;
        float xFin = xCierre + beatsPerdida * unidadesPorBeat;
        float xFondo = -margenInicio;

        // Que la cola roja asome por detrás del corazón en vez de quedar tapada
        if (dibujarDetrasDe != null)
        {
            float xBordeTapa = Vector2.Dot(bordeDerechoTapa - aparicion, recorrido / largo);
            xFin = Mathf.Max(xFin, xBordeTapa + colaVisiblePerdida);
        }

        Colocar(rectFondo, xFondo, xFin - xFondo, altoPista);
        imgFondo.pixelsPerUnitMultiplier = 64f / altoPista;

        // Dentro del fondo las x se miden desde su borde izquierdo
        Colocar(rectAproximacion, -xFondo, xApertura, altoPista);
        Colocar(rectZonaSegura, xApertura - xFondo, xCierre - xApertura, altoPista);
        Colocar(rectPerdida, xCierre - xFondo, xFin - xCierre, altoPista);
        Colocar(rectLineaBeat, largo - anchoLineaBeat * 0.5f - xFondo, anchoLineaBeat, altoPista);

        // Marca de inicio: una píldora vertical que sobresale de la pista
        float anchoInicio = Mathf.Max(4f, altoPista * 0.18f);
        Colocar(rectInicio, -anchoInicio * 0.5f, anchoInicio, altoPista * 1.5f);
        imgInicio.pixelsPerUnitMultiplier = 64f / anchoInicio;

        // Halo de la zona segura: más ancho y alto que el tramo, centrado en él
        float anchoSegura = Mathf.Max(xCierre - xApertura, anchoLineaBeat);
        float anchoHalo = anchoSegura + altoPista * 2f;
        Colocar(rectBrillo, (xApertura + xCierre - anchoHalo) * 0.5f, anchoHalo, altoPista * 3f);

        return true;
    }

    private void ActualizarColores()
    {
        float dt = Time.unscaledDeltaTime;
        float caida = dt / Mathf.Max(0.01f, duracionDestello);

        destelloAcierto = Mathf.MoveTowards(destelloAcierto, 0f, caida);
        destelloFallo = Mathf.MoveTowards(destelloFallo, 0f, caida);
        notaDentro = Mathf.MoveTowards(notaDentro, HayNotaEnVentana() ? 1f : 0f, dt * 8f);

        imgFondo.color = colorFondo;
        imgInicio.color = colorInicio;
        imgAproximacion.color = colorAproximacion;

        // La zona segura se aviva con una nota dentro y destella hacia blanco al acertar
        Color segura = colorZonaSegura;
        segura.a = Mathf.Clamp01(segura.a * (1f + brilloConNotaDentro * notaDentro));
        Color seguraDestello = new Color(1f, 1f, 1f, Mathf.Max(segura.a, 0.85f));
        imgZonaSegura.color = Color.Lerp(segura, seguraDestello, destelloAcierto * 0.7f);

        Color halo = colorZonaSegura;
        halo.a = Mathf.Clamp01(0.35f * brilloConNotaDentro * notaDentro + 0.6f * destelloAcierto);
        imgBrillo.color = halo;

        Color linea = colorLineaBeat;
        linea.a *= Mathf.Lerp(0.7f, 1f, Mathf.Max(notaDentro, destelloAcierto));
        imgLineaBeat.color = linea;

        // La zona de pérdida se enciende cuando el juez cobra una penalización
        Color perdidaDestello = new Color(1f, 0.45f, 0.45f, Mathf.Max(colorPerdida.a, 0.9f));
        imgPerdida.color = Color.Lerp(colorPerdida, perdidaDestello, destelloFallo);
    }

    /// <summary>True si alguna nota sin resolver tiene su ventana abierta ahora mismo.</summary>
    private bool HayNotaEnVentana()
    {
        return beatJudge != null && beatJudge.IsWindowOpen;
    }

    #endregion

    #region Eventos del juez

    private void HandleBeatConsumed(float beat)
    {
        destelloAcierto = 1f;
    }

    private void HandlePenaltyApplied()
    {
        destelloFallo = 1f;
    }

    #endregion
}
