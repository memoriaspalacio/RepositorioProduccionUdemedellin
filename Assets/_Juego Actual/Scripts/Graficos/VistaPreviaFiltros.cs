using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Vista previa del menu de filtros: una captura del juego sin interfaz con el filtro elegido
/// encima, y debajo los colores clave del juego (noche, lampara, piel, oro, fuego) tal como los
/// deja el filtro.
///
/// Dentro de un nivel la captura es de la camara en ese momento, sin el filtro actual. Fuera de
/// los niveles (menu principal) se alternan las imagenes de Resources/VistasFiltros: la
/// ilustracion "La vigilia" y las capturas de los niveles que genera Herramientas > Graficos >
/// Capturar vistas previas de filtros.
///
/// El filtro se aplica en CPU con GestorFiltros.Transformar, una aproximacion de lo que hace el
/// post proceso: se parece mucho pero no es identico (el grano, por ejemplo, es mas sencillo).
/// </summary>
public class VistaPreviaFiltros : MonoBehaviour
{
    const int Ancho = 448;
    const int Alto = 252;
    const string CarpetaCapturas = "VistasFiltros";
    const float SegundosPorCaptura = 4f;
    const float AltoMuestras = 54f;

    static readonly (string nombre, Color color)[] Muestras =
    {
        ("Noche", new Color(0.12f, 0.16f, 0.32f)),
        ("Lámpara", new Color(0.86f, 0.22f, 0.66f)),
        ("Piel", new Color(0.86f, 0.66f, 0.52f)),
        ("Oro", new Color(0.95f, 0.76f, 0.36f)),
        ("Fuego", new Color(1f, 0.42f, 0.12f))
    };

    RawImage imagen;
    TextMeshProUGUI pie;
    TextMeshProUGUI aviso;
    Image[] muestras;

    Texture2D salida;
    Color[] fuente;
    Color[] pixeles;
    float[] ruido;

    Texture2D[] guardadas;
    int indiceGuardada;
    float siguienteCambio;
    bool enVivo;

    bool dibujado;
    FiltroColor ultimoFiltro;
    IntensidadFiltro ultimaIntensidad;

    public static VistaPreviaFiltros Crear(RectTransform bloque)
    {
        VistaPreviaFiltros vista = bloque.gameObject.AddComponent<VistaPreviaFiltros>();
        vista.Construir(bloque);
        vista.Capturar();
        vista.Refrescar();
        return vista;
    }

    #region Construccion

    void Construir(RectTransform bloque)
    {
        EstiloMenu estilo = EstiloMenu.Actual;

        // Marco fino del color de los titulos, con la proporcion de la captura
        RectTransform zona = ConstructorUI.CrearRect("Zona imagen", bloque);
        zona.anchorMin = new Vector2(0f, 0f);
        zona.anchorMax = new Vector2(1f, 1f);
        zona.offsetMin = new Vector2(0f, AltoMuestras + 6f);
        zona.offsetMax = Vector2.zero;

        Image marco = ConstructorUI.CrearImagen("Marco", zona, estilo.titulo);
        RectTransform rectMarco = marco.rectTransform;
        rectMarco.anchorMin = new Vector2(0.5f, 0f);
        rectMarco.anchorMax = new Vector2(0.5f, 1f);
        rectMarco.pivot = new Vector2(0.5f, 0.5f);
        AspectRatioFitter proporcion = marco.gameObject.AddComponent<AspectRatioFitter>();
        proporcion.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
        proporcion.aspectRatio = Ancho / (float)Alto;

        GameObject objetoImagen = new GameObject("Captura", typeof(RectTransform), typeof(RawImage));
        objetoImagen.transform.SetParent(marco.transform, false);
        imagen = objetoImagen.GetComponent<RawImage>();
        imagen.color = Color.white;
        imagen.raycastTarget = false;
        ConstructorUI.Llenar(imagen.rectTransform, 3f);

        aviso = ConstructorUI.CrearTexto("Aviso", imagen.transform, string.Empty, 22f, Color.white, TextAlignmentOptions.Center);
        aviso.textWrappingMode = TextWrappingModes.Normal;
        ConstructorUI.Llenar(aviso.rectTransform, 24f);

        pie = ConstructorUI.CrearTexto("Pie", imagen.transform, string.Empty, 18f, new Color(1f, 1f, 1f, 0.85f), TextAlignmentOptions.BottomLeft);
        pie.fontStyle = FontStyles.Italic;
        pie.margin = new Vector4(10f, 0f, 0f, 6f);
        ConstructorUI.Llenar(pie.rectTransform);
        Shadow sombra = pie.gameObject.AddComponent<Shadow>();
        sombra.effectColor = new Color(0f, 0f, 0f, 0.8f);

        // Muestras de color bajo la imagen
        RectTransform fila = ConstructorUI.CrearRect("Muestras", bloque);
        fila.anchorMin = new Vector2(0.5f, 0f);
        fila.anchorMax = new Vector2(0.5f, 0f);
        fila.pivot = new Vector2(0.5f, 0f);
        fila.sizeDelta = new Vector2(Muestras.Length * 92f, AltoMuestras);
        fila.anchoredPosition = Vector2.zero;

        HorizontalLayoutGroup linea = fila.gameObject.AddComponent<HorizontalLayoutGroup>();
        linea.spacing = 12f;
        linea.childControlWidth = true;
        linea.childControlHeight = true;
        linea.childForceExpandWidth = true;
        linea.childForceExpandHeight = true;

        muestras = new Image[Muestras.Length];
        for (int i = 0; i < Muestras.Length; i++)
        {
            RectTransform celda = ConstructorUI.CrearRect("Muestra " + Muestras[i].nombre, fila);

            muestras[i] = ConstructorUI.CrearImagen("Color", celda, Muestras[i].color);
            muestras[i].sprite = SpritesProcedurales.Capsula;
            muestras[i].type = Image.Type.Sliced;
            RectTransform rectColor = muestras[i].rectTransform;
            rectColor.anchorMin = new Vector2(0f, 1f);
            rectColor.anchorMax = new Vector2(1f, 1f);
            rectColor.pivot = new Vector2(0.5f, 1f);
            rectColor.sizeDelta = new Vector2(0f, 24f);
            rectColor.anchoredPosition = Vector2.zero;

            TextMeshProUGUI nombre = ConstructorUI.CrearTexto("Nombre", celda, Muestras[i].nombre, 18f, estilo.tenue, TextAlignmentOptions.Bottom);
            ConstructorUI.Llenar(nombre.rectTransform);
        }
    }

    #endregion

    #region Capturas

    /// <summary>En un nivel, la camara ahora mismo; si no, las capturas guardadas.</summary>
    void Capturar()
    {
        Camera camara = Camera.main;
        bool hayNivel = camara != null && FindAnyObjectByType<AtmosferaEscena>() != null;

        if (hayNivel)
        {
            RenderTexture grande = RenderTexture.GetTemporary(Ancho * 2, Alto * 2, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            if (GestorFiltros.CapturarSinFiltro(camara, grande))
            {
                fuente = Leer(grande);
                enVivo = true;
                pie.text = "Vista: esta escena";
            }

            RenderTexture.ReleaseTemporary(grande);
            if (fuente != null)
                return;
        }

        guardadas = Resources.LoadAll<Texture2D>(CarpetaCapturas);
        // La ilustracion ("La vigilia", con mayuscula) va antes que las capturas de los niveles
        System.Array.Sort(guardadas, (a, b) => string.CompareOrdinal(a.name, b.name));
        if (guardadas.Length == 0)
        {
            aviso.text = "Abre este menú dentro de un nivel para ver cómo queda el filtro.";
            imagen.color = new Color(0.08f, 0.06f, 0.08f);
            return;
        }

        indiceGuardada = 0;
        UsarGuardada();
    }

    void UsarGuardada()
    {
        Texture2D captura = guardadas[indiceGuardada];
        fuente = Leer(captura);
        pie.text = "Vista: " + CultureInfo.CurrentCulture.TextInfo.ToTitleCase(captura.name.ToLowerInvariant());
        siguienteCambio = Time.unscaledTime + SegundosPorCaptura;
        dibujado = false;
    }

    /// <summary>Copia cualquier textura (aunque no sea legible) a colores de Ancho x Alto en espacio gamma.</summary>
    static Color[] Leer(Texture origen)
    {
        RenderTexture chica = RenderTexture.GetTemporary(Ancho, Alto, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(origen, chica);

        RenderTexture anterior = RenderTexture.active;
        RenderTexture.active = chica;
        Texture2D copia = new Texture2D(Ancho, Alto, TextureFormat.RGBA32, false, false);
        copia.ReadPixels(new Rect(0, 0, Ancho, Alto), 0, 0);
        copia.Apply();
        RenderTexture.active = anterior;
        RenderTexture.ReleaseTemporary(chica);

        Color[] colores = copia.GetPixels();
        Destroy(copia);
        return colores;
    }

    #endregion

    #region Dibujo

    void Update()
    {
        if (enVivo || guardadas == null || guardadas.Length < 2 || Time.unscaledTime < siguienteCambio)
            return;

        indiceGuardada = (indiceGuardada + 1) % guardadas.Length;
        UsarGuardada();
        Refrescar();
    }

    /// <summary>Vuelve a aplicar el filtro si cambio (o si cambio la captura).</summary>
    public void Refrescar()
    {
        FiltroColor filtro = GestorFiltros.Filtro;
        IntensidadFiltro intensidad = GestorFiltros.Intensidad;
        float peso = GestorFiltros.Disponible && filtro != FiltroColor.Ninguno ? GestorFiltros.PesoDe(intensidad) : 0f;
        GestorFiltros.Parametros parametros = GestorFiltros.ParametrosDe(filtro);

        for (int i = 0; i < muestras.Length; i++)
            muestras[i].color = GestorFiltros.Transformar(Muestras[i].color, parametros, peso, new Vector2(0.5f, 0.5f), 0.5f);

        if (fuente == null || (dibujado && filtro == ultimoFiltro && intensidad == ultimaIntensidad))
            return;

        if (salida == null)
        {
            salida = new Texture2D(Ancho, Alto, TextureFormat.RGBA32, false, false);
            salida.wrapMode = TextureWrapMode.Clamp;
            pixeles = new Color[fuente.Length];
            ruido = new float[fuente.Length];
            System.Random azar = new System.Random(112);
            for (int i = 0; i < ruido.Length; i++)
                ruido[i] = (float)azar.NextDouble();
        }

        for (int y = 0; y < Alto; y++)
        {
            float v = (y + 0.5f) / Alto;
            for (int x = 0; x < Ancho; x++)
            {
                int i = y * Ancho + x;
                pixeles[i] = GestorFiltros.Transformar(fuente[i], parametros, peso, new Vector2((x + 0.5f) / Ancho, v), ruido[i]);
            }
        }

        salida.SetPixels(pixeles);
        salida.Apply(false);
        imagen.texture = salida;
        imagen.color = Color.white;
        aviso.text = string.Empty;

        dibujado = true;
        ultimoFiltro = filtro;
        ultimaIntensidad = intensidad;
    }

    void OnDestroy()
    {
        if (salida != null)
            Destroy(salida);
    }

    #endregion
}
