using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnimFunction : MonoBehaviour
{
     public void AttackColliderOn()
    {
        GetComponentInParent<ChicaBehaviour>().AttackColliderOn();
    }

    public void AttackColliderOff()
    {
        GetComponentInParent<ChicaBehaviour>().AttackColliderOff();
    }
}
