using System.Collections;
using System.Collections.Generic;
using UdeM.Characters;
using UnityEngine;

public class EnemyBehaviour : Character3DNavMeshNPCBehaviour 
{
    private Animator _anim;

     protected override void Start()
    {
        base.Start();
        _anim = transform.Find("Chico").GetComponent<Animator>();
    }

    protected override void Update()
    {
        base.Update();
        //_anim.SetBool("isWalking", _actionState == PATROLLING);
        _anim.SetBool("isRunning", _actionState == ATTACKING);
        _anim.SetBool("isWalking", _state != STANDBY && _actionState != ATTACKING);
        //_anim.SetBool("isQuiet", _patrolPointTime >= 0);
    }

    
}
