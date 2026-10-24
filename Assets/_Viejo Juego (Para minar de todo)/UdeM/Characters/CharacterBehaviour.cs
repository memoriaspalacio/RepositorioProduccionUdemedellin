using UnityEngine;
using UdeM.Base;

namespace UdeM.Characters
{
    public abstract class CharacterBehaviour : CustomMonoBehaviour
    {
        // Indica si el personaje puede realizar movimiento.
        [SerializeField] protected bool _canMove;

        // Inicializa el estado base de movimiento del personaje.
        protected override void Awake()
        {
            base.Awake();
            _canMove = true;
        }
    }
}