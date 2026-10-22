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
    public void Damage(float damage, Vector3? attackerPosition = null)
    {
        currentLife = currentLife - damage;

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
            hitFeedback.PlayHitFeedbackFromAttacker(attackerPosition.Value);
        }
        else
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                hitFeedback.PlayHitFeedbackFromAttacker(player.transform.position);
            else
                hitFeedback.PlayHitFeedback();
        }

        GetComponent<MonkCrossBehaviour>()?.SetDead();
        GetComponent<MonkBehaviour>()?.NotifyMonkDamaged();

        AngelBehaviour angel = GetComponent<AngelBehaviour>();
        if (angel != null)
            angel.NotifyAngelAttacked();

        if (currentLife <= 0)
        {
            Death();
        }
    }

    public void Death()
    {
        
        Debug.Log("Enemy ha muerto");
        //skeletonEnemy.SetDead();
        // Las cruces del Monk tienen EnemyHealth pero no cuentan como enemigos del contador.
        if (contador != null)
            contador.EnemyDied(gameObject);
        if (anim != null)
            anim.SetTrigger(deathTrigger);
        
    }
}
