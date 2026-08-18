using UdeM.Characters;
using UnityEngine;

public class EnemyHealth : MonoBehaviour
{

    public float maxLife;
    public float currentLife;
    public Animator anim;
    public SkeletonBehaviour skeletonEnemy;
    public void Damage(float damage)
    {
        currentLife = currentLife - damage;

        Debug.Log("Enemy fue herido fue herido " + currentLife);
        anim.SetTrigger("onHurt");

        if (currentLife <= 0)
        {
            Death();
        }
    }

    public void Death()
    {
        
        Debug.Log("Enemy ha muerto");
        skeletonEnemy.SetDead();
        anim.SetTrigger("onDeath");
    }
}
