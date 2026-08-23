using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace UdeM.Characters
{
    public class Character3DNavMeshGridNPCBehaviour
        : Character3DNavMeshBehaviour
    {
        protected bool behaviourEnabled = true;

        [Header("Cuadricula")]
        [SerializeField] private MeshGrid grid;

        [Header("Patrulla")]
        [SerializeField] private List<Transform> patrolPoints;
        [SerializeField] private float patrolPointWaitTime = 2f;

        [Header("Movimiento por celdas")]
        [Min(1)]
        [SerializeField] private int minimumCellsBeforePause = 1;

        [Min(1)]
        [SerializeField] private int maximumCellsBeforePause = 3;

        [Min(0f)]
        [SerializeField] private float pauseDuration = 1f;

        [Min(0f)]
        [SerializeField] private float timeBetweenCells = 0.1f;

        [Header("Ritmo")]
        [SerializeField] private bool requireRhythm = true;
        [SerializeField] private bool rhythmWindowOpen = false;

        [Header("NavMesh")]
        [Min(0.1f)]
        [SerializeField] private float navMeshSampleDistance = 2f;

        [Header("Estado")]
        [SerializeField] private Vector2Int currentCell;
        [SerializeField] private bool movementConsumedThisWindow;
        [SerializeField] private int movedCells;
        [SerializeField] private int cellsBeforePause;

        private GameObject visionObject;
        private GameObject target;

        private int patrolIndex;
        private float nextMovementTime;
        private float nextPatrolTime;

        private int actionState;
        private bool cellRegistered;

        private const int PATROLLING = 2;
        private const int ATTACKING = 3;

        public Vector2Int CurrentCell => currentCell;
        public bool RhythmWindowOpen => rhythmWindowOpen;
        public bool IsPatrolling => actionState == PATROLLING;
        public bool IsAttacking => actionState == ATTACKING;

        protected MeshGrid Grid => grid;
        protected GameObject CurrentTarget => target;
        protected Vector2Int CurrentGridCell => currentCell;

        private bool movementStopped;
        private float movementStopUntil;

        protected override void Awake()
        {
            base.Awake();

            actionState = PATROLLING;
            patrolIndex = 0;
            movedCells = 0;

            SelectNewCellsBeforePause();
        }

        protected override void Start()
        {
            base.Start();

            ValidateValues();

            if (grid == null)
                grid = FindFirstObjectByType<MeshGrid>();

            if (grid == null)
            {
                Debug.LogError(
                    "No se encontr� un objeto con MeshGrid.",
                    this
                );

                enabled = false;
                return;
            }

            if (_navigator == null)
            {
                Debug.LogError(
                    "No se encontr� un NavMeshAgent.",
                    this
                );

                enabled = false;
                return;
            }

            currentCell = grid.WorldToCell(
                transform.position
            );

            if (!grid.TryOccupyCell(
                    currentCell,
                    gameObject))
            {
                Debug.LogError(
                    $"La celda inicial {currentCell} " +
                    "ya est� ocupada.",
                    this
                );

                enabled = false;
                return;
            }

            cellRegistered = true;

            ConfigureNavigator();
            ConfigureVision();

            nextMovementTime = Time.time;
            nextPatrolTime = Time.time;

            if (!requireRhythm)
                TryPerformGridAction();
        }

        protected override void Update()
        {
            base.Update();
            if (!behaviourEnabled)
                return;

            if (!requireRhythm)
                TryPerformGridAction();
        }

        

        public void StopMovementFor(float seconds)
        {
            movementStopped = true;
            movementStopUntil = Mathf.Max(
                movementStopUntil,
                Time.time + seconds
            );

            if (_navigator != null &&
                _navigator.isOnNavMesh)
            {
                _navigator.ResetPath();
            }

            _state = STANDBY;
        }

        protected override void OnFinishMove()
        {
            base.OnFinishMove();
        }

        protected virtual void OnDestroy()
        {
            ReleaseCurrentCell();
        }

        private void OnDisable()
        {
            if (!gameObject.scene.isLoaded)
                return;

            ReleaseCurrentCell();
        }

        private void ConfigureNavigator()
        {
            _navigator.isStopped = true;
            _navigator.updatePosition = true;
            _navigator.updateRotation = true;
            _navigator.autoBraking = true;

            if (_navigator.isOnNavMesh)
                _navigator.ResetPath();

            _state = STANDBY;
        }

        private void ConfigureVision()
        {
            Transform visionTransform =
                transform.Find("Vision");

            if (visionTransform == null)
                return;

            visionObject = visionTransform.gameObject;

            GridVisionBehaviour vision =
                visionObject.GetComponent<GridVisionBehaviour>();

            if (vision == null)
            {
                vision =
                    visionObject.AddComponent<GridVisionBehaviour>();
            }

            vision.Initialize(this);
        }

        private void ValidateValues()
        {
            minimumCellsBeforePause =
                Mathf.Max(
                    1,
                    minimumCellsBeforePause
                );

            maximumCellsBeforePause =
                Mathf.Max(
                    minimumCellsBeforePause,
                    maximumCellsBeforePause
                );

            pauseDuration =
                Mathf.Max(0f, pauseDuration);

            timeBetweenCells =
                Mathf.Max(0f, timeBetweenCells);

            navMeshSampleDistance =
                Mathf.Max(
                    0.1f,
                    navMeshSampleDistance
                );
        }

        private void TryPerformGridAction()
        {
            if (!behaviourEnabled)
                return;

            if (!CanPerformGridAction())
                return;

            Vector2Int destinationCell;

            if (actionState == ATTACKING &&
                target != null)
            {
                destinationCell =
                    grid.WorldToCell(
                        target.transform.position
                    );

                if (TryAttackAdjacentTarget(
                        destinationCell))
                {
                    return;
                }
            }
            else
            {
                actionState = PATROLLING;

                if (!TryGetPatrolDestination(
                        out destinationCell))
                {
                    return;
                }
            }

            if (destinationCell == currentCell)
            {
                HandleDestinationReached();
                return;
            }

            TryMoveOneCellTowards(
                destinationCell
            );
        }

        private bool TryAttackAdjacentTarget(
            Vector2Int targetCell)
        {
            Vector2Int difference =
                targetCell - currentCell;

            int cellDistance =
                Mathf.Abs(difference.x) +
                Mathf.Abs(difference.y);

            if (cellDistance != 1)
                return false;

            Vector2Int attackDirection =
                new Vector2Int(
                    System.Math.Sign(difference.x),
                    System.Math.Sign(difference.y)
                );

            FaceGridDirection(
                attackDirection
            );

            if (!Attack(
                    attackDirection,
                    targetCell))
            {
                return false;
            }

            if (requireRhythm)
                movementConsumedThisWindow = true;

            nextMovementTime =
                Time.time + timeBetweenCells;

            return true;
        }

        protected virtual bool Attack(
            Vector2Int direction,
            Vector2Int targetCell)
        {
            
            return false;
        }

        protected void FaceGridDirection(
            Vector2Int direction)
        {
            Vector3 worldDirection =
                new Vector3(
                    direction.x,
                    0f,
                    direction.y
                );

            if (worldDirection == Vector3.zero)
                return;

            transform.rotation =
                Quaternion.LookRotation(
                    worldDirection,
                    Vector3.up
                );
        }

        private bool CanPerformGridAction()
        {
            // Bloqueo temporal completo del movimiento
            if (movementStopped)
            {
                if (Time.time < movementStopUntil)
                    return false;

                movementStopped = false;
            }

            if (grid == null ||
                _navigator == null)
            {
                return false;
            }

            if (!cellRegistered)
                return false;

            if (!_canMove)
                return false;

            if (Time.time < nextMovementTime)
                return false;

            if (actionState == PATROLLING &&
                Time.time < nextPatrolTime)
            {
                return false;
            }

            if (!requireRhythm)
                return true;

            return rhythmWindowOpen &&
                !movementConsumedThisWindow;
        }

        private bool TryGetPatrolDestination(
            out Vector2Int destinationCell)
        {
            destinationCell = currentCell;

            if (patrolPoints == null ||
                patrolPoints.Count == 0)
            {
                return false;
            }

            if (patrolIndex < 0 ||
                patrolIndex >= patrolPoints.Count)
            {
                patrolIndex = 0;
            }

            Transform patrolPoint =
                patrolPoints[patrolIndex];

            if (patrolPoint == null)
            {
                AdvancePatrolPoint();
                return false;
            }

            destinationCell =
                grid.WorldToCell(
                    patrolPoint.position
                );

            return true;
        }

        private bool TryMoveOneCellTowards(
            Vector2Int destinationCell)
        {
            Vector2Int difference =
                destinationCell - currentCell;

            CalculateDirections(
                difference,
                out Vector2Int primaryDirection,
                out Vector2Int secondaryDirection
            );

            if (TryMoveInDirection(
                    primaryDirection))
            {
                return true;
            }

            return TryMoveInDirection(
                secondaryDirection
            );
        }

        private void CalculateDirections(
            Vector2Int difference,
            out Vector2Int primaryDirection,
            out Vector2Int secondaryDirection)
        {
            int horizontalDirection =
                System.Math.Sign(difference.x);

            int verticalDirection =
                System.Math.Sign(difference.y);

            if (Mathf.Abs(difference.x) >=
                Mathf.Abs(difference.y))
            {
                primaryDirection =
                    new Vector2Int(
                        horizontalDirection,
                        0
                    );

                secondaryDirection =
                    new Vector2Int(
                        0,
                        verticalDirection
                    );
            }
            else
            {
                primaryDirection =
                    new Vector2Int(
                        0,
                        verticalDirection
                    );

                secondaryDirection =
                    new Vector2Int(
                        horizontalDirection,
                        0
                    );
            }
        }

        private bool TryMoveInDirection(
            Vector2Int direction)
        {
            if (direction == Vector2Int.zero)
                return false;

            if (!grid.TryGetNeighbour(
                    currentCell,
                    direction,
                    out Vector2Int nextCell))
            {
                return false;
            }

            if (nextCell == currentCell)
                return false;

            if (grid.IsCellOccupiedByOther(
                    nextCell,
                    gameObject))
            {
                OnCellBlocked(nextCell);
                return false;
            }

            if (!TryGetNavMeshPosition(
                    nextCell,
                    out Vector3 destination))
            {
                return false;
            }

            if (!_navigator.isOnNavMesh)
                return false;

            Vector2Int previousCell =
                currentCell;

            if (!grid.TryMoveOccupant(
                    previousCell,
                    nextCell,
                    gameObject))
            {
                OnCellBlocked(nextCell);
                return false;
            }

            _navigator.ResetPath();

            bool warpSucceeded =
                _navigator.Warp(destination);

            if (!warpSucceeded)
            {
                bool rollbackSucceeded =
                    grid.TryMoveOccupant(
                        nextCell,
                        previousCell,
                        gameObject
                    );

                if (!rollbackSucceeded)
                {
                    Debug.LogError(
                        "No se pudo restaurar la celda " +
                        "del enemigo despu�s de fallar Warp.",
                        this
                    );

                    cellRegistered = false;
                }

                return false;
            }

            currentCell = nextCell;
            _state = STANDBY;

            if (requireRhythm)
            {
                movementConsumedThisWindow =
                    true;
            }

            RegisterCellMovement();
            HandleDestinationReached();

            return true;
        }

        private bool TryGetNavMeshPosition(
            Vector2Int cell,
            out Vector3 navMeshPosition)
        {
            Vector3 cellPosition =
                grid.CellToWorldCenter(
                    cell,
                    0f
                );

            Vector3 sampleOrigin =
                new Vector3(
                    cellPosition.x,
                    transform.position.y,
                    cellPosition.z
                );

            if (!NavMesh.SamplePosition(
                    sampleOrigin,
                    out NavMeshHit hit,
                    navMeshSampleDistance,
                    NavMesh.AllAreas))
            {
                navMeshPosition =
                    Vector3.zero;

                return false;
            }

            Vector2Int sampledCell =
                grid.WorldToCell(
                    hit.position
                );

            if (sampledCell != cell)
            {
                navMeshPosition =
                    Vector3.zero;

                return false;
            }

            navMeshPosition =
                hit.position;

            return true;
        }

        private void RegisterCellMovement()
        {
            movedCells++;

            if (movedCells >=
                cellsBeforePause)
            {
                movedCells = 0;

                nextMovementTime =
                    Time.time + pauseDuration;

                SelectNewCellsBeforePause();
            }
            else
            {
                nextMovementTime =
                    Time.time + timeBetweenCells;
            }
        }

        private void SelectNewCellsBeforePause()
        {
            cellsBeforePause =
                Random.Range(
                    minimumCellsBeforePause,
                    maximumCellsBeforePause + 1
                );
        }

        private void HandleDestinationReached()
        {
            if (actionState == ATTACKING &&
                target != null)
            {
                Vector2Int targetCell =
                    grid.WorldToCell(
                        target.transform.position
                    );

                if (currentCell == targetCell)
                    OnPlayerCellReached();

                return;
            }

            if (actionState != PATROLLING)
                return;

            CheckPatrolPointReached();
        }

        private void CheckPatrolPointReached()
        {
            if (patrolPoints == null ||
                patrolPoints.Count == 0)
            {
                return;
            }

            Transform patrolPoint =
                patrolPoints[patrolIndex];

            if (patrolPoint == null)
            {
                AdvancePatrolPoint();
                return;
            }

            Vector2Int patrolCell =
                grid.WorldToCell(
                    patrolPoint.position
                );

            if (currentCell != patrolCell)
                return;

            AdvancePatrolPoint();

            nextPatrolTime =
                Time.time +
                patrolPointWaitTime;

            nextMovementTime =
                Mathf.Max(
                    nextMovementTime,
                    nextPatrolTime
                );
        }

        private void AdvancePatrolPoint()
        {
            if (patrolPoints == null ||
                patrolPoints.Count == 0)
            {
                patrolIndex = 0;
                return;
            }

            patrolIndex++;

            if (patrolIndex >=
                patrolPoints.Count)
            {
                patrolIndex = 0;
            }
        }

        private void ReleaseCurrentCell()
        {
            if (!cellRegistered ||
                grid == null)
            {
                return;
            }

            grid.ReleaseCell(
                currentCell,
                gameObject
            );

            cellRegistered = false;
        }

        protected virtual void OnCellBlocked(
            Vector2Int blockedCell)
        {
            Debug.Log(
                $"El enemigo no puede entrar en " +
                $"{blockedCell}: la celda est� ocupada.",
                this
            );
        }

        protected virtual void OnPlayerCellReached()
        {
            Debug.Log(
                $"El enemigo alcanz� la celda " +
                $"del jugador: {currentCell}.",
                this
            );
        }

        public void SetRhythmWindow(
            bool isOpen)
        {
            rhythmWindowOpen = isOpen;

            if (!isOpen)
                return;

            movementConsumedThisWindow =
                false;

            TryPerformGridAction();
        }

        public void SetRequireRhythm(
            bool value)
        {
            requireRhythm = value;

            movementConsumedThisWindow =
                false;

            if (!requireRhythm)
                TryPerformGridAction();
        }

        public virtual void PlayerDetected(
            GameObject detectedTarget)
        {
            if (!behaviourEnabled)
                return;
            if (detectedTarget == null)
                return;

            target = detectedTarget;
            actionState = ATTACKING;

            TryPerformGridAction();
        }

        public virtual void PlayerLost(
            GameObject lostTarget)
        {
            if (target != lostTarget)
                return;

            target = null;
            actionState = PATROLLING;

            nextPatrolTime = Time.time;

            if (_navigator != null &&
                _navigator.isOnNavMesh)
            {
                _navigator.ResetPath();
            }

            _state = STANDBY;

            TryPerformGridAction();
        }

        

        private class GridVisionBehaviour
            : MonoBehaviour
        {
            private Character3DNavMeshGridNPCBehaviour
                owner;

            public void Initialize(
                Character3DNavMeshGridNPCBehaviour npc)
            {
                owner = npc;
            }

            private void OnTriggerEnter(
                Collider other)
            {
                if (owner == null)
                    return;

                if (other.CompareTag("Player"))
                {
                    owner.PlayerDetected(
                        other.gameObject
                    );
                }
            }

            private void OnTriggerStay(
                Collider other)
            {
                if (owner == null)
                    return;

                if (other.CompareTag("Player"))
                {
                    owner.PlayerDetected(
                        other.gameObject
                    );
                }
            }

            private void OnTriggerExit(
                Collider other)
            {
                if (owner == null)
                    return;

                if (other.CompareTag("Player"))
                {
                    owner.PlayerLost(
                        other.gameObject
                    );
                }
            }
        }
    }
}