using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UdeM.Characters;
using UdeM.Base;
using UdeM.Sounds;

public class ChicaBehaviour : Character3DPlayerThirdPersonBehaviour, Damageable
{
    private Animator _anim;
    private int _life = 3;
    protected ChicaAttack _hit;
    [SerializeField] private TextMeshProUGUI _lifeText;
    private const string S_JUMP = "Jump";//constantes de que contienen sonidos
    private const string S_ATTACK = "Sword";
    private const string S_HURTS = "Hurt";
    private const string S_IMPACT = "Impact";
    private const string S_LAND = "land2";
    private SoundManager _soundEffectManager;//Variables que guardan los sonidos para poder ser ejecutados
    private string _soundsPath = "Chica/Sounds/";//direccin desde la carpeta Resources de donde se obtendran los sonidos
    [SerializeField] private GameObject _death;//variable que guarda la pantalla de muerte

    protected override void Awake ()
    {
        base.Awake();
        InitSoundEffects();//Inicialización de funciones sonido
    }
    
    protected override void Start()
    {
        base.Start();
        _anim = transform.Find("ModelChica").GetComponent<Animator>();
        _hit = transform.Find("AttackHitBox").gameObject.AddComponent<ChicaAttack>();
        AttackColliderOff();//Se inicia esta funcion para que el objeto que tiene el ataque del personaje este apagado
        _lifeText.text = "" + _life;

    }

    private void InitSoundEffects()//funcion añade el componente SoundManager y todos los efectos de sonido a excepción de los pasos
    {
        _soundEffectManager = gameObject.AddComponent<SoundManager>();//añade componente SoundManager
        _soundEffectManager.AddClip(_soundsPath, S_LAND);//guardan las direcciones y los sonidos para poder ser ejecutados posteriormente
        _soundEffectManager.AddClip(_soundsPath, S_HURTS);
        _soundEffectManager.AddClip(_soundsPath, S_ATTACK);
        _soundEffectManager.AddClip(_soundsPath, S_JUMP);
        _soundEffectManager.AddClip(_soundsPath, S_IMPACT);
    }

    protected override void Update()
    {
        base.Update();
        _anim.SetFloat("Speed", _direction.magnitude);
        _anim.SetFloat("Height", _height);
        if(Input.GetButtonDown("Fire1") && _isGrounded && _canAction){
            Attack();
        }
    }

    protected override void OnLand()
    {
        base.OnLand();
        _anim.SetTrigger("OnLand");
        _soundEffectManager.PlayClip(S_LAND);
        StartCoroutine(StopMoveDueTime(0.5f));
    }

    protected override void OnStartFalling()
    {
        base.OnStartFalling();
        _anim.SetTrigger("OnStartFalling");
    }

    protected override void Jump()
    {
        base.Jump();
        _anim.SetTrigger("OnJump");
        _soundEffectManager.PlayClip(S_JUMP);//Ejecuta sonido de salto
    }

    private void Attack()//Funcion que realiza todo lo que tiene que ver con el ataque
    {
        //Ejecucion de Corrutinas
        StartCoroutine(StopMoveDueTime(0.30f));//Corutina que impide mover al jugador mientras ejecuta ataque
        StartCoroutine(StopActionDueTime(0.35f));// Corrutina que impide ejecutar una accion diferente al ataque
        _anim.SetTrigger("onAttack");//Ejecucion de animacion de ataque
        _soundEffectManager.PlayClip(S_ATTACK);//Reproducción de sonido de ataque
    }

    //Funciones que activan y desactivan el collider del ataque
    public void AttackColliderOn()
    {
        transform.Find("AttackHitBox").GetComponent<BoxCollider>().enabled = true;//Activa el collider
    }

    public void AttackColliderOff()
    {
        transform.Find("AttackHitBox").GetComponent<BoxCollider>().enabled = false;//Desactiva el collider
    }

    public void GetDamage(int damage)
    {
        Debug.Log("Ayy me pego");
        StartCoroutine(StopMoveDueTime(1f));//Corutina que impide mover al jugador mientras ejecuta ataque
        StartCoroutine(StopActionDueTime(1.1f));
        _anim.SetTrigger("onHurt");
        _soundEffectManager.PlayClip(S_HURTS);
        _soundEffectManager.PlayClip(S_IMPACT);//Sonido de estar herido
        _life = _life - damage;
        _lifeText.text = "" + _life;//muestra la vida actual en la interfaz de juego luego de ser restada
        if(_life == 0){
            StartCoroutine(WaitForDeath(1.1f));
        }
    }

    protected IEnumerator WaitForDeath(float time) {
        
        _anim.SetTrigger("onDeath");
        yield return new WaitForSeconds(time);
        _death.SetActive(true);//Activa la pantalla de muerte
        Destroy(gameObject);
    }

    protected class ChicaAttack : MonoBehaviour
    {
        protected void OnTriggerStay(Collider other)// Funcion que permite hacer daño al enemigo
        {
            
            //Debug.Log("Entro");
            Damageable target = other.gameObject.GetComponent<Damageable>();
            if(target != null && (target as MonoBehaviour).tag == ("Enemy")) 
            {
                Debug.Log("loataque");
                target.GetDamage(1);
            }
        }
    }
}
