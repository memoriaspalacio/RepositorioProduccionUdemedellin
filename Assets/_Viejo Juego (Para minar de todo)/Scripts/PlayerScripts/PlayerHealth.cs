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

        SoundManager.singleton.PlaySFX("danoJugador");

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
