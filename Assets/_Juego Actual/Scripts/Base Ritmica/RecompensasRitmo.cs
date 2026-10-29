using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Lo que se gana jugando bien, como contrapeso de las penalizaciones del juez:
///   - Racha de PERFECT: cada perfectsParaVida PERFECT seguidos devuelven vida. Cualquier otra
///     calificacion (GOOD, BAD, MISS) o un golpe al aire corta la racha.
///   - Enemigo derrotado: suma tiempo al cronometro y devuelve vida.
/// La vida va en fraccion de la vida maxima del jugador, asi vale igual en todos los niveles.
///
/// Se agrega solo en cada nivel con juez de ritmo (en su mismo objeto). Para cambiar los valores
/// de un nivel, pon el componente a mano en el objeto del juez y ajustalo ahi.
/// </summary>
[DisallowMultipleComponent]
public class RecompensasRitmo : MonoBehaviour
{
    [Header("Racha de PERFECT")]
    [Tooltip("PERFECT seguidos que hacen falta para recuperar vida. Cada vez que la racha llega a un multiplo, vuelve a dar.")]
    [SerializeField, Min(1)] private int perfectsParaVida = 8;

    [Tooltip("Vida que devuelve la racha, en fraccion de la vida maxima (0.1 = 10 %).")]
    [SerializeField, Range(0f, 1f)] private float vidaPorRacha = 0.1f;

    [Header("Enemigo derrotado")]
    [Tooltip("Segundos que suma al cronometro cada enemigo derrotado.")]
    [SerializeField, Min(0f)] private float tiempoPorEnemigo = 3f;

    [Tooltip("Vida que devuelve cada enemigo derrotado, en fraccion de la vida maxima.")]
    [SerializeField, Range(0f, 1f)] private float vidaPorEnemigo = 0.1f;

    [Header("Referencias")]
    [Tooltip("Si esta vacio se busca en la escena.")]
    [SerializeField] private BeatActionJudge beatJudge;
    [Tooltip("Si esta vacio se busca en la escena.")]
    [SerializeField] private PlayerHealth jugador;

    static readonly Color VidaArriba = new Color(1f, 0.75f, 0.8f, 1f);
    static readonly Color VidaAbajo = new Color(0.9f, 0.12f, 0.3f, 1f);
    static readonly Color RachaArriba = new Color(1f, 0.96f, 0.5f, 1f);
    static readonly Color RachaAbajo = new Color(1f, 0.5f, 0.12f, 1f);

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
            string texto = curada > 0f ? $"¡{racha} PERFECT!  +VIDA" : $"¡{racha} PERFECT!";
            TextoFlotante.Crear(barra, texto, new Vector2(0f, -55f), new Vector2(0f, 45f),
                RachaArriba, RachaAbajo, 34f, 1.3f, false);
        }
    }

    void CortarRacha()
    {
        racha = 0;
    }

    void AlMorirEnemigo(EnemyHealth enemigo)
    {
        ControladorContrarreloj reloj = ControladorContrarreloj.Instancia;
        if (reloj != null && tiempoPorEnemigo > 0f)
            reloj.AgregarTiempo(tiempoPorEnemigo);

        Curar(vidaPorEnemigo);
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

        TextoFlotante.Crear(barra, $"+{cantidad:0.#}", new Vector2(70f, 0f), new Vector2(0f, 50f),
            VidaArriba, VidaAbajo, 38f, 1f, false);

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
}
