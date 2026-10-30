using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Piezas de UI hechas por codigo para el menu de opciones y el contador de FPS. Asi no
/// dependen de ningun prefab ni de que alguien los ponga en las escenas: funcionan en
/// cualquier escena tal cual.
/// </summary>
public static class ConstructorUI
{
    public static readonly Color ColorPanel = new Color(0.055f, 0.05f, 0.07f, 0.97f);
    public static readonly Color ColorBorde = new Color(0.72f, 0.6f, 0.38f, 0.55f);
    public static readonly Color ColorTitulo = new Color(0.93f, 0.82f, 0.56f);
    public static readonly Color ColorTexto = new Color(0.92f, 0.9f, 0.86f);
    public static readonly Color ColorTenue = new Color(0.62f, 0.6f, 0.58f);
    public static readonly Color ColorBoton = new Color(0.16f, 0.14f, 0.18f, 1f);
    public static readonly Color ColorBotonResaltado = new Color(0.32f, 0.26f, 0.2f, 1f);
    public static readonly Color ColorFila = new Color(1f, 1f, 1f, 0.035f);

    static TMP_FontAsset fuente;

    /// <summary>
    /// La tipografia de texto del juego: la de EstiloMenu, o Rijusans si ya esta cargada por el
    /// HUD o el menu; si no, la de TextMesh Pro por defecto.
    /// </summary>
    public static TMP_FontAsset Fuente
    {
        get
        {
            if (fuente != null)
                return fuente;

            fuente = EstiloMenu.Actual.fuenteTexto;
            if (fuente != null)
                return fuente;

            TMP_FontAsset[] cargadas = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            for (int i = 0; i < cargadas.Length; i++)
            {
                if (cargadas[i].name.Contains("Rijusans"))
                {
                    fuente = cargadas[i];
                    return fuente;
                }
            }

            fuente = TMP_Settings.defaultFontAsset;
            return fuente;
        }
    }

    /// <summary>Canvas superpuesto a todo, escalado como una pantalla de 1920x1080.</summary>
    public static Canvas CrearCanvas(string nombre, int orden, Transform padre = null)
    {
        GameObject objeto = new GameObject(nombre, typeof(RectTransform));
        if (padre != null)
            objeto.transform.SetParent(padre, false);

        Canvas canvas = objeto.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = orden;

        CanvasScaler escalador = objeto.AddComponent<CanvasScaler>();
        escalador.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escalador.referenceResolution = new Vector2(1920f, 1080f);
        escalador.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        escalador.matchWidthOrHeight = 0.5f;

        objeto.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    /// <summary>Crea un EventSystem si la escena no tiene ninguno (sin el los botones no responden).</summary>
    public static GameObject AsegurarEventSystem(Transform padre)
    {
        if (EventSystem.current != null || Object.FindAnyObjectByType<EventSystem>() != null)
            return null;

        GameObject objeto = new GameObject("EventSystem (Menu)", typeof(EventSystem), typeof(InputSystemUIInputModule));
        objeto.transform.SetParent(padre, false);
        return objeto;
    }

    public static RectTransform CrearRect(string nombre, Transform padre)
    {
        GameObject objeto = new GameObject(nombre, typeof(RectTransform));
        RectTransform rect = (RectTransform)objeto.transform;
        rect.SetParent(padre, false);
        return rect;
    }

    /// <summary>Estira el rect a todo su padre, con un margen opcional.</summary>
    public static void Llenar(RectTransform rect, float margen = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(margen, margen);
        rect.offsetMax = new Vector2(-margen, -margen);
    }

    public static Image CrearImagen(string nombre, Transform padre, Color color)
    {
        RectTransform rect = CrearRect(nombre, padre);
        Image imagen = rect.gameObject.AddComponent<Image>();
        imagen.color = color;
        return imagen;
    }

    public static TextMeshProUGUI CrearTexto(string nombre, Transform padre, string texto, float tamano, Color color, TextAlignmentOptions alineacion = TextAlignmentOptions.MidlineLeft)
    {
        RectTransform rect = CrearRect(nombre, padre);
        TextMeshProUGUI tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.font = Fuente;
        tmp.text = texto;
        tmp.fontSize = tamano;
        tmp.color = color;
        tmp.alignment = alineacion;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.raycastTarget = false;
        return tmp;
    }

    /// <summary>Boton con texto centrado. Los colores de estado ya vienen puestos.</summary>
    public static Button CrearBoton(string nombre, Transform padre, string texto, float tamanoTexto, UnityAction alPulsar)
    {
        Image fondo = CrearImagen(nombre, padre, Color.white);
        fondo.sprite = SpritesProcedurales.Capsula;
        fondo.type = Image.Type.Sliced;
        fondo.pixelsPerUnitMultiplier = 2f;

        Button boton = fondo.gameObject.AddComponent<Button>();
        ColorBlock colores = boton.colors;
        colores.normalColor = ColorBoton;
        colores.highlightedColor = ColorBotonResaltado;
        colores.selectedColor = ColorBotonResaltado;
        colores.pressedColor = ColorTitulo * 0.7f;
        colores.disabledColor = new Color(0.12f, 0.11f, 0.13f, 0.6f);
        colores.fadeDuration = 0.08f;
        boton.colors = colores;

        if (alPulsar != null)
            boton.onClick.AddListener(alPulsar);

        TextMeshProUGUI etiqueta = CrearTexto("Texto", fondo.transform, texto, tamanoTexto, ColorTexto, TextAlignmentOptions.Center);
        Llenar(etiqueta.rectTransform, 4f);
        return boton;
    }

    /// <summary>Boton con la pincelada de EstiloMenu y texto claro, como los del menu principal.</summary>
    public static Button CrearBotonPincelada(string nombre, Transform padre, string texto, float tamanoTexto, UnityAction alPulsar)
    {
        EstiloMenu estilo = EstiloMenu.Actual;
        if (estilo.pincelada == null)
            return CrearBoton(nombre, padre, texto, tamanoTexto, alPulsar);

        Image fondo = CrearImagen(nombre, padre, Color.white);
        fondo.sprite = estilo.pincelada;

        Button boton = fondo.gameObject.AddComponent<Button>();
        ColorBlock colores = boton.colors;
        colores.normalColor = new Color(0.86f, 0.86f, 0.86f, 1f);
        colores.highlightedColor = Color.white;
        colores.selectedColor = Color.white;
        colores.pressedColor = new Color(0.6f, 0.6f, 0.6f, 1f);
        colores.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        colores.colorMultiplier = 1.15f;
        colores.fadeDuration = 0.08f;
        boton.colors = colores;

        if (alPulsar != null)
            boton.onClick.AddListener(alPulsar);

        TextMeshProUGUI etiqueta = CrearTexto("Texto", fondo.transform, texto, tamanoTexto, estilo.textoBoton, TextAlignmentOptions.Center);
        Llenar(etiqueta.rectTransform, 4f);
        return boton;
    }

    /// <summary>Boton que es solo texto (las flechas de las filas): el color del texto marca el estado.</summary>
    public static Button CrearBotonTexto(string nombre, Transform padre, string texto, float tamanoTexto, Color color, UnityAction alPulsar)
    {
        // Imagen invisible para recibir los clics en toda el area del boton
        Image area = CrearImagen(nombre, padre, new Color(0f, 0f, 0f, 0f));

        TextMeshProUGUI etiqueta = CrearTexto("Texto", area.transform, texto, tamanoTexto, Color.white, TextAlignmentOptions.Center);
        Llenar(etiqueta.rectTransform);

        Button boton = area.gameObject.AddComponent<Button>();
        boton.targetGraphic = etiqueta;
        ColorBlock colores = boton.colors;
        colores.normalColor = color * new Color(1f, 1f, 1f, 0.75f);
        colores.highlightedColor = color;
        colores.selectedColor = color;
        colores.pressedColor = color * 0.6f;
        colores.disabledColor = new Color(color.r, color.g, color.b, 0.18f);
        colores.fadeDuration = 0.08f;
        boton.colors = colores;

        if (alPulsar != null)
            boton.onClick.AddListener(alPulsar);

        return boton;
    }

    /// <summary>Tamano fijo para un hijo de un LayoutGroup.</summary>
    public static LayoutElement Medidas(Component componente, float ancho = -1f, float alto = -1f, float anchoFlexible = -1f)
    {
        LayoutElement elemento = componente.gameObject.GetComponent<LayoutElement>();
        if (elemento == null)
            elemento = componente.gameObject.AddComponent<LayoutElement>();

        elemento.preferredWidth = ancho;
        elemento.minWidth = ancho;
        elemento.preferredHeight = alto;
        elemento.minHeight = alto;
        elemento.flexibleWidth = anchoFlexible;
        return elemento;
    }

    /// <summary>Rect de la zona segura de la pantalla (sin muescas ni bordes redondeados), en anclas normalizadas.</summary>
    public static void AjustarAZonaSegura(RectTransform rect)
    {
        Rect segura = Screen.safeArea;
        float ancho = Mathf.Max(1, Screen.width);
        float alto = Mathf.Max(1, Screen.height);
        rect.anchorMin = new Vector2(segura.xMin / ancho, segura.yMin / alto);
        rect.anchorMax = new Vector2(segura.xMax / ancho, segura.yMax / alto);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
