using UnityEngine;

public class EnemyHealth : MonoBehaviour
{

    public float maxLife;
    public float currentLife;
    public void Damage(float damage)
    {
        currentLife = currentLife - damage;

        Debug.Log("Enemy fue herido fue herido " + currentLife);

        if (currentLife <= 0)
        {
            Death();
        }
    }

    public void Death()
    {
        Debug.Log("Enemy ha muerto");
    }
}
