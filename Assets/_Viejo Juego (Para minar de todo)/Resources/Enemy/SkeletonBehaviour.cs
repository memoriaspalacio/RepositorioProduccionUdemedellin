using System.Collections;
using UnityEngine;

namespace UdeM.Characters
{
    public class SkeletonBehaviour : Character3DNavMeshGridNPCBehaviour
    {
        // Indica si el esqueleto ya se encuentra muerto.
        private bool isDead = false;

        // Guarda el Animator usado por las animaciones del esqueleto. Puedes arrastrarlo desde el Inspector.
        [Header("Animacion")]
        public Animator animator;

        // Guarda el nombre del trigger usado para el estado inactivo.
        [SerializeField]
        private string idleTrigger = "Idle";

        // Guarda el nombre del trigger usado para ejecutar el ataque.
        [SerializeField]
        private string attackTrigger = "Attack";

        // Define el tiempo entre la preparacion y la comprobacion del golpe.
        [SerializeField] private float reloadTime = 0.5f;

        // Indica si existe un ataque en proceso.
        private bool isPreparingAttack;

        // Guarda la referencia al componente de vida del jugador.
        public PlayerHealth playerHealth;

        // Busca automaticamente el Animator si no fue asignado en el Inspector.
        protected override void Start()
        {
            base.Start();

            if (animator == null)
                animator = GetComponentInChildren<Animator>();
        }

        // Detiene el comportamiento del esqueleto cuando muere.
        public void SetDead()
        {
            if (isDead)
                return;

            isDead = true;
            isPreparingAttack = false;
            behaviourEnabled = false;

            StopAllCoroutines();
        }

        // Genera el valor aleatorio de dano realizado por el esqueleto.
        private float HitFunction()
        {
            return Random.Range(1, 7);
        }

        // Inicia la preparacion del ataque si el esqueleto puede atacar.
        protected override bool Attack(
            Vector2Int direction,
            Vector2Int targetCell)
        {
            if (isDead)
                return false;

            if (isPreparingAttack)
                return false;

            StartCoroutine(PrepareAndAttack(direction));

            return true;
        }

        // Prepara el ataque y aplica dano si el objetivo sigue en una celda vecina.
        private IEnumerator PrepareAndAttack(Vector2Int direction)
        {
            if (isDead)
                yield break;

            isPreparingAttack = true;

            FaceGridDirection(direction);

            if (animator != null)
                animator.SetTrigger(attackTrigger);

            // Esperamos el tiempo necesario (reloadTime) para que la animacion coincida con el impacto.
            yield return new WaitForSeconds(reloadTime);

            if (CurrentTarget != null)
            {
                Vector2Int currentTargetCell =
                    Grid.WorldToCell(
                        CurrentTarget.transform.position
                    );

                Vector2Int difference =
                    currentTargetCell - CurrentGridCell;

                int distance =
                    Mathf.Abs(difference.x) +
                    Mathf.Abs(difference.y);

                if (distance == 1)
                {
                    FaceGridDirection(difference);

                    SoundManager.singleton.PlaySFX("esqueletoAtaque");

                    Debug.Log(
                        $"El esqueleto ataco la celda {currentTargetCell}.",
                        this
                    );

                    playerHealth.Damage(HitFunction());
                }
            }

            isPreparingAttack = false;
        }
    }
}