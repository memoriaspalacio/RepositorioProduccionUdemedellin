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

        // Tiempo que el esqueleto apunta a la celda del jugador antes de lanzar el golpe.
        // Es el aviso: si el jugador sale de esa celda en este tiempo, esquiva el ataque.
        [Tooltip("Segundos que el esqueleto apunta a la celda del jugador antes de golpear. " +
                 "Si el jugador sale de esa celda en este tiempo, esquiva el golpe.")]
        [Min(0f)]
        [SerializeField] private float preparationTime = 0.5f;

        // Momento de la animacion de ataque en que la espada toca: ahi, y solo ahi, entra el dano.
        [Tooltip("Segundos desde que empieza la animacion de ataque hasta que la espada toca. " +
                 "El dano se aplica en ese instante, no antes.")]
        [Min(0f)]
        [SerializeField] private float hitDelay = 0.5f;

        // Define la recarga DESPUES del golpe; mientras recarga no puede volver a atacar ni hace dano.
        [Tooltip("Segundos de recarga despues del golpe (animacion de recarga). Mientras recarga " +
                 "no puede volver a atacar y no hace dano.")]
        [SerializeField] private float reloadTime = 0.5f;

        // Define el tiempo que dura la animación de ataque antes de poder actuar de nuevo.
        [Tooltip("Duracion de la animacion de ataque antes de pasar a la recarga.")]
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

        // Apunta a la celda del jugador, golpea y despues recarga. Antes la recarga iba
        // primero y el dano entraba justo al terminarla, cuando la animacion de ataque
        // apenas empezaba (y la siguiente recarga la cortaba), asi que el golpe se sentia
        // como dano de la recarga. Ahora el dano solo entra en el impacto y solo si el
        // jugador sigue en la celda apuntada.
        private IEnumerator PrepareAndAttack(Vector2Int direction)
        {
            if (isDead)
                yield break;

            isPreparingAttack = true;

            // La celda queda fijada al empezar: moverse durante la preparacion esquiva el golpe
            Vector2Int attackedCell =
                CurrentGridCell + direction;

            FaceGridDirection(direction);

            // Quieto mientras apunta y golpea, para que el golpe caiga donde se aviso
            StopMovementFor(
                preparationTime + Mathf.Max(attackDuration, hitDelay)
            );

            yield return new WaitForSeconds(preparationTime);

            if (animator != null)
                animator.SetTrigger(attackTrigger);

            SoundManager.singleton.PlaySFX("esqueletoAtaque");

            yield return new WaitForSeconds(hitDelay);

            if (CurrentTarget != null &&
                Grid.WorldToCell(CurrentTarget.transform.position) == attackedCell)
            {
                Debug.Log(
                    $"El esqueleto ataco la celda {attackedCell}.",
                    this
                );

                if (playerHealth != null)
                    playerHealth.Damage(HitFunction(), transform.position);
            }

            // Deja terminar la animacion de ataque antes de recargar
            yield return new WaitForSeconds(
                Mathf.Max(0f, attackDuration - hitDelay)
            );

            if (animator != null)
                animator.SetTrigger(cooldownTrigger);

            // Recarga: no puede volver a atacar hasta que termine
            yield return new WaitForSeconds(reloadTime);

            isPreparingAttack = false;
        }
    }
}