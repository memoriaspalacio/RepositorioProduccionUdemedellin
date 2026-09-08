using UnityEngine;
using UnityEngine.UI;

public class PlayerHealth : MonoBehaviour
{
    public float maxLife;
    public float currentLife;
    public Animator anim;
    public GridPlayerMovement playerMovement;

    [Header("UI")]
    public Slider lifeBar;


    private void Start()
    {
        currentLife = maxLife;

        lifeBar.maxValue = maxLife;
        lifeBar.value = currentLife;
    }
    public void Damage(float damage)
    {
        currentLife = currentLife - damage;

        // Actualizar barra
        lifeBar.value = currentLife;

        Debug.Log("Player fue herido " + currentLife);
        anim.SetTrigger("onHurt");

        if(currentLife <= 0)
        {
            Death();
        }
    }

    public void Death()
    {
        Debug.Log("Player ha muerto");
        playerMovement.SetDead();
        anim.SetTrigger("onDeath");
        ScreensInGame.singleton.ScreenLostActive();
        
    }



}
