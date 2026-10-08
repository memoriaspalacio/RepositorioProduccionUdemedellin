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

        // Guarda el nombre del trigger usado para la animación de recarga.
        [SerializeField]
        private string cooldownTrigger = "Cooldown";

        // Define el tiempo entre la preparacion y la comprobacion del golpe.
        [SerializeField] private float reloadTime = 0.5f;

        // Define el tiempo que dura la animación de ataque antes de poder actuar de nuevo.
        [SerializeField] private float attackDuration = 0.5f;

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
                animator.SetTrigger(cooldownTrigger);

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
                    animator.SetTrigger(attackTrigger);
                    SoundManager.singleton.PlaySFX("esqueletoAtaque");

                    Debug.Log(
                        $"El esqueleto ataco la celda {currentTargetCell}.",
                        this
                    );

                    playerHealth.Damage(HitFunction());
                }
            }

            // Esperamos el tiempo necesario para que la animación de ataque termine
            // antes de permitir que el esqueleto haga otra acción.
            yield return new WaitForSeconds(attackDuration);

            isPreparingAttack = false;
        }
    }
}