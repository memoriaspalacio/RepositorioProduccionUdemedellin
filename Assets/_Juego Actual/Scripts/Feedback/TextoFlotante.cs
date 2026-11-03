using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Texto suelto de la interfaz que aparece, se mueve y se desvanece solo ("+3", "-5", "+VIDA").
/// Usa el acabado de caricatura de las calificaciones (EfectosCalificacion): negrita, degradado
/// vertical, contorno grueso y sombra dura. Dos caracteres de movimiento:
///   - Ligero (ganar): salta hacia arriba con rebote y suelta destellos.
///   - Pesado (perder): nace grande, cae con gravedad, se tuerce y tiembla.
/// Corre con tiempo sin escalar, asi que se ve igual con el juego en pausa.
/// </summary>
public class TextoFlotante : MonoBehaviour
{
    static readonly Color ColorContorno = new Color(0.13f, 0.05f, 0.1f, 1f);

    TMP_Text texto;
    RectTransform rect;
    Vector2 inicio;
    Vector2 recorrido;
    float duracion;
    float edad;
    bool pesado;
    float giro;
    Color arriba;
    Color abajo;
    float expansion;

    /// <summary>
    /// Crea el texto como hermano de 'ancla' (en su mismo Canvas), en su posicion mas 'desplazamiento'.
    /// 'recorrido' es hacia donde viaja (para pesado es la caida total). Con 'expansion' mayor que cero
    /// no salta ni suelta destellos: crece sin parar a ese ritmo (una onda).
    /// </summary>
    public static TextoFlotante Crear(RectTransform ancla, string contenido, Vector2 desplazamiento, Vector2 recorrido,
        Color arriba, Color abajo, float tamano, float duracion, bool pesado, TMP_FontAsset fuente = null, float expansion = 0f)
    {
        if (ancla == null || ancla.parent == null)
            return null;

        GameObject objeto = new GameObject(pesado ? "TextoFlotante (perder)" : "TextoFlotante (ganar)", typeof(RectTransform));
        objeto.layer = ancla.gameObject.layer;
        RectTransform rect = (RectTransform)objeto.transform;
        rect.SetParent(ancla.parent, false);
        rect.anchorMin = ancla.anchorMin;
        rect.anchorMax = ancla.anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(300f, 80f);
        rect.anchoredPosition = ancla.anchoredPosition + desplazamiento;

        TextMeshProUGUI tmp = objeto.AddComponent<TextMeshProUGUI>();
        TMP_Text referencia = ancla.GetComponent<TMP_Text>();
        tmp.font = fuente != null ? fuente : referencia != null ? referencia.font : TMP_Settings.defaultFontAsset;
        tmp.fontSize = tamano;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;
        tmp.text = contenido;
        EstiloCaricatura(tmp, 0.3f);

        TextoFlotante flotante = objeto.AddComponent<TextoFlotante>();
        flotante.texto = tmp;
        flotante.rect = rect;
        flotante.inicio = rect.anchoredPosition;
        flotante.recorrido = recorrido;
        flotante.duracion = Mathf.Max(0.1f, duracion);
        flotante.pesado = pesado;
        flotante.giro = pesado ? Random.Range(-14f, 14f) : 0f;
        flotante.arriba = arriba;
        flotante.abajo = abajo;
        flotante.expansion = expansion;
        flotante.Pintar(1f);

        if (!pesado && expansion <= 0f)
            Destellos(rect, arriba, 7);

        return flotante;
    }

    /// <summary>Negrita, degradado vertical, contorno grueso y sombra dura, como las calificaciones.</summary>
    public static void EstiloCaricatura(TMP_Text tmp, float contorno)
    {
        tmp.fontStyle |= FontStyles.Bold;
        tmp.enableVertexGradient = true;
        tmp.outlineWidth = contorno;
        tmp.outlineColor = ColorContorno;

        // fontMaterial crea una copia propia: la sombra no se contagia a otros textos con la misma fuente
        Material material = tmp.fontMaterial;
        if (material.HasProperty(ShaderUtilities.ID_UnderlayColor))
        {
            material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            material.SetColor(ShaderUtilities.ID_UnderlayColor, ColorContorno);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.35f);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.45f);
            material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0f);
        }
    }

    public static void Degradado(TMP_Text tmp, Color arriba, Color abajo)
    {
        tmp.colorGradient = new VertexGradient(arriba, arriba, abajo, abajo);
    }

    void Update()
    {
        edad += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(edad / duracion);

        if (expansion > 0f)
        {
            rect.localScale = Vector3.one * (1f + edad * expansion);
        }
        else if (pesado)
        {
            // Golpe: nace al 170 % y se encoge rapido; luego cae acelerando, torcido y temblando
            float golpe = 1f + 0.7f * Mathf.Exp(-edad * 14f);
            rect.localScale = Vector3.one * golpe;
            float caida = t * t;
            Vector2 temblor = Random.insideUnitCircle * 5f * (1f - t);
            rect.anchoredPosition = inicio + recorrido * caida + temblor;
            rect.localRotation = Quaternion.Euler(0f, 0f, giro * Mathf.SmoothStep(0f, 1f, t));
        }
        else
        {
            // Salto: sube rapido y frena, con un rebote de escala al nacer
            float subida = 1f - (1f - t) * (1f - t) * (1f - t);
            rect.anchoredPosition = inicio + recorrido * subida;
            float rebote = 1f + 0.45f * Mathf.Exp(-edad * 9f) * Mathf.Cos(edad * 26f);
            rect.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, Mathf.Clamp01(edad * 10f)) * rebote;
        }

        // Visible casi todo el tiempo y apagado al final
        Pintar(1f - Mathf.SmoothStep(0.55f, 1f, t));

        if (t >= 1f)
            Destroy(gameObject);
    }

    void Pintar(float alfa)
    {
        Color a = arriba;
        Color b = abajo;
        a.a *= alfa;
        b.a *= alfa;
        Degradado(texto, a, b);
        texto.alpha = alfa;
    }

    /// <summary>Estrellitas que salen disparadas del texto y se apagan.</summary>
    public static void Destellos(RectTransform origen, Color color, int cantidad)
    {
        for (int i = 0; i < cantidad; i++)
        {
            GameObject objeto = new GameObject("Destello", typeof(RectTransform));
            objeto.layer = origen.gameObject.layer;
            RectTransform rect = (RectTransform)objeto.transform;
            rect.SetParent(origen.parent, false);
            rect.anchorMin = origen.anchorMin;
            rect.anchorMax = origen.anchorMax;
            rect.anchoredPosition = origen.anchoredPosition;
            rect.sizeDelta = Vector2.one * Random.Range(14f, 24f);

            Image imagen = objeto.AddComponent<Image>();
            imagen.sprite = SpritesProcedurales.Estrella;
            imagen.color = color;
            imagen.raycastTarget = false;

            float angulo = (i / (float)cantidad + Random.Range(-0.06f, 0.06f)) * Mathf.PI * 2f;
            Destello destello = objeto.AddComponent<Destello>();
            destello.velocidad = new Vector2(Mathf.Cos(angulo), Mathf.Sin(angulo)) * Random.Range(110f, 190f);
        }
    }

    class Destello : MonoBehaviour
    {
        public Vector2 velocidad;
        float edad;
        Image imagen;
        RectTransform rect;

        void Awake()
        {
            imagen = GetComponent<Image>();
            rect = (RectTransform)transform;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            edad += dt;
            rect.anchoredPosition += velocidad * dt;
            velocidad *= Mathf.Exp(-dt * 5f);
            rect.localRotation = Quaternion.Euler(0f, 0f, edad * 220f);

            float vida = 1f - Mathf.Clamp01(edad / 0.55f);
            rect.localScale = Vector3.one * vida;
            Color color = imagen.color;
            color.a = vida;
            imagen.color = color;

            if (vida <= 0f)
                Destroy(gameObject);
        }
    }
}
