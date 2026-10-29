using UdeM.Characters;
using UnityEngine;

public class EnemyHealth : MonoBehaviour
{

    public float maxLife;
    public float currentLife;
    public Animator anim;
    //public SkeletonBehaviour skeletonEnemy;
    public ContadorEnemigos contador;

    [Header("Animaciones")]
    public string hurtTrigger = "onHurt";
    public string deathTrigger = "onDeath";

    /// <summary>
    /// Al morir un enemigo de verdad (no las cruces del Monk, que tambien tienen EnemyHealth).
    /// RecompensasRitmo da tiempo y vida por cada uno.
    /// </summary>
    public static event System.Action<EnemyHealth> AlMorirEnemigo;

    // Evita repetir la muerte si llegan dos golpes en el mismo frame.
    private bool muerto;
    // Desde donde llego el ultimo golpe; el efecto de muerte empuja las particulas lejos de ahi.
    private Vector3? origenUltimoGolpe;

    public void Damage(float damage, Vector3? attackerPosition = null)
    {
        currentLife = currentLife - damage;
        bool golpeLetal = currentLife <= 0;

        Debug.Log("Enemy fue herido fue herido " + currentLife);
        if (anim != null)
            anim.SetTrigger(hurtTrigger);

        // Retroalimentacion de golpe estilo Minecraft (Flash rojo + shake/tilt)
        MinecraftHitFeedback hitFeedback = GetComponent<MinecraftHitFeedback>();
        if (hitFeedback == null)
        {
            hitFeedback = gameObject.AddComponent<MinecraftHitFeedback>();
        }

        if (attackerPosition.HasValue)
        {
            origenUltimoGolpe = attackerPosition.Value;
            hitFeedback.PlayHitFeedbackFromAttacker(attackerPosition.Value);
        }
        else
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                origenUltimoGolpe = player.transform.position;
                hitFeedback.PlayHitFeedbackFromAttacker(player.transform.position);
            }
            else
                hitFeedback.PlayHitFeedback();
        }

        GetComponent<MonkCrossBehaviour>()?.SetDead();
        // El golpe letal no teletransporta al Monk: muere (y arde) donde lo golpearon.
        if (!golpeLetal)
            GetComponent<MonkBehaviour>()?.NotifyMonkDamaged();

        AngelBehaviour angel = GetComponent<AngelBehaviour>();
        if (angel != null)
            angel.NotifyAngelAttacked();

        if (golpeLetal)
        {
            Death();
        }
    }

    public void Death()
    {
        if (muerto)
            return;
        muerto = true;

        Debug.Log("Enemy ha muerto");
        //skeletonEnemy.SetDead();
        // Va antes del contador porque este destruye al enemigo: el efecto copia su cuerpo y
        // sigue en un objeto aparte (esqueleto en polvo, monjas y Monk en llamas, angeles en rocas).
        // Si el efecto falla, el enemigo igual tiene que morir y contar para la victoria.
        try
        {
            EfectoMuerte.Reproducir(gameObject, origenUltimoGolpe);
        }
        catch (System.Exception error)
        {
            Debug.LogException(error, this);
        }
        // Antes del contador, que destruye al enemigo
        if (GetComponent<MonkCrossBehaviour>() == null)
            AlMorirEnemigo?.Invoke(this);

        // Las cruces del Monk tienen EnemyHealth pero no cuentan como enemigos del contador.
        if (contador != null)
            contador.EnemyDied(gameObject);
        if (anim != null)
            anim.SetTrigger(deathTrigger);
        
    }
}
