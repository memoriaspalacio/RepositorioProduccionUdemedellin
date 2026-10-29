using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dibuja la pista por la que viajan las notas y, encima, dónde cae cada calificación,
/// con los mismos colores que el texto que sale al pulsar (ColoresCalificacion):
///   - BAD (lavanda): desde que la ventana se abre hasta acercarse al beat.
///   - GOOD (menta): cerca del beat.
///   - PERFECT (dorado): justo en el beat, con una línea fina en el centro exacto.
///   - Fuera de las zonas no hay nada que pulsar: pulsar ahí es MISS.
///
/// Las zonas son manchas difuminadas y transparentes, y se dibujan por encima del corazón
/// para que GOOD y PERFECT, que son estrechos y caen dentro de él, se vean. La zona en la
/// que está ahora la nota más cercana se aviva, y la de la calificación conseguida destella.
///
/// Todo sale de los mismos datos que usa el juez: los puntos de BeatTrackUI, las
/// tolerancias de BeatActionJudge (que dan BeatWindowZone y BeatDestroyZone) y sus
/// ventanas de PERFECT y GOOD. Mover esas cajas mueve las zonas en el siguiente
/// fotograma, así que lo dibujado siempre coincide con lo que de verdad se califica.
///
/// Con BeatTrackUI en espejo dibuja dos carriles, uno a cada lado del corazón, que se
/// encuentran en él; cada uno pinta sus zonas hasta el centro y el otro pinta el resto.
///
/// Es una vista pura: no decide nada. Construye la pista al entrar en Play, antes que
/// HeartBeatFeedback.
/// </summary>
[DefaultExecutionOrder(-10)]
[DisallowMultipleComponent]
public class BeatTrackLane : MonoBehaviour
{
    [Header("--- Referencias ---")]
    [Tooltip("Pista de notas de la escena (da el punto de aparición y el del corazón). Si es nula se busca al arrancar.")]
    [SerializeField] private BeatTrackUI beatTrackUI;

    [Tooltip("Juez de ritmo de la escena (da la ventana y las calificaciones). Si es nulo se busca al arrancar.")]
    [SerializeField] private BeatActionJudge beatJudge;

    [Tooltip("Opcional, normalmente el corazón. El fondo de la pista se dibuja detrás de él y las zonas de " +
             "calificación justo delante. Vacío: todo detrás de las notas.")]
    [SerializeField] private RectTransform dibujarDetrasDe;

    [Header("--- Forma ---")]
    [Tooltip("Alto del fondo de la pista en unidades de UI.")]
    [Range(4f, 80f)]
    [SerializeField] private float altoPista = 14f;

    [Tooltip("Alto de las zonas de calificación. Más altas que la pista, se leen como un brillo difuminado.")]
    [Range(8f, 120f)]
    [SerializeField] private float altoZonas = 36f;

    [Tooltip("Cuánto asoma el fondo de la pista antes del punto donde aparecen las notas.")]
    [Range(0f, 40f)]
    [SerializeField] private float margenInicio = 10f;

    [Tooltip("Ancho de la línea que marca el beat exacto.")]
    [Range(1f, 10f)]
    [SerializeField] private float anchoLineaBeat = 2f;

    [Header("--- Colores ---")]
    [Tooltip("Fondo de toda la pista.")]
    [SerializeField] private Color colorFondo = new Color(0.04f, 0.05f, 0.12f, 0.35f);

    [Tooltip("Marca donde empieza el ritmo: el punto donde aparecen las notas.")]
    [SerializeField] private Color colorInicio = new Color(0.75f, 0.85f, 1f, 0.8f);

    [Tooltip("Línea del beat exacto, en el centro del PERFECT.")]
    [SerializeField] private Color colorLineaBeat = new Color(1f, 1f, 1f, 0.7f);

    [Tooltip("Opacidad de las zonas en reposo: BAD, GOOD y PERFECT.")]
    [SerializeField] private Vector3 opacidadZonas = new Vector3(0.22f, 0.32f, 0.45f);

    [Header("--- Reacción ---")]
    [Tooltip("Cuánta opacidad gana la zona en la que está ahora la nota más cercana.")]
    [Range(0f, 1f)]
    [SerializeField] private float avivarZonaActual = 0.3f;

    [Tooltip("Segundos que dura el destello de la zona conseguida al acertar.")]
    [Range(0.05f, 1f)]
    [SerializeField] private float duracionDestello = 0.3f;

    // Las tres zonas, en el orden de JudgementResult a partir de Bad
    private const int Zonas = 3;

    /// <summary>Los objetos generados de un carril: uno normal y, en espejo, otro reflejado.</summary>
    private class Carril
    {
        public RectTransform fondo; // detrás del corazón
        public RectTransform zonas; // delante del corazón
        public RectTransform rectPista, rectInicio, rectLinea;
        public Image imgPista, imgInicio, imgLinea;
        public RectTransform[] rectZona = new RectTransform[Zonas];
        public Image[] imgZona = new Image[Zonas];

        // Lado tardío, pasado el corazón: solo con un carril (en espejo lo pinta el otro)
        public RectTransform[] rectZonaTarde = new RectTransform[Zonas];
        public Image[] imgZonaTarde = new Image[Zonas];
    }

    private Carril carril;
    private Carril carrilEspejo;

    // 0..1 suavizado por zona: la nota más cercana está en ella / se acaba de conseguir
    private readonly float[] zonaActual = new float[Zonas];
    private readonly float[] destello = new float[Zonas];

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
            beatJudge.OnBeatGraded += HandleBeatGraded;

        MostrarCarriles(true);
    }

    private void OnDisable()
    {
        if (beatJudge != null)
            beatJudge.OnBeatGraded -= HandleBeatGraded;

        MostrarCarriles(false);
    }

    private void OnDestroy()
    {
        // Pueden vivir bajo otro padre (dibujarDetrasDe), así que se limpian a mano
        Destruir(carril);
        Destruir(carrilEspejo);
    }

    private static void Destruir(Carril c)
    {
        if (c == null)
            return;

        if (c.fondo != null)
            Destroy(c.fondo.gameObject);

        if (c.zonas != null)
            Destroy(c.zonas.gameObject);
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
        if (carril == null)
            return;

        Transform padre = carril.fondo.parent;
        Vector2 aparicion = padre.InverseTransformPoint(beatTrackUI.SpawnPoint.position);
        Vector2 corazon = padre.InverseTransformPoint(beatTrackUI.HeartPoint.position);

        // Con un solo carril, ese carril también pinta el lado tardío, pasado el corazón
        if (!ActualizarGeometria(carril, aparicion, corazon, carrilEspejo == null))
            return;

        if (carrilEspejo != null)
        {
            // El otro lado sale igual de lejos del corazón, reflejado respecto a él
            Vector2 aparicionEspejo = new Vector2(2f * corazon.x - aparicion.x, aparicion.y);
            ActualizarGeometria(carrilEspejo, aparicionEspejo, corazon, false);
        }

        ActualizarColores();
    }

    #region Construcción

    private void Construir()
    {
        carril = ConstruirCarril("PistaRitmo");

        if (beatTrackUI.MirrorTrack)
            carrilEspejo = ConstruirCarril("PistaRitmoEspejo");
    }

    private Carril ConstruirCarril(string nombre)
    {
        Transform padre = dibujarDetrasDe != null ? dibujarDetrasDe.parent : transform;
        Carril nuevo = new Carril();

        nuevo.fondo = CrearRect(nombre, padre);
        nuevo.zonas = CrearRect(nombre + "Zonas", padre);

        if (dibujarDetrasDe != null)
        {
            // Fondo justo detrás del corazón y zonas justo delante
            int indice = dibujarDetrasDe.GetSiblingIndex();
            nuevo.fondo.SetSiblingIndex(indice);
            nuevo.zonas.SetSiblingIndex(dibujarDetrasDe.GetSiblingIndex() + 1);
        }
        else
        {
            nuevo.zonas.SetAsFirstSibling();
            nuevo.fondo.SetAsFirstSibling();
        }

        nuevo.imgPista = CrearImagen("Fondo", nuevo.fondo, SpritesProcedurales.Capsula, out nuevo.rectPista);
        nuevo.imgPista.type = Image.Type.Sliced;

        nuevo.imgInicio = CrearImagen("MarcaInicio", nuevo.fondo, SpritesProcedurales.Capsula, out nuevo.rectInicio);
        nuevo.imgInicio.type = Image.Type.Sliced;

        for (int i = 0; i < Zonas; i++)
        {
            string zona = ((JudgementResult)(i + 1)).ToString();
            nuevo.imgZona[i] = CrearImagen("Zona" + zona, nuevo.zonas, SpritesProcedurales.ZonaDifusa, out nuevo.rectZona[i]);
            nuevo.imgZonaTarde[i] = CrearImagen("Zona" + zona + "Tarde", nuevo.zonas, SpritesProcedurales.ZonaDifusa, out nuevo.rectZonaTarde[i]);
        }

        nuevo.imgLinea = CrearImagen("LineaBeat", nuevo.zonas, SpritesProcedurales.ZonaDifusa, out nuevo.rectLinea);
        return nuevo;
    }

    private void MostrarCarriles(bool visible)
    {
        Mostrar(carril, visible);
        Mostrar(carrilEspejo, visible);
    }

    private static void Mostrar(Carril c, bool visible)
    {
        if (c == null)
            return;

        if (c.fondo != null)
            c.fondo.gameObject.SetActive(visible);

        if (c.zonas != null)
            c.zonas.gameObject.SetActive(visible);
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
    /// Coloca un carril sobre la recta aparición → corazón y reparte las zonas con la misma
    /// conversión beats ↔ distancia que usan las notas. Como el carril gira hacia su
    /// corazón, el reflejado queda del revés solo y no necesita nada aparte.
    /// </summary>
    private bool ActualizarGeometria(Carril c, Vector2 aparicion, Vector2 corazon, bool ladoTardio)
    {
        Vector2 recorrido = corazon - aparicion;
        float largo = recorrido.magnitude;

        if (largo < 1f)
            return false;

        Quaternion giro = Quaternion.Euler(0f, 0f, Mathf.Atan2(recorrido.y, recorrido.x) * Mathf.Rad2Deg);
        Alinear(c.fondo, aparicion, giro);
        Alinear(c.zonas, aparicion, giro);

        // Una nota recorre 'largo' en TravelBeats beats
        float unidadesPorBeat = largo / Mathf.Max(0.01f, beatTrackUI.TravelBeats);
        float unidadesPorSegundo = BeatManager.Instance != null ? unidadesPorBeat / BeatManager.Instance.SecPerBeat : unidadesPorBeat * 2f;

        float temprana = beatJudge != null ? beatJudge.EarlyTolerance * unidadesPorBeat : 0f;
        float tardia = beatJudge != null ? beatJudge.LateTolerance * unidadesPorBeat : 0f;
        float perfect = beatJudge != null ? beatJudge.PerfectWindowSeconds * unidadesPorSegundo : 0f;
        float good = beatJudge != null ? beatJudge.GoodWindowSeconds * unidadesPorSegundo : 0f;

        // Fondo: de antes del inicio hasta el corazón (o hasta el cierre, con un solo carril)
        float xFin = largo + (ladoTardio ? tardia : 0f);
        Colocar(c.rectPista, -margenInicio, xFin + margenInicio, altoPista);
        c.imgPista.pixelsPerUnitMultiplier = 64f / altoPista;

        float anchoInicio = Mathf.Max(3f, altoPista * 0.25f);
        Colocar(c.rectInicio, -anchoInicio * 0.5f, anchoInicio, altoPista * 1.8f);
        c.imgInicio.pixelsPerUnitMultiplier = 64f / anchoInicio;

        // Distancias al corazón de cada borde: las zonas se tocan sin solaparse, para que
        // los colores no se mezclen
        float tempranaGood = Mathf.Min(good, temprana);
        float tempranaPerfect = Mathf.Min(perfect, temprana);
        float tardiaGood = Mathf.Min(good, tardia);
        float tardiaPerfect = Mathf.Min(perfect, tardia);

        // En espejo, el centro lo comparten los dos carriles: cada uno pasa un poco del
        // corazón para que sus bordes difuminados no dejen un hueco justo en el beat
        float finPerfect = ladoTardio ? tardiaPerfect : Mathf.Max(2f, perfect * 0.5f);

        ColocarZona(c.rectZona[0], largo - temprana, largo - tempranaGood);
        ColocarZona(c.rectZona[1], largo - tempranaGood, largo - tempranaPerfect);
        ColocarZona(c.rectZona[2], largo - tempranaPerfect, largo + finPerfect);

        ColocarZona(c.rectZonaTarde[0], largo + tardiaGood, largo + (ladoTardio ? tardia : tardiaGood));
        ColocarZona(c.rectZonaTarde[1], largo + tardiaPerfect, largo + (ladoTardio ? tardiaGood : tardiaPerfect));
        ColocarZona(c.rectZonaTarde[2], largo, largo); // el PERFECT tardío ya va en la zona de arriba

        Colocar(c.rectLinea, largo - anchoLineaBeat * 0.5f, anchoLineaBeat, altoZonas * 0.9f);
        return true;
    }

    private static void Alinear(RectTransform contenedor, Vector2 aparicion, Quaternion giro)
    {
        contenedor.localPosition = aparicion;
        contenedor.localRotation = giro;
        contenedor.localScale = Vector3.one;
    }

    private void ColocarZona(RectTransform rect, float desde, float hasta)
    {
        float ancho = hasta - desde;
        if (ancho <= 0.01f)
        {
            Colocar(rect, desde, 0f, 0f);
            return;
        }

        // Se ensancha un poco para que el difuminado de los bordes caiga sobre la frontera
        float extra = Mathf.Max(1.5f, ancho * 0.12f);
        Colocar(rect, desde - extra, ancho + extra * 2f, altoZonas);
    }

    private void ActualizarColores()
    {
        float dt = Time.unscaledDeltaTime;
        float caida = dt / Mathf.Max(0.01f, duracionDestello);

        JudgementResult ahora = beatJudge != null ? beatJudge.GradeIfPressedNow : JudgementResult.Miss;

        for (int i = 0; i < Zonas; i++)
        {
            destello[i] = Mathf.MoveTowards(destello[i], 0f, caida);
            zonaActual[i] = Mathf.MoveTowards(zonaActual[i], (int)ahora == i + 1 ? 1f : 0f, dt * 10f);
        }

        PintarCarril(carril);

        if (carrilEspejo != null)
            PintarCarril(carrilEspejo);
    }

    private void PintarCarril(Carril c)
    {
        c.imgPista.color = colorFondo;
        c.imgInicio.color = colorInicio;
        c.imgLinea.color = colorLineaBeat;

        for (int i = 0; i < Zonas; i++)
        {
            Color color = ColoresCalificacion.De((JudgementResult)(i + 1));
            color.a = Mathf.Clamp01(opacidadZonas[i] + avivarZonaActual * zonaActual[i] + 0.5f * destello[i]);
            c.imgZona[i].color = color;
            c.imgZonaTarde[i].color = color;
        }
    }

    private void HandleBeatGraded(float beat, JudgementResult resultado)
    {
        if (resultado != JudgementResult.Miss)
            destello[(int)resultado - 1] = 1f;
    }

    #endregion
}
