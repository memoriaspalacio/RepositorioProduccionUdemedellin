using System.Collections;
using UnityEngine;

namespace UdeM.Characters
{
    public class MonkCrossBehaviour : MonoBehaviour
    {
        // Representa los posibles estados de una cruz.
        private enum CrossState
        {
            Possessed,
            Unpossessed,
            Dead
        }

        // Guarda el Monk propietario de esta cruz.
        private MonkBehaviour owner;

        // Guarda la cuadricula utilizada para posicionar y registrar la cruz.
        private MeshGrid grid;

        // Guarda el indice original asignado por el Monk.
        private int crossIndex;

        // Guarda el estado actual de la cruz.
        private CrossState currentState = CrossState.Possessed;

        // Guarda la celda actualmente ocupada cuando la cruz esta fuera del Monk.
        private Vector2Int currentCell;

        // Indica si la cruz tiene actualmente una celda registrada.
        private bool cellRegistered = false;

        // Indica si la cruz se encuentra ejecutando un ataque.
        private bool isAttacking = false;

        // Indica si esta carga ya impacto al jugador.
        private bool playerHitThisAttack = false;

        // Define el dano realizado cuando la cruz impacta al jugador.
        [Min(0f)]
        [SerializeField] private float attackDamage = 4f;

        // Define cuanto tarda aproximadamente la cruz en recorrer una celda.
        [Min(0.01f)]
        [SerializeField] private float travelTimePerCell = 0.12f;

        // Define la altura visual de la cruz cuando queda clavada en el suelo.
        [SerializeField] private float groundHeightOffset = 0f;

        // Define la velocidad usada para rotar mientras la cruz esta poseida.
        [SerializeField] private float possessedRotationSpeed = 90f;

        // Guarda la referencia al sistema de vida del jugador.
        [SerializeField] private PlayerHealth playerHealth;

        // Expone si la cruz se encuentra actualmente en posesion del Monk.
        public bool IsPossessed =>
            currentState == CrossState.Possessed;

        // Expone si la cruz se encuentra muerta.
        public bool IsDead =>
            currentState == CrossState.Dead;

        // Expone si la cruz esta realizando actualmente un ataque.
        public bool IsAttacking =>
            isAttacking;

        // Expone la celda actualmente ocupada por la cruz.
        public Vector2Int CurrentCell =>
            currentCell;

        // Configura las referencias iniciales y coloca la cruz bajo el Monk.
        public void Initialize(
            MonkBehaviour monk,
            MeshGrid meshGrid,
            int index)
        {
            owner = monk;
            grid = meshGrid;
            crossIndex = index;

            currentState =
                CrossState.Possessed;

            transform.SetParent(
                monk.transform,
                true
            );
        }

        // Actualiza la posicion y rotacion visual mientras la cruz esta poseida.
        public void UpdatePossessedPosition(
            Vector3 position)
        {
            if (!IsPossessed)
                return;

            transform.position =
                position;

            transform.Rotate(
                Vector3.up,
                possessedRotationSpeed *
                Time.deltaTime,
                Space.World
            );
        }

        // Registra la posicion actual del jugador y lanza la cruz en linea recta.
        public IEnumerator AttackPlayer(
            GameObject player)
        {
            if (IsDead)
                yield break;

            if (isAttacking)
                yield break;

            if (player == null)
                yield break;

            isAttacking = true;
            playerHitThisAttack = false;

            Vector2Int targetCell =
                grid.WorldToCell(
                    player.transform.position
                );

            if (playerHealth == null)
            {
                playerHealth =
                    player.GetComponent<PlayerHealth>();
            }

            if (IsPossessed)
            {
                currentState =
                    CrossState.Unpossessed;

                transform.SetParent(
                    null,
                    true
                );
            }

            ReleaseOccupiedCell();

            Vector3 startPosition =
                transform.position;

            Vector3 targetPosition =
                grid.CellToWorldCenter(
                    targetCell,
                    groundHeightOffset
                );

            Vector3 direction =
                targetPosition -
                startPosition;

            direction.y = 0f;

            if (direction.sqrMagnitude > 0.001f)
            {
                transform.rotation =
                    Quaternion.LookRotation(
                        direction.normalized,
                        Vector3.up
                    );
            }

            float distance =
                Vector3.Distance(
                    startPosition,
                    targetPosition
                );

            float cellDistance =
                distance /
                Mathf.Max(
                    grid.CellSize,
                    0.01f
                );

            float travelDuration =
                Mathf.Max(
                    travelTimePerCell,
                    cellDistance *
                    travelTimePerCell
                );

            float elapsedTime =
                0f;

            while (elapsedTime <
                   travelDuration)
            {
                if (IsDead)
                    yield break;

                elapsedTime +=
                    Time.deltaTime;

                float progress =
                    Mathf.Clamp01(
                        elapsedTime /
                        travelDuration
                    );

                transform.position =
                    Vector3.Lerp(
                        startPosition,
                        targetPosition,
                        progress
                    );

                CheckPlayerImpact(
                    player
                );

                yield return null;
            }

            transform.position =
                targetPosition;

            CheckPlayerImpact(
                player
            );

            RegisterCurrentCell(
                targetCell
            );

            isAttacking = false;
        }

        // Comprueba mediante las celdas si la cruz impacto al jugador durante su recorrido.
        private void CheckPlayerImpact(
            GameObject player)
        {
            if (playerHitThisAttack)
                return;

            if (player == null)
                return;

            Vector2Int crossCell =
                grid.WorldToCell(
                    transform.position
                );

            Vector2Int playerCell =
                grid.WorldToCell(
                    player.transform.position
                );

            if (crossCell != playerCell)
                return;

            playerHitThisAttack =
                true;

            if (playerHealth != null)
            {
                playerHealth.Damage(
                    attackDamage
                );
            }

            Debug.Log(
                $"La cruz {crossIndex} impacto al jugador en {playerCell}.",
                this
            );
        }

        // Registra la celda donde la cruz queda clavada despues del ataque.
        private void RegisterCurrentCell(
            Vector2Int cell)
        {
            currentCell =
                cell;

            cellRegistered =
                grid.TryOccupyCell(
                    currentCell,
                    gameObject
                );

            if (!cellRegistered)
            {
                Debug.LogWarning(
                    $"La cruz {crossIndex} no pudo registrar la celda {cell}.",
                    this
                );
            }
        }

        // Libera la celda actualmente registrada por esta cruz.
        public void ReleaseOccupiedCell()
        {
            if (!cellRegistered)
                return;

            if (grid == null)
                return;

            grid.ReleaseCell(
                currentCell,
                gameObject
            );

            cellRegistered =
                false;
        }

        // Devuelve la cruz viva al estado de posesion del Monk.
        public void ReturnToPossession()
        {
            if (IsDead)
                return;

            CancelCurrentAttack();

            ReleaseOccupiedCell();

            currentState =
                CrossState.Possessed;

            if (owner != null)
            {
                transform.SetParent(
                    owner.transform,
                    true
                );
            }
        }

        // Cancela inmediatamente cualquier desplazamiento ejecutado por la cruz.
        public void CancelCurrentAttack()
        {
            StopAllCoroutines();

            isAttacking =
                false;

            playerHitThisAttack =
                false;
        }

        // Marca la cruz como muerta y notifica al Monk para recuperar las restantes.
        public void SetDead()
        {
            if (IsDead)
                return;

            CancelCurrentAttack();

            ReleaseOccupiedCell();

            currentState =
                CrossState.Dead;

            if (owner != null)
            {
                owner.NotifyCrossDied(
                    this
                );
            }

            gameObject.SetActive(
                false
            );
        }

        // Libera cualquier celda registrada cuando la cruz es destruida.
        private void OnDestroy()
        {
            ReleaseOccupiedCell();
        }
    }
}