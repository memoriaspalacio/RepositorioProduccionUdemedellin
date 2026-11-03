using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHealth : MonoBehaviour
{
    public float maxLife;
    public float currentLife;
    public Animator anim;
    public GridPlayerMovement playerMovement;
    [SerializeField] private float tiempoEntreSonidosDano = 0.5f; // Tiempo de espera en segundos
    private float siguienteTiempoSonidoDano = 0f;
    

    [Header("UI")]
    public Slider lifeBar;

    [Header("Feedback")]
    [Tooltip("Indicador de ritmo del suelo. Si es nulo se busca en la escena al arrancar.")]
    [SerializeField] private RhythmGroundIndicator groundIndicator;

    [Header("Ritmo")]
    [Tooltip("Juez de ritmo de la escena. Si es nulo se busca al arrancar. Con el, el jugador recibe " +
             "como maximo un golpe por nota y ese golpe resta tiempo del cronometro.")]
    [SerializeField] private BeatActionJudge beatJudge;

    private bool muerto = false;

    public bool EstaMuerto => muerto;

    /// <summary>Al recuperar vida (RecompensasRitmo); lleva la vida que de verdad se sumo.</summary>
    public event System.Action<float> AlCurar;

    // Nota (contador del juez) en la que entro el ultimo golpe, y cuanto dano se aplico en ella
    private int notaDelUltimoGolpe = -1;
    private float danoEnEstaNota = 0f;


    private void Start()
    {
        currentLife = maxLife;

        if (groundIndicator == null)
            groundIndicator = FindFirstObjectByType<RhythmGroundIndicator>();

        if (beatJudge == null)
            beatJudge = FindFirstObjectByType<BeatActionJudge>();

        if (lifeBar != null)
        {
            lifeBar.maxValue = maxLife;
            lifeBar.value = currentLife;
        }
    }
    public void Damage(float damage, Vector3? attackerPosition = null)
    {
        if (muerto || damage <= 0f)
            return;

        // Con el nivel terminado (pantalla de ganar o perder) ya no se recibe dano
        if (ControladorContrarreloj.Instancia != null &&
            !ControladorContrarreloj.Instancia.EstaContando)
            return;

        damage = DanoQueEntraEnEstaNota(damage);

        if (damage <= 0f)
            return;

        currentLife = currentLife - damage;

        if (lifeBar != null)
            lifeBar.value = currentLife;

        Debug.Log("Player fue herido " + currentLife);

        if (anim != null)
            anim.SetTrigger("onHurt");

        // Retroalimentacion de golpe estilo Minecraft (Flash rojo + HurtCam / Shake)
        MinecraftHitFeedback hitFeedback = GetComponent<MinecraftHitFeedback>();
        if (hitFeedback == null)
        {
            hitFeedback = gameObject.AddComponent<MinecraftHitFeedback>();
        }

        if (attackerPosition.HasValue)
            hitFeedback.PlayHitFeedbackFromAttacker(attackerPosition.Value);
        else
            hitFeedback.PlayHitFeedback();

        if (groundIndicator != null)
            groundIndicator.TriggerDamageFeedback();

        // NUEVO: Solo reproduce el sonido si ya pasó el tiempo de espera mínimo
        if (Time.time >= siguienteTiempoSonidoDano)
        {
            SoundManager.singleton.PlaySFX("danoJugador");
            siguienteTiempoSonidoDano = Time.time + tiempoEntreSonidosDano; // Actualiza el temporizador
            tiempoEntreSonidosDano = Random.Range(1.2f, 3);
        }

        if (currentLife <= 0)
        {
            Death();
        }
    }

    /// <summary>
    /// Suma vida sin pasar del maximo y devuelve cuanta se sumo de verdad (0 si ya estaba llena,
    /// si murio o si el nivel termino).
    /// </summary>
    public float Curar(float cantidad)
    {
        if (muerto || cantidad <= 0f)
            return 0f;

        if (ControladorContrarreloj.Instancia != null &&
            !ControladorContrarreloj.Instancia.EstaContando)
            return 0f;

        float antes = currentLife;
        currentLife = Mathf.Min(maxLife, currentLife + cantidad);
        float sumada = currentLife - antes;

        if (sumada <= 0f)
            return 0f;

        if (lifeBar != null)
            lifeBar.value = currentLife;

        AlCurar?.Invoke(sumada);
        return sumada;
    }

    // Un golpe por nota: hasta que entra una nueva nota a la zona del corazon solo cuenta
    // el golpe mas fuerte. Asi una quemadura que pega varias veces seguidas no se acumula, y si
    // despues llega un ataque mas fuerte en la misma nota solo se aplica la diferencia.
    // El primer golpe de cada nota tambien resta tiempo del cronometro.
    private float DanoQueEntraEnEstaNota(float damage)
    {
        // Sin ritmo sonando no hay notas que esperar: el dano entra normal
        if (beatJudge == null || BeatManager.Instance == null || !BeatManager.Instance.IsPlaying)
            return damage;

        int notaActual = beatJudge.NotesEnteredZone;

        if (notaActual != notaDelUltimoGolpe)
        {
            notaDelUltimoGolpe = notaActual;
            danoEnEstaNota = damage;

            beatJudge.PenalizeHit();
            return damage;
        }

        if (damage <= danoEnEstaNota)
            return 0f;

        float diferencia = damage - danoEnEstaNota;
        danoEnEstaNota = damage;
        return diferencia;
    }


    public void Death()
    {
        if (muerto)
            return;

        muerto = true;

        Debug.Log("Player ha muerto");
        if (playerMovement != null)
            playerMovement.SetDead();

        if (anim != null)
            anim.SetTrigger("onDeath");
        ScreensInGame.singleton.ScreenLostActive();
        
        SoundManager.singleton.PlaySFX("perder");

    }



}

