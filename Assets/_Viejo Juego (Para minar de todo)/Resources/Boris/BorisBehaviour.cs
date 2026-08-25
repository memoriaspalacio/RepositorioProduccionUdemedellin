using System.Collections;
using System.Collections.Generic;
using UdeM.Characters;
using UnityEngine;

public class BorisBehaviour : Character3DPlayerThirdPersonBehaviour
{
    private Animator _anim;
    

    protected override void Start(){
        base.Start();
        _anim = transform.Find("ModelBoris").GetComponent<Animator>();
    }

    protected override void Update(){
        base.Update();
        _anim.SetFloat("Speed", _direction.magnitude);
        _anim.SetFloat("Height", _height);
    }

    protected override void OnLand()
    {
        base.OnLand();
        _anim.SetTrigger("OnLand");
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
    }
}
