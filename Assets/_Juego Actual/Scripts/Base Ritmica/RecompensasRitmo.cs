using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Lo que se gana jugando bien, como contrapeso de las penalizaciones del juez:
///   - Racha de PERFECT: cada perfectsParaVida PERFECT seguidos devuelven vida. Cualquier otra
///     calificacion (GOOD, BAD, MISS) o un golpe al aire corta la racha.
///   - Enemigo derrotado: suma tiempo al cronometro y devuelve vida. Del enemigo salen un "+X"
///     que vuela al cronometro y unos corazones que vuelan a la barra de vida; el tiempo y la vida
///     entran al llegar, asi se ve de donde vienen.
/// La vida va en fraccion de la vida maxima del jugador, asi vale igual en todos los niveles.
///
/// Se agrega solo en cada nivel con juez de ritmo (en su mismo objeto). Para cambiar los valores
/// de un nivel, pon el componente a mano en el objeto del juez y ajustalo ahi.
/// </summary>
[DisallowMultipleComponent]
public class RecompensasRitmo : MonoBehaviour
{
    [Header("Racha de PERFECT")]
    [Tooltip("PERFECTOS seguidos que hacen falta para recuperar vida. Cada vez que la racha llega a un multiplo, vuelve a dar.")]
    [SerializeField, Min(1)] private int perfectsParaVida = 8;

    [Tooltip("Vida que devuelve la racha, en fraccion de la vida maxima (0.1 = 10 %).")]
    [SerializeField, Range(0f, 1f)] private float vidaPorRacha = 0.1f;

    [Header("Enemigo derrotado")]
    [Tooltip("Segundos que suma al cronometro cada enemigo derrotado.")]
    [SerializeField, Min(0f)] private float tiempoPorEnemigo = 15f;

    [Tooltip("Vida que devuelve cada enemigo derrotado, en fraccion de la vida maxima.")]
    [SerializeField, Range(0f, 1f)] private float vidaPorEnemigo = 0.15f;

    [Tooltip("Segundos que tardan el tiempo y los corazones en volar del enemigo al HUD.")]
    [SerializeField, Min(0.1f)] private float duracionVuelo = 0.65f;

    [Header("Referencias")]
    [Tooltip("Si esta vacio se busca en la escena.")]
    [SerializeField] private BeatActionJudge beatJudge;
    [Tooltip("Si esta vacio se busca en la escena.")]
    [SerializeField] private PlayerHealth jugador;

    static readonly Color VidaArriba = new Color(1f, 0.75f, 0.8f, 1f);
    static readonly Color VidaAbajo = new Color(0.9f, 0.12f, 0.3f, 1f);
    static readonly Color RachaArriba = new Color(1f, 0.96f, 0.5f, 1f);
    static readonly Color RachaAbajo = new Color(1f, 0.5f, 0.12f, 1f);
    static readonly Color TiempoArriba = new Color(0.9f, 1f, 0.6f, 1f);
    static readonly Color TiempoAbajo = new Color(0.25f, 0.85f, 0.45f, 1f);
    static readonly Color ColorCorazon = new Color(0.95f, 0.18f, 0.32f, 1f);

    static Sprite spriteCorazon;

    int racha;
    Coroutine latidoBarra;

    /// <summary>PERFECT seguidos en este momento.</summary>
    public int Racha => racha;

    #region Arranque

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Registrar()
    {
        SceneManager.sceneLoaded -= AlCargarEscena;
        SceneManager.sceneLoaded += AlCargarEscena;
        AlCargarEscena(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void AlCargarEscena(Scene escena, LoadSceneMode modo)
    {
        if (FindFirstObjectByType<RecompensasRitmo>() != null)
            return;

        BeatActionJudge juez = FindFirstObjectByType<BeatActionJudge>();
        if (juez != null && juez.gameObject.scene == escena)
            juez.gameObject.AddComponent<RecompensasRitmo>();
    }

    void OnEnable()
    {
        if (beatJudge == null)
            beatJudge = FindFirstObjectByType<BeatActionJudge>();
        if (jugador == null)
            jugador = FindFirstObjectByType<PlayerHealth>();

        if (beatJudge != null)
        {
            beatJudge.OnBeatGraded += AlCalificar;
            beatJudge.OnPenaltyApplied += CortarRacha;
        }

        if (jugador != null)
            jugador.AlCurar += AlCurarJugador;

        EnemyHealth.AlMorirEnemigo += AlMorirEnemigo;
    }

    void OnDisable()
    {
        if (beatJudge != null)
        {
            beatJudge.OnBeatGraded -= AlCalificar;
            beatJudge.OnPenaltyApplied -= CortarRacha;
        }

        if (jugador != null)
            jugador.AlCurar -= AlCurarJugador;

        EnemyHealth.AlMorirEnemigo -= AlMorirEnemigo;
    }

    #endregion

    #region Recompensas

    void AlCalificar(float beat, JudgementResult resultado)
    {
        if (resultado != JudgementResult.Perfect)
        {
            racha = 0;
            return;
        }

        racha++;
        if (racha % perfectsParaVida != 0)
            return;

        float curada = Curar(vidaPorRacha);
        RectTransform barra = BarraDeVida();
        if (barra != null)
        {
            string texto = curada > 0f ? $"¡{racha} PERFECTOS!  +VIDA" : $"¡{racha} PERFECTOS!";
            TextoFlotante.Crear(barra, texto, new Vector2(0f, -55f), new Vector2(0f, 40f),
                RachaArriba, RachaAbajo, 26f, 1.3f, false);
        }
    }

    void CortarRacha()
    {
        racha = 0;
    }

    void AlMorirEnemigo(EnemyHealth enemigo)
    {
        ControladorContrarreloj reloj = ControladorContrarreloj.Instancia;
        if (reloj == null || !reloj.EstaContando)
            return;

        Vector3 origen = enemigo.transform.position + Vector3.up * 1.2f;

        if (tiempoPorEnemigo > 0f)
        {
            GameObject textoReloj = GameObject.Find("TextoContrarreloj");
            RectTransform destino = textoReloj != null ? textoReloj.transform as RectTransform : null;
            float segundos = tiempoPorEnemigo;
            if (!VolarTexto($"+{segundos:0.#}", origen, destino, () => reloj.AgregarTiempo(segundos, false)))
                reloj.AgregarTiempo(segundos);
        }

        if (vidaPorEnemigo > 0f && jugador != null && jugador.currentLife < jugador.maxLife)
        {
            float fraccion = vidaPorEnemigo;
            if (!VolarCorazones(origen, BarraDeVida(), () => Curar(fraccion)))
                Curar(fraccion);
        }
    }

    float Curar(float fraccion)
    {
        if (jugador == null || fraccion <= 0f)
            return 0f;

        return jugador.Curar(jugador.maxLife * fraccion);
    }

    #endregion

    #region Barra de vida

    RectTransform BarraDeVida()
    {
        return jugador != null && jugador.lifeBar != null ? (RectTransform)jugador.lifeBar.transform : null;
    }

    void AlCurarJugador(float cantidad)
    {
        RectTransform barra = BarraDeVida();
        if (barra == null)
            return;

        TextoFlotante.Crear(barra, $"+{cantidad:0.#}", new Vector2(70f, 0f), new Vector2(0f, 40f),
            VidaArriba, VidaAbajo, 28f, 0.9f, false);

        if (latidoBarra != null)
            StopCoroutine(latidoBarra);
        latidoBarra = StartCoroutine(LatirBarra(barra));
    }

    /// <summary>La barra late dos veces, como un corazon que se recupera.</summary>
    IEnumerator LatirBarra(RectTransform barra)
    {
        Vector3 original = barra.localScale;
        float edad = 0f;

        while (edad < 0.6f && barra != null)
        {
            edad += Time.unscaledDeltaTime;
            float latido = Mathf.Max(Golpe(edad, 0f), 0.7f * Golpe(edad, 0.22f));
            barra.localScale = original * (1f + 0.18f * latido);
            yield return null;
        }

        if (barra != null)
            barra.localScale = original;
        latidoBarra = null;
    }

    static float Golpe(float edad, float inicio)
    {
        float t = (edad - inicio) / 0.18f;
        return t < 0f || t > 1f ? 0f : Mathf.Sin(t * Mathf.PI);
    }

    #endregion

    #region Vuelo del enemigo al HUD

    /// <summary>Un "+X" pequeno que sale del enemigo y vuela al destino. False si no hay donde volar.</summary>
    bool VolarTexto(string contenido, Vector3 origenMundo, RectTransform destino, Action alLlegar)
    {
        if (!PuntoDePartida(origenMundo, destino, out RectTransform lienzo, out Vector2 desde, out Vector2 hasta))
            return false;

        GameObject objeto = new GameObject("Vuelo tiempo", typeof(RectTransform));
        Preparar(objeto, lienzo, desde, new Vector2(160f, 50f));

        TextMeshProUGUI tmp = objeto.AddComponent<TextMeshProUGUI>();
        TMP_Text referencia = destino.GetComponent<TMP_Text>();
        if (referencia != null)
            tmp.font = referencia.font;
        tmp.fontSize = 30f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        tmp.text = contenido;
        TextoFlotante.EstiloCaricatura(tmp, 0.3f);
        TextoFlotante.Degradado(tmp, TiempoArriba, TiempoAbajo);

        objeto.AddComponent<VueloUI>().Iniciar(desde, hasta, duracionVuelo, 0f, () =>
        {
            alLlegar?.Invoke();
            if (destino != null)
                TextoFlotante.Destellos(destino, TiempoArriba, 5);
        });
        return true;
    }

    /// <summary>Tres corazones que salen del enemigo y vuelan a la barra; la vida entra con el primero.</summary>
    bool VolarCorazones(Vector3 origenMundo, RectTransform destino, Action alLlegar)
    {
        if (!PuntoDePartida(origenMundo, destino, out RectTransform lienzo, out Vector2 desde, out Vector2 hasta))
            return false;

        for (int i = 0; i < 3; i++)
        {
            GameObject objeto = new GameObject("Vuelo corazon", typeof(RectTransform));
            Vector2 salida = desde + UnityEngine.Random.insideUnitCircle * 30f;
            Preparar(objeto, lienzo, salida, Vector2.one * 34f);

            Image imagen = objeto.AddComponent<Image>();
            imagen.sprite = SpriteCorazon();
            imagen.color = ColorCorazon;
            imagen.raycastTarget = false;

            Action llegada = i == 0 ? alLlegar : null;
            objeto.AddComponent<VueloUI>().Iniciar(salida, hasta, duracionVuelo + i * 0.08f, i * 0.06f, llegada);
        }

        return true;
    }

    /// <summary>Donde estan el enemigo y el destino dentro del Canvas del destino.</summary>
    static bool PuntoDePartida(Vector3 origenMundo, RectTransform destino, out RectTransform lienzo, out Vector2 desde, out Vector2 hasta)
    {
        lienzo = null;
        desde = hasta = Vector2.zero;

        Camera camara = Camera.main;
        Canvas canvas = destino != null ? destino.GetComponentInParent<Canvas>() : null;
        if (camara == null || canvas == null || !destino.gameObject.activeInHierarchy)
            return false;

        canvas = canvas.rootCanvas;
        lienzo = (RectTransform)canvas.transform;
        Camera camaraUI = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

        Vector3 pantalla = camara.WorldToScreenPoint(origenMundo);
        if (pantalla.z < 0f)
            return false;

        Vector2 pantallaDestino = RectTransformUtility.WorldToScreenPoint(camaraUI, destino.position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(lienzo, pantalla, camaraUI, out desde);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(lienzo, pantallaDestino, camaraUI, out hasta);
        return true;
    }

    static void Preparar(GameObject objeto, RectTransform lienzo, Vector2 posicion, Vector2 tamano)
    {
        objeto.layer = lienzo.gameObject.layer;
        RectTransform rect = (RectTransform)objeto.transform;
        rect.SetParent(lienzo, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = tamano;
        rect.anchoredPosition = posicion;
        rect.SetAsLastSibling();
    }

    /// <summary>Corazon dibujado por codigo (la curva clasica), con el borde algo mas oscuro.</summary>
    static Sprite SpriteCorazon()
    {
        if (spriteCorazon != null)
            return spriteCorazon;

        const int lado = 64;
        Texture2D textura = new Texture2D(lado, lado, TextureFormat.RGBA32, false)
        {
            name = "CorazonRecompensa",
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color[] pixeles = new Color[lado * lado];
        for (int y = 0; y < lado; y++)
        {
            for (int x = 0; x < lado; x++)
            {
                float u = (x + 0.5f) / lado * 2.6f - 1.3f;
                float v = (y + 0.5f) / lado * 2.6f - 1.4f;
                float a = u * u + v * v - 1f;
                float f = a * a * a - u * u * v * v * v;
                float dentro = Mathf.Clamp01(-f * 18f);
                float brillo = Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(-f * 6f));
                pixeles[y * lado + x] = new Color(brillo, brillo, brillo, dentro);
            }
        }

        textura.SetPixels(pixeles);
        textura.Apply();
        spriteCorazon = Sprite.Create(textura, new Rect(0, 0, lado, lado), new Vector2(0.5f, 0.5f), 100f);
        spriteCorazon.hideFlags = HideFlags.HideAndDontSave;
        return spriteCorazon;
    }

    /// <summary>
    /// Vuela en arco de 'desde' a 'hasta': aparece con un salto, acelera hacia el destino y se
    /// encoge al llegar. Llama a alLlegar al tocarlo y se destruye.
    /// </summary>
    class VueloUI : MonoBehaviour
    {
        RectTransform rect;
        Vector2 desde;
        Vector2 hasta;
        Vector2 control;
        float duracion;
        float espera;
        float edad;
        Action alLlegar;

        public void Iniciar(Vector2 desde, Vector2 hasta, float duracion, float espera, Action alLlegar)
        {
            rect = (RectTransform)transform;
            this.desde = desde;
            this.hasta = hasta;
            this.duracion = duracion;
            this.espera = espera;
            this.alLlegar = alLlegar;
            // Punto de control por encima y hacia un lado: la trayectoria hace un arco
            control = Vector2.Lerp(desde, hasta, 0.3f) + new Vector2(UnityEngine.Random.Range(-90f, 90f), 160f);
            rect.localScale = Vector3.zero;
        }

        void Update()
        {
            edad += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01((edad - espera) / duracion);
            float curva = t * t * (3f - 2f * t) * 0.35f + t * t * 0.65f;

            Vector2 a = Vector2.Lerp(desde, control, curva);
            Vector2 b = Vector2.Lerp(control, hasta, curva);
            rect.anchoredPosition = Vector2.Lerp(a, b, curva);

            float aparecer = Mathf.Clamp01((edad - espera) * 8f);
            float llegar = 1f - Mathf.SmoothStep(0.8f, 1f, t) * 0.6f;
            rect.localScale = Vector3.one * aparecer * llegar * (1f + 0.25f * Mathf.Sin(t * Mathf.PI));

            if (t >= 1f)
            {
                alLlegar?.Invoke();
                alLlegar = null;
                Destroy(gameObject);
            }
        }
    }

    #endregion
}
