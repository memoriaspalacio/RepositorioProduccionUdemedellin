using System.Collections;
using System.Collections.Generic;
using UdeM.Characters;
using UdeM.Base;
using UnityEngine;
using UdeM.Sounds;

public class TurtleBehaviour : Character3DNavMeshNPCBehaviour, Damageable 
{
    private Animator _anim;
    private int _life = 2;
    protected TurtleAttack _hit;
    private SoundManager _soundEffectManager;//Variables que guardan los sonidos para poder ser ejecutados
    private const string S_ROAR = "Idle1";
    private const string S_HURT = "Au1";
    private string _soundsPath = "Enemy/Sounds/";//direccin desde la carpeta Resources de donde se obtendran los sonidos
    //private ContadorEnemigos _less;
    
    private void InitSoundEffects() // aqui se añade el componente SoundManager y se añaden los clips de sonido
    {
        _soundEffectManager = gameObject.AddComponent<SoundManager>();//añade componente SoundManager
        _soundEffectManager.AddClip(_soundsPath, S_ROAR);
        _soundEffectManager.AddClip(_soundsPath, S_HURT);
    }

    protected override void Start()
    {
        base.Start();
        InitSoundEffects();
        _anim = transform.Find("Turtle").GetComponent<Animator>();
        _hit = transform.Find("Dañador").gameObject.AddComponent<TurtleAttack>();
        StartCoroutine(MomentToRoar());
    }

    private IEnumerator MomentToRoar() //Corrutina para ajustar frecuencia del sonido de rugir
    {
        while(_state == STANDBY || _state != STANDBY || _actionState == ATTACKING || _actionState != ATTACKING)
        {
            yield return new WaitForSeconds (4f);
            _soundEffectManager.PlayClip(S_ROAR);
            yield return new WaitForSeconds(9f);
        }
        
    }

    protected override void Update()
    {
        base.Update();
        _anim.SetBool("isRunning", _actionState == ATTACKING);
        _anim.SetBool("isWalking", _state != STANDBY && _actionState != ATTACKING);
    }

    public void GetDamage(int damage)
    {
        Debug.Log("me dio");
        _life = _life - damage;
        if(_life == 0){
            StartCoroutine(StopMoveDueTime(1.2f));//Corutina que impide mover al jugador mientras ejecuta ataque
            StartCoroutine(StopActionDueTime(1.3f));
            StartCoroutine(WaitForDeath(1.3f));    
        } 
    }

    protected void StartCoroutines()
    {
        StartCoroutine(StopMoveDueTime(1f));//Corutina que impide mover al jugador mientras ejecuta ataque
        StartCoroutine(StopActionDueTime(1.1f));
        
    }

    protected IEnumerator WaitForDeath(float time) {
        transform.Find("Dañador").GetComponent<SphereCollider>().enabled = false;
        _anim.SetTrigger("onHurt");
        _soundEffectManager.PlayClip(S_HURT);

        yield return new WaitForSeconds(time);
        Destroy(gameObject);
    }
    
    protected class TurtleAttack : MonoBehaviour{
        protected void OnTriggerEnter(Collider other)// al tocar el enemigo el genera daño a quien tenga tag Player
        {
            Damageable target = other.gameObject.GetComponent<Damageable>();
            if(target != null && (target as MonoBehaviour).tag == ("Player")) 
            {
                target.GetDamage(1);
                transform.parent.GetComponent<TurtleBehaviour>().StartCoroutines();
            }
        }
    }

    
   
    

    

    

    
}