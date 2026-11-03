using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Transicion de las cinematicas: el corazon del HUD late sobre un fondo del bosque con los
/// monstruos, crece hasta llenar la pantalla y funde a blanco (antes de una cinematica) o a
/// negro (despues). Con la pantalla cubierta hace lo que se le pida (empezar un video, cargar
/// una escena) y luego se aclara.
///
/// Se crea sola la primera vez que se usa (no hace falta ponerla en la escena), sobrevive a los
/// cambios de escena y corre en tiempo sin escalar. Las imagenes estan en Resources/Transiciones.
/// </summary>
[DisallowMultipleComponent]
public class TransicionCorazon : MonoBehaviour
{
    public enum Fundido { Blanco, Negro }

    // Toda la animacion dura esto; las fases van en fracciones de esta duracion
    private const float Duracion = 3.6f;
    private const float FinCubrir = 0.86f;   // aqui la pantalla ya es blanca o negra
    private const float SegundosDescubrir = 0.6f;

    private const float LadoCorazon = 653f;  // 34% del ancho de 1920
    private const float LadoHalo = 1152f;    // 60% del ancho de 1920

    private static TransicionCorazon instancia;

    private Canvas canvas;
    private Image fondoPantalla;
    private RawImage pantalla, fondo, halo, corazon;
    private AspectRatioFitter ajustePantalla;
    private Image velo;
    private Texture2D texClaro, texOscuro;

    private Coroutine rutina;
    private float giroHalo;
    private bool animandoHalo;

    /// <summary>Hay una transicion o un fundido en marcha.</summary>
    public static bool EnCurso => instancia != null && instancia.rutina != null;

    public static TransicionCorazon Instancia
    {
        get
        {
            if (instancia == null)
                instancia = Crear();
            return instancia;
        }
    }

    static TransicionCorazon Crear()
    {
        GameObject objeto = new GameObject("TransicionCorazon", typeof(RectTransform));
        DontDestroyOnLoad(objeto);

        TransicionCorazon t = objeto.AddComponent<TransicionCorazon>();
        t.canvas = objeto.AddComponent<Canvas>();
        t.canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        t.canvas.sortingOrder = short.MaxValue - 1;

        CanvasScaler escalador = objeto.AddComponent<CanvasScaler>();
        escalador.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escalador.referenceResolution = new Vector2(1920f, 1080f);
        escalador.matchWidthOrHeight = 0.5f;
        objeto.AddComponent<GraphicRaycaster>(); // el velo bloquea los clics mientras cubre

        t.texClaro = Resources.Load<Texture2D>("Transiciones/Fondo_Transicion_Claro");
        t.texOscuro = Resources.Load<Texture2D>("Transiciones/Fondo_Transicion_Oscuro");

        // De abajo arriba: video, fondo del bosque, halo, corazon y velo
        t.fondoPantalla = CrearCapa<Image>(objeto.transform, "FondoPantalla", true);
        t.fondoPantalla.color = Color.black;
        t.pantalla = CrearCapa<RawImage>(t.fondoPantalla.transform, "Pantalla", true);
        t.ajustePantalla = t.pantalla.gameObject.AddComponent<AspectRatioFitter>();
        t.ajustePantalla.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        t.fondoPantalla.gameObject.SetActive(false);

        t.fondo = CrearCapa<RawImage>(objeto.transform, "Fondo", true);
        AspectRatioFitter ajusteFondo = t.fondo.gameObject.AddComponent<AspectRatioFitter>();
        ajusteFondo.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        ajusteFondo.aspectRatio = 16f / 9f;

        t.halo = CrearCapa<RawImage>(objeto.transform, "Halo", false);
        t.halo.texture = Resources.Load<Texture2D>("Transiciones/Corazon_Halo");
        t.halo.rectTransform.sizeDelta = new Vector2(LadoHalo, LadoHalo);

        t.corazon = CrearCapa<RawImage>(objeto.transform, "Corazon", false);
        t.corazon.texture = Resources.Load<Texture2D>("Transiciones/Corazon_Transicion");
        t.corazon.rectTransform.sizeDelta = new Vector2(LadoCorazon, LadoCorazon);

        t.velo = CrearCapa<Image>(objeto.transform, "Velo", true);

        t.Pintar(0f, 0f);
        t.canvas.enabled = false;
        return t;
    }

    static T CrearCapa<T>(Transform padre, string nombre, bool estirar) where T : Graphic
    {
        GameObject capa = new GameObject(nombre, typeof(RectTransform));
        RectTransform rect = (RectTransform)capa.transform;
        rect.SetParent(padre, false);
        if (estirar)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
        T grafico = capa.AddComponent<T>();
        grafico.raycastTarget = false;
        return grafico;
    }

    void OnDestroy()
    {
        if (instancia == this)
            instancia = null;
    }

    void Update()
    {
        if (animandoHalo)
        {
            giroHalo += Time.unscaledDeltaTime * 8f;
            halo.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -giroHalo);
        }
    }

    /// <summary>
    /// Corazon que cubre la pantalla hasta el blanco o el negro; con la pantalla cubierta espera a
    /// "enMedio" (puede ser null) y despues se aclara. La corrutina termina con la pantalla limpia.
    /// </summary>
    public Coroutine Reproducir(Fundido tipo, Func<IEnumerator> enMedio)
    {
        if (rutina != null)
            StopCoroutine(rutina);
        rutina = StartCoroutine(Secuencia(tipo, enMedio));
        return rutina;
    }

    /// <summary>Deja la pantalla en blanco o negro sin animacion (por ejemplo, al abrir el juego).</summary>
    public void CubrirAlInstante(Fundido tipo)
    {
        if (rutina != null)
            StopCoroutine(rutina);
        rutina = null;

        canvas.enabled = true;
        velo.color = ColorVelo(tipo, 1f);
        velo.raycastTarget = true;
        Pintar(FinCubrir, 1f);
    }

    /// <summary>Fundido simple desde lo que cubre la pantalla hasta dejarla limpia.</summary>
    public Coroutine Descubrir(float segundos)
    {
        if (rutina != null)
            StopCoroutine(rutina);
        rutina = StartCoroutine(Aclarar(segundos));
        return rutina;
    }

    /// <summary>Muestra un video (RenderTexture) debajo del corazon; null lo quita.</summary>
    public void MostrarPantalla(Texture textura, float aspecto)
    {
        canvas.enabled = true;
        pantalla.texture = textura;
        ajustePantalla.aspectRatio = aspecto;
        fondoPantalla.gameObject.SetActive(textura != null);
    }

    IEnumerator Secuencia(Fundido tipo, Func<IEnumerator> enMedio)
    {
        canvas.enabled = true;
        velo.raycastTarget = true;
        fondo.texture = tipo == Fundido.Blanco ? texClaro : texOscuro;
        corazonOscurece = tipo == Fundido.Negro;

        // Si ya habia algo cubriendo (un fundido a medias) el velo parte de ahi en vez de saltar
        float veloInicial = velo.color.a;
        Color colorVelo = ColorVelo(tipo, 0f);

        for (float t = 0f; t < Duracion * FinCubrir; t += Time.unscaledDeltaTime)
        {
            float k = t / Duracion;
            velo.color = colorVelo;
            Pintar(k, Mathf.Max(CurvaVelo(k), veloInicial * (1f - Tramo(k, 0f, 0.2f))));
            yield return null;
        }
        Pintar(FinCubrir, 1f);

        if (enMedio != null)
            yield return enMedio();

        yield return Aclarar(SegundosDescubrir);
    }

    IEnumerator Aclarar(float segundos)
    {
        // Lo que queda es solo el velo (o el video debajo de el)
        Pintar(FinCubrir, velo.color.a);
        velo.raycastTarget = false;
        float inicio = velo.color.a;

        for (float t = 0f; t < segundos; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.Clamp01(t / segundos);
            SetAlfa(velo, inicio * (1f - Suave(k)));
            yield return null;
        }

        SetAlfa(velo, 0f);
        rutina = null;
        canvas.enabled = fondoPantalla.gameObject.activeSelf;
    }

    private bool corazonOscurece;

    /// <summary>Coloca cada capa en el momento k (0..1 de Duracion), como en la propuesta.</summary>
    void Pintar(float k, float alfaVelo)
    {
        bool cubierto = k >= FinCubrir;

        // Fondo: aparece, se acerca un poco y desaparece cuando ya todo es blanco o negro
        fondo.gameObject.SetActive(!cubierto && k > 0f);
        SetAlfa(fondo, 0.92f * Suave(Tramo(k, 0f, 0.26f)));
        float escalaFondo = Mathf.Lerp(1.12f, 1f, Suave(Tramo(k, 0f, FinCubrir)));
        fondo.rectTransform.localScale = new Vector3(escalaFondo, escalaFondo, 1f);

        // Corazon: aparece, late dos veces y crece hasta llenar la pantalla
        corazon.gameObject.SetActive(!cubierto && k > 0.12f);
        float crecer = Suave(Tramo(k, 0.5f, 0.84f));
        float escala = k < 0.2f ? Mathf.Lerp(0.2f, 1f, Suave(Tramo(k, 0.12f, 0.2f)))
                     : k < 0.5f ? Latido(k)
                     : Mathf.Lerp(1f, 9f, crecer);
        corazon.rectTransform.localScale = new Vector3(escala, escala, 1f);
        float luz = corazonOscurece ? 1f - crecer : 1f;
        corazon.color = new Color(luz, luz, luz, Suave(Tramo(k, 0.12f, 0.2f)));

        // Halo: entra con el corazon, gira y crece con el
        halo.gameObject.SetActive(!cubierto && k > 0.1f);
        animandoHalo = halo.gameObject.activeSelf;
        float escalaHalo = k < 0.22f ? Mathf.Lerp(0.4f, 1f, Suave(Tramo(k, 0.1f, 0.22f)))
                         : k < 0.5f ? Mathf.Lerp(1f, 1.05f, Tramo(k, 0.22f, 0.5f))
                         : Mathf.Lerp(1.05f, 5f, crecer);
        halo.rectTransform.localScale = new Vector3(escalaHalo, escalaHalo, 1f);
        SetAlfa(halo, Suave(Tramo(k, 0.1f, 0.22f)));

        SetAlfa(velo, alfaVelo);
    }

    // Dos latidos entre 0.2 y 0.5
    static float Latido(float k)
    {
        if (k < 0.28f) return Mathf.Lerp(1f, 1.14f, Suave(Tramo(k, 0.2f, 0.28f)));
        if (k < 0.34f) return Mathf.Lerp(1.14f, 0.96f, Suave(Tramo(k, 0.28f, 0.34f)));
        if (k < 0.42f) return Mathf.Lerp(0.96f, 1.12f, Suave(Tramo(k, 0.34f, 0.42f)));
        return Mathf.Lerp(1.12f, 1f, Suave(Tramo(k, 0.42f, 0.5f)));
    }

    // El velo entra acelerando entre 0.58 y 0.84
    static float CurvaVelo(float k)
    {
        float v = Tramo(k, 0.58f, 0.84f);
        return v * v;
    }

    static Color ColorVelo(Fundido tipo, float alfa)
    {
        return tipo == Fundido.Blanco ? new Color(1f, 1f, 1f, alfa) : new Color(0f, 0f, 0f, alfa);
    }

    static float Tramo(float k, float desde, float hasta)
    {
        return Mathf.Clamp01((k - desde) / (hasta - desde));
    }

    static float Suave(float k)
    {
        return k * k * (3f - 2f * k);
    }

    static void SetAlfa(Graphic grafico, float alfa)
    {
        Color c = grafico.color;
        c.a = alfa;
        grafico.color = c;
    }
}
