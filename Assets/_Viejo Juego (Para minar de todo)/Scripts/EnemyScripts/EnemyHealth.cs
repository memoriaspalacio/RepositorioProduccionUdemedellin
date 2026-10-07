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
    public void Damage(float damage)
    {
        currentLife = currentLife - damage;

        Debug.Log("Enemy fue herido fue herido " + currentLife);
        if (anim != null)
            anim.SetTrigger(hurtTrigger);

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
        contador.EnemyDied(gameObject);
        if (anim != null)
            anim.SetTrigger(deathTrigger);
        
    }
}
