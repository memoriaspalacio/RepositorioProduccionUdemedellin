using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Efectos de caricatura del metrónomo según qué tan bien cayó cada golpe:
///   - PERFECT: el texto salta con rebote, una onda de choque y una lluvia de estrellas.
///   - GOOD: el texto salta más tranquilo, con una onda pequeña y unas chispas.
///   - BAD: el texto se desinfla y se cae de lado, sale humo gris y una gota de sudor.
///   - MISS: el texto tiembla en rojo (nota que se dejó pasar o pulsar sin nota).
///
/// El latido del corazón lo hace HeartBeatFeedback; esto solo pone el texto y las
/// partículas encima. Todo se crea por código al arrancar (ver SpritesProcedurales), sin
/// assets extra, y se reutiliza: no genera basura durante la partida.
///
/// Va en el mismo objeto que el corazón. Si no está, HeartBeatFeedback lo añade solo.
/// </summary>
[DisallowMultipleComponent]
public class EfectosCalificacion : MonoBehaviour
{
    [Header("--- Referencias ---")]
    [Tooltip("Juez de ritmo de la escena. Si es nulo se busca al arrancar.")]
    [SerializeField] private BeatActionJudge beatJudge;

    [Tooltip("Tipografía de los textos. Vacía: la del juego (Rijusans) si ya está cargada.")]
    [SerializeField] private TMP_FontAsset fuente;

    [Header("--- Texto ---")]
    [Tooltip("Tamaño del texto de la calificación.")]
    [Range(10f, 80f)]
    [SerializeField] private float tamanoTexto = 30f;

    [Tooltip("A qué altura sobre el centro del corazón aparece el texto, en unidades de UI.")]
    [Range(0f, 200f)]
    [SerializeField] private float alturaTexto = 75f;

    [Tooltip("Segundos que dura el texto en pantalla.")]
    [Range(0.2f, 2f)]
    [SerializeField] private float duracionTexto = 0.7f;

    [Tooltip("Mostrar MISS al dejar pasar una nota o pulsar sin nota.")]
    [SerializeField] private bool mostrarMiss = true;

    [Header("--- Colores (arriba / abajo del texto) ---")]
    [SerializeField] private Color perfectArriba = new Color(1f, 0.96f, 0.5f, 1f);
    [SerializeField] private Color perfectAbajo = new Color(1f, 0.5f, 0.12f, 1f);
    [SerializeField] private Color goodArriba = new Color(0.6f, 1f, 0.75f, 1f);
    [SerializeField] private Color goodAbajo = new Color(0.12f, 0.72f, 0.6f, 1f);
    [SerializeField] private Color badArriba = new Color(0.78f, 0.76f, 0.88f, 1f);
    [SerializeField] private Color badAbajo = new Color(0.42f, 0.4f, 0.58f, 1f);
    [SerializeField] private Color missArriba = new Color(1f, 0.5f, 0.5f, 1f);
    [SerializeField] private Color missAbajo = new Color(0.78f, 0.08f, 0.16f, 1f);

    [Tooltip("Contorno de los textos y de las partículas.")]
    [SerializeField] private Color colorContorno = new Color(0.13f, 0.05f, 0.1f, 1f);

    [Header("--- Partículas ---")]
    [Tooltip("Estrellas que salen disparadas en un PERFECT.")]
    [Range(0, 24)]
    [SerializeField] private int estrellasPerfect = 10;

    [Tooltip("Chispas que salen en un GOOD.")]
    [Range(0, 16)]
    [SerializeField] private int chispasGood = 6;

    [Tooltip("Hasta dónde llegan las estrellas, en unidades de UI.")]
    [Range(20f, 300f)]
    [SerializeField] private float alcanceParticulas = 95f;

    private static readonly Color[] ColoresEstrella =
    {
        new Color(1f, 0.88f, 0.3f), new Color(1f, 0.55f, 0.75f), new Color(0.55f, 0.92f, 1f), Color.white
    };

    private const int MaxTextos = 3;
    private const int MaxParticulas = 48;

    private class Texto
    {
        public RectTransform rect;
        public TextMeshProUGUI tmp;
        public JudgementResult tipo;
        public float edad = 1f; // 0..1 a lo largo de duracionTexto; 1 = libre
        public float giro;
    }

    private class Particula
    {
        public RectTransform rect;
        public Image imagen;
        public Vector2 velocidad;
        public float gravedad, freno, giro, vida, edad = -1f, escalaInicio, escalaFin;
        public Color color;
        public bool vivaPocoAPoco; // crece al nacer en vez de aparecer de golpe
    }

    private RectTransform corazon;
    private RectTransform contenedor;
    private readonly List<Texto> textos = new List<Texto>();
    private readonly List<Particula> particulas = new List<Particula>();
    private int siguienteTexto;

    private void Awake()
    {
        corazon = (RectTransform)transform;

        if (beatJudge == null)
            beatJudge = FindFirstObjectByType<BeatActionJudge>();
    }

    private void OnEnable()
    {
        if (beatJudge != null)
            beatJudge.OnBeatGraded += HandleBeatGraded;
    }

    private void OnDisable()
    {
        if (beatJudge != null)
            beatJudge.OnBeatGraded -= HandleBeatGraded;
    }

    private void OnDestroy()
    {
        // Vive como hermano del corazón, no como hijo: hay que borrarlo a mano
        if (contenedor != null)
            Destroy(contenedor.gameObject);
    }

    private void Start()
    {
        if (fuente == null)
            fuente = ConstructorUI.Fuente;

        // Hermano del corazón y no hijo, para que su latido no escale los textos
        GameObject objeto = new GameObject("EfectosCalificacion", typeof(RectTransform));
        objeto.layer = gameObject.layer;
        contenedor = (RectTransform)objeto.transform;
        contenedor.SetParent(corazon.parent, false);
        contenedor.anchorMin = corazon.anchorMin;
        contenedor.anchorMax = corazon.anchorMax;
        contenedor.pivot = new Vector2(0.5f, 0.5f);
        contenedor.sizeDelta = Vector2.zero;
        contenedor.anchoredPosition = corazon.anchoredPosition;

        for (int i = 0; i < MaxTextos; i++)
            textos.Add(CrearTexto(i));
    }

    private void LateUpdate()
    {
        if (contenedor == null)
            return;

        float dt = Time.unscaledDeltaTime;
        bool activo = false;

        foreach (Texto t in textos)
            activo |= AnimarTexto(t, dt);

        foreach (Particula p in particulas)
            activo |= AnimarParticula(p, dt);

        // Las notas se crean bajo el mismo padre durante la partida: hay que seguir encima
        if (activo && contenedor.GetSiblingIndex() != contenedor.parent.childCount - 1)
            contenedor.SetAsLastSibling();
    }

    #region Eventos del juez

    private void HandleBeatGraded(float beat, JudgementResult resultado)
    {
        if (contenedor == null)
            return;

        if (resultado == JudgementResult.Miss && !mostrarMiss)
            return;

        switch (resultado)
        {
            case JudgementResult.Perfect: EfectoPerfect(); break;
            case JudgementResult.Good: EfectoGood(); break;
            case JudgementResult.Bad: EfectoBad(); break;
        }

        // Después de las partículas, para quedar encima de ellas
        MostrarTexto(resultado);
    }

    #endregion

    #region Texto

    private Texto CrearTexto(int indice)
    {
        GameObject objeto = new GameObject("Calificacion" + indice, typeof(RectTransform));
        objeto.layer = gameObject.layer;

        Texto t = new Texto();
        t.rect = (RectTransform)objeto.transform;
        t.rect.SetParent(contenedor, false);
        t.rect.sizeDelta = new Vector2(400f, 100f);

        t.tmp = objeto.AddComponent<TextMeshProUGUI>();
        if (fuente != null)
            t.tmp.font = fuente;
        t.tmp.fontSize = tamanoTexto;
        t.tmp.fontStyle = FontStyles.Bold;
        t.tmp.alignment = TextAlignmentOptions.Center;
        t.tmp.overflowMode = TextOverflowModes.Overflow;
        t.tmp.raycastTarget = false;
        t.tmp.enableVertexGradient = true;

        // Contorno grueso y sombra dura: el acabado de caricatura
        t.tmp.outlineWidth = 0.28f;
        t.tmp.outlineColor = colorContorno;
        Material material = t.tmp.fontMaterial;
        if (material.HasProperty(ShaderUtilities.ID_UnderlayColor))
        {
            material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            material.SetColor(ShaderUtilities.ID_UnderlayColor, colorContorno);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.35f);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.45f);
            material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0f);
        }

        objeto.SetActive(false);
        return t;
    }

    private void MostrarTexto(JudgementResult tipo)
    {
        // El anterior se aparta rápido para no amontonarse en ráfagas de notas
        foreach (Texto viejo in textos)
        {
            if (viejo.edad < 0.72f)
                viejo.edad = 0.72f;
        }

        Texto t = textos[siguienteTexto];
        siguienteTexto = (siguienteTexto + 1) % textos.Count;

        t.tipo = tipo;
        t.edad = 0f;
        t.giro = Random.Range(-1f, 1f);

        Color arriba, abajo;
        switch (tipo)
        {
            case JudgementResult.Perfect: t.tmp.text = "¡PERFECTO!"; arriba = perfectArriba; abajo = perfectAbajo; break;
            case JudgementResult.Good: t.tmp.text = "¡BIEN!"; arriba = goodArriba; abajo = goodAbajo; break;
            case JudgementResult.Bad: t.tmp.text = "REGULAR"; arriba = badArriba; abajo = badAbajo; break;
            default: t.tmp.text = "FALLO"; arriba = missArriba; abajo = missAbajo; break;
        }

        t.tmp.colorGradient = new VertexGradient(arriba, arriba, abajo, abajo);
        t.tmp.fontSize = tipo == JudgementResult.Perfect ? tamanoTexto * 1.15f : tamanoTexto;
        t.rect.SetAsLastSibling();
        t.rect.gameObject.SetActive(true);
        AnimarTexto(t, 0f);
    }

    /// <summary>Mueve un texto según su tipo. Devuelve false si ya no se ve.</summary>
    private bool AnimarTexto(Texto t, float dt)
    {
        if (t.edad >= 1f)
            return false;

        t.edad = Mathf.Min(1f, t.edad + dt / duracionTexto);
        float e = t.edad;

        if (e >= 1f)
        {
            t.rect.gameObject.SetActive(false);
            return false;
        }

        Vector2 posicion = new Vector2(0f, alturaTexto);
        Vector3 escala = Vector3.one;
        float angulo = 0f;

        switch (t.tipo)
        {
            case JudgementResult.Perfect:
            {
                // Salta desde cero con un rebote grande, sube y se queda vibrando un poco
                float salto = RebotarAfuera(Mathf.Clamp01(e / 0.28f), 2.6f);
                float latido = 1f + 0.06f * Mathf.Sin(e * Mathf.PI * 8f) * (1f - e);
                escala = Vector3.one * (salto * latido);
                posicion.y += 26f * SalirSuave(e);
                angulo = t.giro * 9f * (1f - SalirSuave(Mathf.Clamp01(e / 0.4f))) + t.giro * 3f;
                break;
            }
            case JudgementResult.Good:
            {
                float salto = RebotarAfuera(Mathf.Clamp01(e / 0.25f), 1.6f);
                escala = Vector3.one * salto;
                posicion.y += 18f * SalirSuave(e);
                angulo = t.giro * 4f;
                break;
            }
            case JudgementResult.Bad:
            {
                // Aparece aplastado, se desinfla y se cae de lado
                float aparecer = SalirSuave(Mathf.Clamp01(e / 0.15f));
                float desinflar = Mathf.Clamp01((e - 0.2f) / 0.8f);
                escala = new Vector3(aparecer * (1f + 0.08f * desinflar), aparecer * (1f - 0.22f * desinflar), 1f);
                posicion.y -= 30f * desinflar * desinflar;
                posicion.x += 6f * Mathf.Sign(t.giro) * desinflar;
                angulo = -16f * Mathf.Sign(t.giro) * desinflar * desinflar;
                break;
            }
            default:
            {
                // Tiembla de lado a lado y se calma
                float calma = 1f - e;
                posicion.x += Mathf.Sin(e * 70f) * 7f * calma * calma;
                escala = Vector3.one * (1f + 0.15f * calma * calma);
                break;
            }
        }

        t.rect.anchoredPosition = posicion;
        t.rect.localScale = escala;
        t.rect.localRotation = Quaternion.Euler(0f, 0f, angulo);
        t.tmp.alpha = e < 0.7f ? 1f : 1f - (e - 0.7f) / 0.3f;
        return true;
    }

    #endregion

    #region Partículas

    private void EfectoPerfect()
    {
        Onda(new Color(1f, 0.85f, 0.3f), 0.9f, 2.1f, 0.32f);
        Onda(Color.white, 0.7f, 1.5f, 0.22f);

        float desfase = Random.Range(0f, 360f);
        for (int i = 0; i < estrellasPerfect; i++)
        {
            float angulo = (desfase + i * 360f / Mathf.Max(1, estrellasPerfect) + Random.Range(-12f, 12f)) * Mathf.Deg2Rad;
            Vector2 direccion = new Vector2(Mathf.Cos(angulo), Mathf.Sin(angulo));

            Particula p = Lanzar(SpritesProcedurales.Estrella, Random.Range(20f, 30f), direccion * 30f,
                                 ColoresEstrella[i % ColoresEstrella.Length], Random.Range(0.55f, 0.75f));
            p.velocidad = direccion * alcanceParticulas * Random.Range(4.2f, 5.6f);
            p.freno = 5.5f;
            p.gravedad = -260f;
            p.giro = Random.Range(-540f, 540f);
            p.escalaInicio = 1.2f;
            p.escalaFin = 0.2f;
            p.vivaPocoAPoco = true;
        }

        // Chispitas blancas entre las estrellas
        for (int i = 0; i < 6; i++)
        {
            Vector2 direccion = Random.insideUnitCircle.normalized;
            Particula p = Lanzar(SpritesProcedurales.Circulo, Random.Range(7f, 11f), direccion * 20f, Color.white, 0.4f);
            p.velocidad = direccion * alcanceParticulas * Random.Range(3f, 4.5f);
            p.freno = 6f;
            p.escalaInicio = 1f;
            p.escalaFin = 0f;
        }
    }

    private void EfectoGood()
    {
        Onda(new Color(0.55f, 1f, 0.75f), 0.85f, 1.6f, 0.26f);

        float desfase = Random.Range(0f, 360f);
        for (int i = 0; i < chispasGood; i++)
        {
            float angulo = (desfase + i * 360f / Mathf.Max(1, chispasGood)) * Mathf.Deg2Rad;
            Vector2 direccion = new Vector2(Mathf.Cos(angulo), Mathf.Sin(angulo));

            Particula p = Lanzar(SpritesProcedurales.Circulo, Random.Range(9f, 13f), direccion * 28f,
                                 i % 2 == 0 ? new Color(0.6f, 1f, 0.8f) : Color.white, 0.45f);
            p.velocidad = direccion * alcanceParticulas * 3.2f;
            p.freno = 6f;
            p.escalaInicio = 1f;
            p.escalaFin = 0.1f;
        }
    }

    private void EfectoBad()
    {
        // Nubecitas de humo que se escurren hacia abajo
        for (int i = 0; i < 3; i++)
        {
            Vector2 inicio = new Vector2(Random.Range(-30f, 30f), Random.Range(-35f, -15f));
            Particula p = Lanzar(SpritesProcedurales.Circulo, Random.Range(20f, 28f), inicio,
                                 new Color(0.62f, 0.6f, 0.72f, 0.85f), Random.Range(0.6f, 0.8f));
            p.velocidad = new Vector2(inicio.x * 1.2f, Random.Range(-45f, -25f));
            p.freno = 2f;
            p.escalaInicio = 0.5f;
            p.escalaFin = 1.25f;
            p.vivaPocoAPoco = true;
        }

        // Gota de sudor en la esquina del corazón
        Particula gota = Lanzar(SpritesProcedurales.Gota, 18f, new Vector2(38f, 32f), new Color(0.6f, 0.85f, 1f), 0.7f);
        gota.velocidad = new Vector2(20f, 30f);
        gota.gravedad = -320f;
        gota.escalaInicio = 1f;
        gota.escalaFin = 0.8f;
        gota.vivaPocoAPoco = true;
    }

    // Anillo que se expande desde el corazón
    private void Onda(Color color, float escalaInicio, float escalaFin, float vida)
    {
        Particula p = Lanzar(SpritesProcedurales.Anillo, corazon.rect.width * 0.75f, Vector2.zero, color, vida);
        p.escalaInicio = escalaInicio;
        p.escalaFin = escalaFin;
        p.freno = 0f;
    }

    private Particula Lanzar(Sprite sprite, float tamano, Vector2 posicion, Color color, float vida)
    {
        Particula p = ParticulaLibre();

        p.imagen.sprite = sprite;
        p.rect.sizeDelta = new Vector2(tamano, tamano);
        p.rect.anchoredPosition = posicion;
        p.rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        p.rect.SetAsLastSibling(); // encima de las anteriores; el texto se sube después
        p.color = color;
        p.velocidad = Vector2.zero;
        p.gravedad = 0f;
        p.freno = 0f;
        p.giro = 0f;
        p.vida = vida;
        p.edad = 0f;
        p.escalaInicio = 1f;
        p.escalaFin = 1f;
        p.vivaPocoAPoco = false;
        p.rect.gameObject.SetActive(true);
        AnimarParticula(p, 0f);
        return p;
    }

    private Particula ParticulaLibre()
    {
        Particula masVieja = null;

        foreach (Particula p in particulas)
        {
            if (p.edad < 0f)
                return p;

            if (masVieja == null || p.edad / p.vida > masVieja.edad / masVieja.vida)
                masVieja = p;
        }

        if (particulas.Count >= MaxParticulas)
            return masVieja;

        GameObject objeto = new GameObject("Particula", typeof(RectTransform));
        objeto.layer = gameObject.layer;

        Particula nueva = new Particula();
        nueva.rect = (RectTransform)objeto.transform;
        nueva.rect.SetParent(contenedor, false);
        nueva.imagen = objeto.AddComponent<Image>();
        nueva.imagen.raycastTarget = false;

        particulas.Add(nueva);
        return nueva;
    }

    /// <summary>Mueve una partícula. Devuelve false si ya no está viva.</summary>
    private bool AnimarParticula(Particula p, float dt)
    {
        if (p.edad < 0f)
            return false;

        p.edad += dt;
        float e = p.edad / p.vida;

        if (e >= 1f)
        {
            p.edad = -1f;
            p.rect.gameObject.SetActive(false);
            return false;
        }

        p.velocidad *= Mathf.Exp(-p.freno * dt);
        p.velocidad.y += p.gravedad * dt;
        p.rect.anchoredPosition += p.velocidad * dt;
        p.rect.localRotation *= Quaternion.Euler(0f, 0f, p.giro * dt);

        float escala = Mathf.Lerp(p.escalaInicio, p.escalaFin, SalirSuave(e));
        if (p.vivaPocoAPoco)
            escala *= RebotarAfuera(Mathf.Clamp01(e / 0.18f), 1.8f);
        p.rect.localScale = Vector3.one * escala;

        Color color = p.color;
        color.a *= e < 0.6f ? 1f : 1f - (e - 0.6f) / 0.4f;
        p.imagen.color = color;
        return true;
    }

    #endregion

    #region Curvas

    private static float SalirSuave(float t)
    {
        t = 1f - t;
        return 1f - t * t * t;
    }

    // 0 → 1 pasándose de largo y volviendo, como un resorte. 'fuerza' es cuánto se pasa.
    private static float RebotarAfuera(float t, float fuerza)
    {
        t -= 1f;
        return 1f + t * t * ((fuerza + 1f) * t + fuerza);
    }

    #endregion
}
