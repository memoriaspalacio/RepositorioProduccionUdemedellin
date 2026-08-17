using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public float maxLife;
    public float currentLife;


    public void Damage(float damage)
    {
        currentLife = currentLife - damage;

        Debug.Log("Player fue herido " + currentLife);

        if(currentLife <= 0)
        {
            Death();
        }
    }

    public void Death()
    {
        Debug.Log("Player ha muerto");
    }



}
