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


    private void Start()
    {
        currentLife = maxLife;

        if (groundIndicator == null)
            groundIndicator = FindFirstObjectByType<RhythmGroundIndicator>();

        if (lifeBar != null)
        {
            lifeBar.maxValue = maxLife;
            lifeBar.value = currentLife;
        }
    }
    public void Damage(float damage, Vector3? attackerPosition = null)
    {
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

    
    public void Death()
    {
        Debug.Log("Player ha muerto");
        if (playerMovement != null)
            playerMovement.SetDead();

        if (anim != null)
            anim.SetTrigger("onDeath");
        ScreensInGame.singleton.ScreenLostActive();
        
        SoundManager.singleton.PlaySFX("perder");

    }



}

