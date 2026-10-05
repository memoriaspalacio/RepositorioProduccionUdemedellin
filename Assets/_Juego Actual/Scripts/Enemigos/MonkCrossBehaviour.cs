using System.Collections;
using System.Collections.Generic;
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

        // Guarda la cuadricula utilizada para posicionar y mover la cruz.
        private MeshGrid grid;

        // Guarda el indice original asignado por el Monk.
        private int crossIndex;

        // Guarda el estado actual de la cruz.
        private CrossState currentState = CrossState.Possessed;

        // Guarda la celda logica actual de la cruz.
        private Vector2Int currentCell;

        // Indica si la cruz tiene actualmente una celda registrada en la cuadricula.
        private bool cellRegistered = false;

        // Indica si la cruz se encuentra ejecutando un ataque.
        private bool isAttacking = false;

        // Indica si esta carga ya impacto al jugador.
        private bool playerHitThisAttack = false;

        // Guarda la ultima celda del jugador registrada al comenzar el ataque.
        private Vector2Int recordedPlayerCell;

        // Define el dano realizado cuando la cruz impacta al jugador.
        [Min(0f)]
        [SerializeField] private float attackDamage = 4f;

        // Define cuanto tarda la cruz en desplazarse visualmente entre dos celdas.
        [Min(0.01f)]
        [SerializeField] private float travelTimePerCell = 0.12f;

        // Define cuanto se eleva visualmente la cruz respecto a la altura del Monk.
        [SerializeField] private float unpossessedHeightOffset = 0.5f;

        // Define la velocidad usada para rotar mientras la cruz esta poseida.
        [SerializeField] private float possessedRotationSpeed = 90f;

        // Guarda la referencia al sistema de vida del jugador.
        [SerializeField] private PlayerHealth playerHealth;

        // Expone si la cruz se encuentra actualmente en posesion del Monk.
        public bool IsPossessed =>
            currentState == CrossState.Possessed;

        // Expone si la cruz se encuentra fuera de posesion del Monk.
        public bool IsUnpossessed =>
            currentState == CrossState.Unpossessed;

        // Expone si la cruz se encuentra muerta.
        public bool IsDead =>
            currentState == CrossState.Dead;

        // Expone si la cruz esta realizando actualmente un ataque.
        public bool IsAttacking =>
            isAttacking;

        // Expone la celda logica actual de la cruz.
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

            currentCell =
                grid.WorldToCell(
                    transform.position
                );

            transform.SetParent(
                monk.transform,
                true
            );
        }

        // Actualiza la posicion y celda logica mientras la cruz permanece poseida.
        public void UpdatePossessedPosition(
            Vector3 position)
        {
            if (!IsPossessed)
                return;

            if (grid == null)
                return;

            transform.position =
                position;

            currentCell =
                grid.WorldToCell(
                    transform.position
                );

            transform.Rotate(
                Vector3.up,
                possessedRotationSpeed *
                Time.deltaTime,
                Space.World
            );
        }

        // Registra al jugador al comenzar el turno y lanza la cruz por celdas en linea recta.
        public IEnumerator AttackPlayer(
            GameObject player)
        {
            if (IsDead)
                yield break;

            if (isAttacking)
                yield break;

            if (player == null)
                yield break;

            if (grid == null)
                yield break;

            isAttacking = true;
            playerHitThisAttack = false;

            recordedPlayerCell =
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
                bool released =
                    LeavePossession();

                if (!released)
                {
                    isAttacking = false;
                    yield break;
                }
            }

            List<Vector2Int> attackPath =
                BuildStraightCellPath(
                    currentCell,
                    recordedPlayerCell
                );

            if (attackPath.Count <= 1)
            {
                CheckPlayerImpact(
                    player
                );

                SnapToCurrentCell();

                isAttacking = false;
                yield break;
            }

            for (int i = 1;
                 i < attackPath.Count;
                 i++)
            {
                if (IsDead)
                    yield break;

                Vector2Int nextCell =
                    attackPath[i];

                bool canContinue =
                    CanEnterAttackCell(
                        nextCell,
                        player
                    );

                if (!canContinue)
                    break;

                bool moved =
                    grid.TryMoveOccupant(
                        currentCell,
                        nextCell,
                        gameObject
                    );

                if (!moved)
                {
                    Debug.Log(
                        $"La cruz {crossIndex} no pudo avanzar hacia {nextCell}.",
                        this
                    );

                    break;
                }

                Vector2Int previousCell =
                    currentCell;

                currentCell =
                    nextCell;

                yield return StartCoroutine(
                    MoveVisualBetweenCells(
                        previousCell,
                        currentCell,
                        player
                    )
                );

                CheckPlayerImpact(
                    player
                );

                if (playerHitThisAttack)
                {
                    if (IsPlayerInCell(
                            player,
                            currentCell))
                    {
                        break;
                    }
                }
            }

            SnapToCurrentCell();

            isAttacking = false;
        }

        // Cambia la cruz a no poseida e intenta registrar su celda actual.
        private bool LeavePossession()
        {
            if (!IsPossessed)
                return true;

            currentCell =
                grid.WorldToCell(
                    transform.position
                );

            if (!grid.IsCellInside(
                    currentCell))
            {
                return false;
            }

            if (grid.IsCellOccupiedByOther(
                    currentCell,
                    gameObject))
            {
                Debug.LogWarning(
                    $"La cruz {crossIndex} no puede salir porque {currentCell} esta ocupada.",
                    this
                );

                return false;
            }

            bool occupied =
                grid.TryOccupyCell(
                    currentCell,
                    gameObject
                );

            if (!occupied)
                return false;

            cellRegistered = true;

            currentState =
                CrossState.Unpossessed;

            transform.SetParent(
                null,
                true
            );

            return true;
        }

        // Comprueba si la cruz puede entrar en la siguiente celda durante su ataque.
        private bool CanEnterAttackCell(
            Vector2Int nextCell,
            GameObject player)
        {
            if (!grid.IsCellInside(
                    nextCell))
            {
                return false;
            }

            GameObject occupant =
                grid.GetCellOccupant(
                    nextCell
                );

            if (occupant == null)
                return true;

            if (occupant == gameObject)
                return true;

            if (player != null &&
                occupant == player)
            {
                ApplyPlayerImpact(
                    player,
                    nextCell
                );

                return false;
            }

            Debug.Log(
                $"La cruz {crossIndex} fue bloqueada en {nextCell} por {occupant.name}.",
                this
            );

            return false;
        }

        // Interpola visualmente la cruz entre dos celdas manteniendo una altura estable.
        private IEnumerator MoveVisualBetweenCells(
            Vector2Int previousCell,
            Vector2Int nextCell,
            GameObject player)
        {
            Vector3 startPosition =
                transform.position;

            Vector3 targetPosition =
                GetUnpossessedWorldPosition(
                    nextCell
                );

            Vector3 direction =
                targetPosition -
                startPosition;

            direction.y = 0f;

            if (direction.sqrMagnitude >
                0.001f)
            {
                transform.rotation =
                    Quaternion.LookRotation(
                        direction.normalized,
                        Vector3.up
                    );
            }

            float elapsedTime =
                0f;

            while (elapsedTime <
                   travelTimePerCell)
            {
                if (IsDead)
                    yield break;

                elapsedTime +=
                    Time.deltaTime;

                float progress =
                    Mathf.Clamp01(
                        elapsedTime /
                        travelTimePerCell
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
        }

        // Obtiene una posicion estable usando la celda para XZ y el Monk como referencia de altura.
        private Vector3 GetUnpossessedWorldPosition(
            Vector2Int cell)
        {
            Vector3 position =
                grid.CellToWorldCenter(
                    cell,
                    0f
                );

            if (owner != null)
            {
                position.y =
                    owner.transform.position.y +
                    unpossessedHeightOffset;
            }
            else
            {
                position.y =
                    unpossessedHeightOffset;
            }

            return position;
        }

        // Construye una linea discreta de celdas desde el origen hasta el objetivo.
        private List<Vector2Int> BuildStraightCellPath(
            Vector2Int startCell,
            Vector2Int targetCell)
        {
            List<Vector2Int> path =
                new List<Vector2Int>();

            int x0 =
                startCell.x;

            int y0 =
                startCell.y;

            int x1 =
                targetCell.x;

            int y1 =
                targetCell.y;

            int deltaX =
                Mathf.Abs(
                    x1 - x0
                );

            int deltaY =
                Mathf.Abs(
                    y1 - y0
                );

            int stepX =
                x0 < x1
                    ? 1
                    : -1;

            int stepY =
                y0 < y1
                    ? 1
                    : -1;

            int error =
                deltaX -
                deltaY;

            while (true)
            {
                Vector2Int cell =
                    new Vector2Int(
                        x0,
                        y0
                    );

                if (grid.IsCellInside(
                        cell))
                {
                    path.Add(
                        cell
                    );
                }

                if (x0 == x1 &&
                    y0 == y1)
                {
                    break;
                }

                int doubleError =
                    error * 2;

                if (doubleError >
                    -deltaY)
                {
                    error -=
                        deltaY;

                    x0 +=
                        stepX;
                }

                if (doubleError <
                    deltaX)
                {
                    error +=
                        deltaX;

                    y0 +=
                        stepY;
                }
            }

            return path;
        }

        // Comprueba mediante celdas si la cruz coincide actualmente con el jugador.
        private void CheckPlayerImpact(
            GameObject player)
        {
            if (playerHitThisAttack)
                return;

            if (player == null)
                return;

            Vector2Int playerCell =
                grid.WorldToCell(
                    player.transform.position
                );

            Vector2Int visualCrossCell =
                grid.WorldToCell(
                    transform.position
                );

            if (visualCrossCell != playerCell)
                return;

            ApplyPlayerImpact(
                player,
                playerCell
            );
        }

        // Aplica una unica vez el dano correspondiente al impacto de la carga actual.
        private void ApplyPlayerImpact(
            GameObject player,
            Vector2Int impactCell)
        {
            if (playerHitThisAttack)
                return;

            if (player == null)
                return;

            playerHitThisAttack =
                true;

            if (playerHealth == null)
            {
                playerHealth =
                    player.GetComponent<PlayerHealth>();
            }

            if (playerHealth != null)
            {
                playerHealth.Damage(
                    attackDamage
                );
            }

            Debug.Log(
                $"La cruz {crossIndex} impacto al jugador en {impactCell}.",
                this
            );
        }

        // Comprueba si el jugador ocupa actualmente la celda indicada.
        private bool IsPlayerInCell(
            GameObject player,
            Vector2Int cell)
        {
            if (player == null)
                return false;

            Vector2Int playerCell =
                grid.WorldToCell(
                    player.transform.position
                );

            return playerCell ==
                   cell;
        }

        // Ajusta visualmente la cruz al centro de su celda usando la altura configurada.
        private void SnapToCurrentCell()
        {
            if (!IsUnpossessed)
                return;

            if (!cellRegistered)
                return;

            transform.position =
                GetUnpossessedWorldPosition(
                    currentCell
                );
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

        // Devuelve una cruz viva al estado de posesion del Monk.
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

                currentCell =
                    grid.WorldToCell(
                        owner.transform.position
                    );
            }
        }

        // Cancela inmediatamente cualquier desplazamiento ejecutado por esta cruz.
        public void CancelCurrentAttack()
        {
            StopAllCoroutines();

            isAttacking =
                false;

            playerHitThisAttack =
                false;

            if (IsUnpossessed)
                SnapToCurrentCell();
        }

        // Marca la cruz como muerta, libera su celda y ordena recuperar las restantes.
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

        // Libera cualquier ocupacion existente cuando el objeto es destruido.
        private void OnDestroy()
        {
            ReleaseOccupiedCell();
        }
    }
}