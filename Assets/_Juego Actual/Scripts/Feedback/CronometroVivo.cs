using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Hace que el cronometro (TextoContrarreloj) se sienta: ControladorContrarreloj lo pone solo en
/// el texto y aqui se decide todo lo que se ve. Tiene el acabado de las calificaciones (negrita,
/// degradado, contorno y sombra dura) y reacciona a tres cosas:
///   - Queda poco: por debajo de cada umbral se calienta de color, late con cada segundo (en el
///     critico con latido doble, como un corazon), tiembla, muestra decimas y oscurece los bordes
///     de la pantalla con una vineta roja que respira.
///   - Gana tiempo: los numeros suben rodando, rebote dorado, "+X" que salta y destellos.
///   - Pierde tiempo: mas fuerte que ganar. Los numeros caen rodando, se aplasta y estira,
///     tiembla fuerte, "-X" grande que cae con peso, una copia roja que estalla y un destello
///     rojo en los bordes de la pantalla.
/// Todo va con tiempo sin escalar, como el propio cronometro.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(TMP_Text))]
public class CronometroVivo : MonoBehaviour
{
    [Header("Umbrales (segundos)")]
    [Tooltip("Por debajo empieza a calentarse y a latir suave.")]
    [SerializeField] private float umbralAviso = 60f;
    [Tooltip("Por debajo se pone rojo, late fuerte y tiembla.")]
    [SerializeField] private float umbralUrgente = 30f;
    [Tooltip("Por debajo muestra decimas, late doble, tiembla siempre y aparece la vineta.")]
    [SerializeField] private float umbralCritico = 10f;

    [Header("Colores (arriba / abajo del degradado)")]
    [SerializeField] private Color normalArriba = new Color(1f, 0.97f, 0.9f, 1f);
    [SerializeField] private Color normalAbajo = new Color(0.86f, 0.8f, 0.72f, 1f);
    [SerializeField] private Color avisoArriba = new Color(1f, 0.9f, 0.45f, 1f);
    [SerializeField] private Color avisoAbajo = new Color(1f, 0.52f, 0.12f, 1f);
    [SerializeField] private Color urgenteArriba = new Color(1f, 0.55f, 0.5f, 1f);
    [SerializeField] private Color urgenteAbajo = new Color(0.85f, 0.06f, 0.12f, 1f);
    [SerializeField] private Color ganarArriba = new Color(0.9f, 1f, 0.6f, 1f);
    [SerializeField] private Color ganarAbajo = new Color(0.25f, 0.85f, 0.45f, 1f);
    [SerializeField] private Color perderArriba = new Color(1f, 0.4f, 0.35f, 1f);
    [SerializeField] private Color perderAbajo = new Color(0.55f, 0f, 0.06f, 1f);

    [Header("Ganar tiempo")]
    [SerializeField, Min(0.1f)] private float duracionGanar = 0.7f;
    [SerializeField, Range(1f, 2f)] private float escalaGanar = 1.35f;

    [Header("Perder tiempo")]
    [SerializeField, Min(0.1f)] private float duracionPerder = 0.85f;
    [SerializeField, Range(1f, 2.5f)] private float escalaPerder = 1.55f;
    [Tooltip("Temblor al perder tiempo, en pixeles de UI.")]
    [SerializeField, Range(0f, 40f)] private float temblorPerder = 16f;

    [Header("Numeros que ruedan")]
    [Tooltip("Segundos que tardan los numeros en llegar al valor nuevo al ganar o perder tiempo.")]
    [SerializeField, Min(0f)] private float duracionRodar = 0.45f;

    [Header("Textos flotantes")]
    [Tooltip("Tamano del \"-X\" al perder tiempo.")]
    [SerializeField] private float tamanoFlotante = 46f;
    [Tooltip("Tamano del \"+X\" al ganar tiempo: discreto, ganar no debe tapar el juego.")]
    [SerializeField] private float tamanoGanancia = 28f;
    [SerializeField] private Vector2 desplazamientoFlotante = new Vector2(20f, -45f);

    [Header("Vineta de pantalla")]
    [SerializeField] private bool usarVineta = true;
    [SerializeField, Range(0f, 1f)] private float vinetaCritico = 0.32f;
    [SerializeField, Range(0f, 1f)] private float vinetaPerder = 0.5f;
    [SerializeField, Range(0f, 1f)] private float vinetaGanar = 0.2f;

    static Texture2D texturaVineta;

    TMP_Text texto;
    RectTransform rect;
    Vector3 escalaBase;
    Vector2 posicionBase;
    Quaternion giroBase;

    float mostrado;
    float rodarDesde;
    float rodarEdad = float.PositiveInfinity;

    float edadGanar = float.PositiveInfinity;
    float edadPerder = float.PositiveInfinity;

    RawImage vineta;

    public static string Formatear(float segundos, bool decimas)
    {
        if (decimas)
        {
            float valor = Mathf.Max(0f, segundos);
            int enteros = Mathf.FloorToInt(valor);
            int decima = Mathf.Clamp(Mathf.FloorToInt((valor - enteros) * 10f), 0, 9);
            return $"{enteros:00}.{decima}";
        }

        int total = Mathf.CeilToInt(Mathf.Max(0f, segundos));
        return $"{total / 60:00}:{total % 60:00}";
    }

    void Awake()
    {
        texto = GetComponent<TMP_Text>();
        rect = texto.rectTransform;
        escalaBase = rect.localScale;
        posicionBase = rect.anchoredPosition;
        giroBase = rect.localRotation;
        TextoFlotante.EstiloCaricatura(texto, 0.26f);

        ControladorContrarreloj reloj = ControladorContrarreloj.Instancia;
        mostrado = reloj != null ? reloj.TiempoRestante : 0f;
    }

    void OnEnable()
    {
        ControladorContrarreloj.AlCambiarTiempo += AlCambiarTiempo;
    }

    void OnDisable()
    {
        ControladorContrarreloj.AlCambiarTiempo -= AlCambiarTiempo;

        if (rect != null)
        {
            rect.localScale = escalaBase;
            rect.anchoredPosition = posicionBase;
            rect.localRotation = giroBase;
        }

        if (vineta != null)
            Destroy(vineta.canvas.gameObject);
    }

    void AlCambiarTiempo(float diferencia, bool conTexto)
    {
        ControladorContrarreloj reloj = ControladorContrarreloj.Instancia;
        if (reloj == null)
            return;

        // Los numeros parten de lo que se veia y ruedan hasta el valor nuevo
        rodarDesde = mostrado;
        rodarEdad = 0f;

        if (diferencia > 0f)
        {
            edadGanar = 0f;
            if (conTexto)
                TextoFlotante.Crear(rect, $"+{diferencia:0.#}", desplazamientoFlotante, new Vector2(0f, 45f),
                    ganarArriba, ganarAbajo, tamanoGanancia, 0.9f, false, texto.font);
        }
        else
        {
            edadPerder = 0f;
            TextoFlotante.Crear(rect, $"{diferencia:0.#}", desplazamientoFlotante, new Vector2(10f, -95f),
                perderArriba, perderAbajo, tamanoFlotante * 1.2f, 1.2f, true, texto.font);
            Estallido();
        }
    }

    void LateUpdate()
    {
        ControladorContrarreloj reloj = ControladorContrarreloj.Instancia;
        if (reloj == null)
            return;

        float dt = Time.unscaledDeltaTime;
        float real = reloj.TiempoRestante;

        // Numeros: siguen al reloj; tras ganar o perder, ruedan del valor anterior al nuevo
        rodarEdad += dt;
        if (rodarEdad < duracionRodar)
        {
            float t = rodarEdad / duracionRodar;
            mostrado = Mathf.Lerp(rodarDesde, real, 1f - (1f - t) * (1f - t) * (1f - t));
        }
        else
        {
            mostrado = real;
        }

        bool critico = mostrado < umbralCritico && reloj.EstaContando;
        texto.text = Formatear(mostrado, critico);

        // Urgencia: 0 por encima del aviso, 1 al llegar al umbral urgente, 2 en el critico
        float urgencia = mostrado >= umbralAviso ? 0f
            : mostrado >= umbralUrgente ? Mathf.InverseLerp(umbralAviso, umbralUrgente, mostrado)
            : 1f + Mathf.InverseLerp(umbralUrgente, umbralCritico, mostrado);
        if (!reloj.EstaContando)
            urgencia = 0f;

        Color arriba = urgencia <= 1f ? Color.Lerp(normalArriba, avisoArriba, urgencia) : Color.Lerp(avisoArriba, urgenteArriba, urgencia - 1f);
        Color abajo = urgencia <= 1f ? Color.Lerp(normalAbajo, avisoAbajo, urgencia) : Color.Lerp(avisoAbajo, urgenteAbajo, urgencia - 1f);

        // Latido con cada segundo que cae: justo al cambiar el numero es cuando golpea
        float fase = mostrado - Mathf.Floor(mostrado);
        float latido = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.7f, 1f, fase));
        if (critico)
            latido = Mathf.Max(latido, 0.6f * Mathf.SmoothStep(0f, 1f, 1f - Mathf.Abs(fase - 0.55f) / 0.12f));
        float fuerzaLatido = urgencia * 0.09f;
        float escala = 1f + latido * fuerzaLatido;

        // Temblor de fondo en el critico, y en el urgente solo al latir
        Vector2 temblor = Vector2.zero;
        float giro = 0f;
        if (urgencia > 1f)
        {
            float nervio = (urgencia - 1f) * (critico ? 2.2f : latido * 1.6f);
            temblor = Random.insideUnitCircle * nervio;
            giro = Mathf.Sin(Time.unscaledTime * 31f) * nervio * 0.8f;
        }

        // Ganar: rebote elastico, salto hacia arriba y oro
        edadGanar += dt;
        float ganar = Mathf.Clamp01(1f - edadGanar / duracionGanar);
        Vector2 escalaXY = Vector2.one * escala;
        if (ganar > 0f)
        {
            float rebote = Mathf.Exp(-edadGanar * 7f) * Mathf.Cos(edadGanar * 22f);
            escalaXY *= 1f + (escalaGanar - 1f) * rebote;
            temblor += new Vector2(0f, 10f * ganar * ganar);
            arriba = Color.Lerp(arriba, ganarArriba, ganar);
            abajo = Color.Lerp(abajo, ganarAbajo, ganar);
        }

        // Perder: aplasta y estira (como un golpe), temblor fuerte y rojo intenso
        edadPerder += dt;
        float perder = Mathf.Clamp01(1f - edadPerder / duracionPerder);
        if (perder > 0f)
        {
            float golpe = Mathf.Exp(-edadPerder * 9f);
            float oscilacion = Mathf.Cos(edadPerder * 30f) * golpe;
            escalaXY.x *= 1f + (escalaPerder - 1f) * oscilacion;
            escalaXY.y *= 1f - (escalaPerder - 1f) * 0.45f * oscilacion;
            temblor += Random.insideUnitCircle * temblorPerder * perder * perder;
            giro += Random.Range(-8f, 8f) * perder * perder;
            arriba = Color.Lerp(arriba, perderArriba, Mathf.Sqrt(perder));
            abajo = Color.Lerp(abajo, perderAbajo, Mathf.Sqrt(perder));
        }

        rect.localScale = new Vector3(escalaBase.x * escalaXY.x, escalaBase.y * escalaXY.y, escalaBase.z);
        rect.anchoredPosition = posicionBase + temblor;
        rect.localRotation = giroBase * Quaternion.Euler(0f, 0f, giro);
        TextoFlotante.Degradado(texto, arriba, abajo);

        ActualizarVineta(critico ? 1f : Mathf.Clamp01(urgencia - 1f) * 0.35f, latido, ganar, perder);
    }

    /// <summary>Una copia roja del cronometro que crece y se desvanece, como una onda del golpe.</summary>
    void Estallido()
    {
        TextoFlotante.Crear(rect, texto.text, Vector2.zero, Vector2.zero, perderArriba,
            new Color(perderAbajo.r, perderAbajo.g, perderAbajo.b, 0.6f), texto.fontSize, 0.45f, false, texto.font, 3.2f);
    }

    #region Vineta

    void ActualizarVineta(float critico, float latido, float ganar, float perder)
    {
        if (!usarVineta)
            return;

        float rojo = critico * vinetaCritico * (0.55f + 0.45f * latido) + vinetaPerder * perder * perder;
        float oro = vinetaGanar * ganar * ganar;
        if (rojo <= 0.001f && oro <= 0.001f)
        {
            if (vineta != null)
                vineta.enabled = false;
            return;
        }

        if (vineta == null)
            CrearVineta();

        vineta.enabled = true;
        Color colorRojo = new Color(0.7f, 0.02f, 0.06f, rojo);
        Color colorOro = new Color(1f, 0.8f, 0.3f, oro);
        vineta.color = rojo >= oro ? colorRojo : colorOro;
    }

    void CrearVineta()
    {
        // Canvas propio por encima del juego y del HUD, pero por debajo de los menus
        // Suelto en la escena (se va con ella): dentro de otro Canvas heredaria su orden
        GameObject objeto = new GameObject("Vineta cronometro", typeof(RectTransform));
        Canvas canvas = objeto.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;

        GameObject imagen = new GameObject("Vineta", typeof(RectTransform));
        RectTransform rectImagen = (RectTransform)imagen.transform;
        rectImagen.SetParent(objeto.transform, false);
        rectImagen.anchorMin = Vector2.zero;
        rectImagen.anchorMax = Vector2.one;
        rectImagen.offsetMin = Vector2.zero;
        rectImagen.offsetMax = Vector2.zero;

        vineta = imagen.AddComponent<RawImage>();
        vineta.texture = TexturaVineta();
        vineta.raycastTarget = false;
    }

    static Texture2D TexturaVineta()
    {
        if (texturaVineta != null)
            return texturaVineta;

        const int lado = 128;
        texturaVineta = new Texture2D(lado, lado, TextureFormat.RGBA32, false)
        {
            name = "VinetaCronometro",
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color[] pixeles = new Color[lado * lado];
        for (int y = 0; y < lado; y++)
        {
            for (int x = 0; x < lado; x++)
            {
                float u = x / (lado - 1f) * 2f - 1f;
                float v = y / (lado - 1f) * 2f - 1f;
                // Mas de lado que de arriba: la pantalla es ancha
                float distancia = Mathf.Sqrt(u * u * 0.9f + v * v * 1.1f);
                float alfa = Mathf.SmoothStep(0.55f, 1.25f, distancia);
                pixeles[y * lado + x] = new Color(1f, 1f, 1f, alfa * alfa);
            }
        }

        texturaVineta.SetPixels(pixeles);
        texturaVineta.Apply();
        return texturaVineta;
    }

    #endregion
}
