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


    private void Start()
    {
        currentLife = maxLife;

        if (lifeBar != null)
        {
            lifeBar.maxValue = maxLife;
            lifeBar.value = currentLife;
        }
    }
    public void Damage(float damage)
    {
        currentLife = currentLife - damage;

        if (lifeBar != null)
            lifeBar.value = currentLife;

        Debug.Log("Player fue herido " + currentLife);

        if (anim != null)
            anim.SetTrigger("onHurt");

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
        
    }



}
