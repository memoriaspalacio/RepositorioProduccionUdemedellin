using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using System.Collections;

namespace UdeM.Characters
{
    public class Character3DNavMeshBehaviour : CharacterBehaviour
    {
        protected NavMeshAgent _navigator;
        protected UnityEvent _onStartMove;
        protected UnityEvent _onFinishMove;
        protected GameObject _target;

        [SerializeField] protected int _state;

        [Header("Movimiento por cuadrícula")]
        [SerializeField] protected MeshGrid _grid;

        [Tooltip("Prioriza el eje con mayor distancia al destino.")]
        [SerializeField] protected bool _prioritizeLongestAxis = true;

        protected const int STANDBY = 0;
        protected const int MOVING = 1;

        // Casilla final a la que quiere llegar el NPC.
        protected Vector2Int _gridTargetCell;

        // Casilla intermedia hacia la que está avanzando actualmente.
        protected Vector2Int _activeStepCell;

        // Indica si hay un recorrido por cuadrícula activo.
        protected bool _movingByGrid;

        [Header("Ritmo de desplazamiento")]
        [Min(0.01f)]
        [SerializeField] protected float _gridStepInterval = 0.5f;

        [SerializeField] protected bool _instantGridMovement = true;

        protected Coroutine _gridMovementCoroutine;

        protected override void Start()
        {
            base.Start();

            _onStartMove = new UnityEvent();
            _onFinishMove = new UnityEvent();

            _onFinishMove.AddListener(OnFinishMove);

            _navigator = GetComponent<NavMeshAgent>();

            if (_navigator == null)
            {
                _navigator = gameObject.AddComponent<NavMeshAgent>();
            }

            if (_grid == null)
            {
                _grid = FindFirstObjectByType<MeshGrid>();
            }

            if (_grid == null)
            {
                Debug.LogWarning(
                    "No se encontró MeshGrid. " +
                    "El personaje utilizará movimiento NavMesh normal.",
                    this
                );
            }
        }

        protected virtual void OnFinishMove()
        {
            _state = STANDBY;
        }

        protected override void Update()
        {
            base.Update();
            CheckMoveState();
        }

        protected void CheckMoveState()
        {
            if (_state != MOVING)
                return;

            if (_navigator.pathPending)
                return;

            if (_navigator.remainingDistance >
                _navigator.stoppingDistance)
            {
                return;
            }

            if (_navigator.hasPath)
                return;

            if (_navigator.velocity.sqrMagnitude > 0.01f)
                return;

            // El NavMeshAgent llegó a su destino actual.
            _state = STANDBY;

            if (_movingByGrid)
            {
                OnGridStepFinished();
            }
            else
            {
                _onFinishMove.Invoke();
            }
        }

        /// <summary>
        /// Se ejecuta cuando el NPC termina una casilla intermedia.
        /// </summary>
        protected virtual void OnGridStepFinished()
        {
            Vector2Int currentCell =
                _grid.WorldToCell(transform.position);

            // Si ya llegó a la casilla final, termina todo el recorrido.
            if (currentCell == _gridTargetCell)
            {
                FinishGridMovement();
                return;
            }

            // Todavía quedan casillas por recorrer.
            MoveToNextGridCell();
        }

        /// <summary>
        /// Termina el recorrido completo y avisa a las clases hijas.
        /// </summary>
        protected virtual void FinishGridMovement()
        {
            _movingByGrid = false;
            _state = STANDBY;

            if (_navigator != null &&
                _navigator.isOnNavMesh)
            {
                _navigator.ResetPath();
            }

            _onFinishMove.Invoke();
        }

        /// <summary>
        /// Recibe un destino del mundo y lo convierte en una casilla final.
        /// </summary>
        public void GoToDestination(Vector3 position)
        {
            if (_grid == null)
            {
                GoToNavMeshPosition(position);
                return;
            }

            _gridTargetCell = _grid.WorldToCell(position);

            Vector2Int currentCell =
                _grid.WorldToCell(transform.position);

            if (currentCell == _gridTargetCell)
                return;

            _movingByGrid = true;
            _state = MOVING;

            if (_gridMovementCoroutine == null)
            {
                _gridMovementCoroutine =
                    StartCoroutine(GridMovementRoutine());
            }
        }

        protected virtual IEnumerator GridMovementRoutine()
        {
            while (_movingByGrid)
            {
                Vector2Int currentCell =
                    _grid.WorldToCell(transform.position);

                if (currentCell == _gridTargetCell)
                {
                    FinishGridMovement();
                    break;
                }

                bool moved = MoveInstantlyToNextGridCell();

                if (!moved)
                {
                    CancelGridMovement();
                    break;
                }

                yield return new WaitForSeconds(_gridStepInterval);
            }

            _gridMovementCoroutine = null;
        }

        protected virtual bool MoveInstantlyToNextGridCell()
        {
            Vector2Int currentCell =
                _grid.WorldToCell(transform.position);

            if (currentCell == _gridTargetCell)
                return false;

            Vector2Int difference =
                _gridTargetCell - currentCell;

            CalculateDirections(
                difference,
                out Vector2Int primaryDirection,
                out Vector2Int secondaryDirection
            );

            if (TryWarpOneCell(currentCell, primaryDirection))
                return true;

            if (TryWarpOneCell(currentCell, secondaryDirection))
                return true;

            return false;
        }

        protected virtual bool TryWarpOneCell(
    Vector2Int currentCell,
    Vector2Int direction)
        {
            if (direction == Vector2Int.zero)
                return false;

            if (!_grid.TryGetNeighbour(
                    currentCell,
                    direction,
                    out Vector2Int nextCell))
            {
                return false;
            }

            Vector3 cellCenter =
                _grid.CellToWorldCenter(nextCell, 0f);

            float sampleDistance =
                Mathf.Max(0.5f, _grid.CellSize * 0.45f);

            if (!NavMesh.SamplePosition(
                    cellCenter,
                    out NavMeshHit navMeshHit,
                    sampleDistance,
                    NavMesh.AllAreas))
            {
                return false;
            }

            RotateTowardsCell(navMeshHit.position);

            _activeStepCell = nextCell;

            if (_navigator.isOnNavMesh)
            {
                _navigator.ResetPath();

                bool warped =
                    _navigator.Warp(navMeshHit.position);

                if (!warped)
                    return false;
            }
            else
            {
                transform.position = navMeshHit.position;
            }

            return true;
        }

        protected virtual void RotateTowardsCell(
    Vector3 destination)
        {
            Vector3 direction =
                destination - transform.position;

            direction.y = 0f;

            if (direction.sqrMagnitude <= 0.001f)
                return;

            transform.rotation =
                Quaternion.LookRotation(
                    direction.normalized,
                    Vector3.up
                );
        }



        /// <summary>
        /// Calcula una única casilla vecina hacia el destino final.
        /// </summary>
        protected virtual void MoveToNextGridCell()
        {
            Vector2Int currentCell =
                _grid.WorldToCell(transform.position);

            if (currentCell == _gridTargetCell)
            {
                FinishGridMovement();
                return;
            }

            Vector2Int difference =
                _gridTargetCell - currentCell;

            Vector2Int primaryDirection;
            Vector2Int secondaryDirection;

            CalculateDirections(
                difference,
                out primaryDirection,
                out secondaryDirection
            );

            // Intenta avanzar primero por el eje prioritario.
            if (TryMoveOneCell(currentCell, primaryDirection))
                return;

            // Si no puede, intenta avanzar por el otro eje.
            if (TryMoveOneCell(currentCell, secondaryDirection))
                return;

            Debug.LogWarning(
                $"El NPC no puede avanzar desde la casilla " +
                $"{currentCell} hasta {_gridTargetCell}.",
                this
            );

            CancelGridMovement();
        }

        /// <summary>
        /// Decide qué eje debe utilizar primero.
        /// </summary>
        protected virtual void CalculateDirections(
            Vector2Int difference,
            out Vector2Int primaryDirection,
            out Vector2Int secondaryDirection)
        {
            Vector2Int horizontalDirection =
                difference.x == 0
                    ? Vector2Int.zero
                    : new Vector2Int(
                        (int)Mathf.Sign(difference.x),
                        0
                    );

            Vector2Int verticalDirection =
                difference.y == 0
                    ? Vector2Int.zero
                    : new Vector2Int(
                        0,
                        (int)Mathf.Sign(difference.y)
                    );

            bool useHorizontalFirst;

            if (_prioritizeLongestAxis)
            {
                useHorizontalFirst =
                    Mathf.Abs(difference.x) >=
                    Mathf.Abs(difference.y);
            }
            else
            {
                useHorizontalFirst = difference.x != 0;
            }

            if (useHorizontalFirst)
            {
                primaryDirection = horizontalDirection;
                secondaryDirection = verticalDirection;
            }
            else
            {
                primaryDirection = verticalDirection;
                secondaryDirection = horizontalDirection;
            }
        }

        /// <summary>
        /// Intenta mandar el NavMeshAgent a una sola casilla vecina.
        /// </summary>
        protected virtual bool TryMoveOneCell(
            Vector2Int currentCell,
            Vector2Int direction)
        {
            if (direction == Vector2Int.zero)
                return false;

            if (!_grid.TryGetNeighbour(
                    currentCell,
                    direction,
                    out Vector2Int nextCell))
            {
                return false;
            }

            Vector3 cellCenter =
                _grid.CellToWorldCenter(nextCell, 0f);

            /*
             * CellToWorldCenter obtiene el centro geométrico,
             * pero necesitamos asegurarnos de que exista un
             * punto válido del NavMesh cerca de ese centro.
             */
            float sampleDistance =
                Mathf.Max(0.5f, _grid.CellSize * 0.45f);

            if (!NavMesh.SamplePosition(
                    cellCenter,
                    out NavMeshHit navMeshHit,
                    sampleDistance,
                    NavMesh.AllAreas))
            {
                return false;
            }

            _activeStepCell = nextCell;
            _state = MOVING;

            _onStartMove.Invoke();

            bool destinationAccepted =
                _navigator.SetDestination(navMeshHit.position);

            if (!destinationAccepted)
            {
                _state = STANDBY;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Movimiento tradicional sin cuadrícula.
        /// </summary>
        protected virtual void GoToNavMeshPosition(
            Vector3 position)
        {
            _movingByGrid = false;
            _state = MOVING;

            _onStartMove.Invoke();
            _navigator.SetDestination(position);
        }

        /// <summary>
        /// Cancela el recorrido actual.
        /// </summary>
        protected virtual void CancelGridMovement()
        {
            _movingByGrid = false;
            _state = STANDBY;

            if (_navigator != null &&
                _navigator.isOnNavMesh)
            {
                _navigator.ResetPath();
            }
        }

        protected float speed
        {
            set { _navigator.speed = value; }
        }

        protected float acceleration
        {
            set { _navigator.acceleration = value; }
        }

        protected float angularSpeed
        {
            set { _navigator.angularSpeed = value; }
        }

        protected float stoppingDistance
        {
            set { _navigator.stoppingDistance = value; }
        }

        protected bool autoBraking
        {
            set { _navigator.autoBraking = value; }
        }

        protected override void CheckHeight()
        {
        }
    }
}