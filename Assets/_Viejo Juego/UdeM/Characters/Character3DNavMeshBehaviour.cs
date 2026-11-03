using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using System.Collections;

namespace UdeM.Characters
{
    public class Character3DNavMeshBehaviour : CharacterBehaviour
    {
        // Guarda el NavMeshAgent usado para mover el personaje.
        protected NavMeshAgent _navigator;

        // Notifica cuando termina un desplazamiento completo.
        protected UnityEvent _onFinishMove;

        // Guarda el estado actual de movimiento del personaje.
        [SerializeField] protected int _state;

        // Guarda la cuadricula usada para convertir posiciones en celdas.
        [Header("Movimiento por cuadricula")]
        [SerializeField] protected MeshGrid _grid;

        // Define si primero se intenta avanzar por el eje con mayor distancia.
        [Tooltip("Prioriza el eje con mayor distancia al destino.")]
        [SerializeField] protected bool _prioritizeLongestAxis = true;

        // Representa el estado en espera del personaje.
        protected const int STANDBY = 0;

        // Representa el estado de movimiento del personaje.
        protected const int MOVING = 1;

        // Guarda la celda final del recorrido por cuadricula.
        protected Vector2Int _gridTargetCell;

        // Indica si existe un recorrido activo por cuadricula.
        protected bool _movingByGrid;

        // Define el tiempo entre pasos del recorrido por cuadricula.
        [Header("Ritmo de desplazamiento")]
        [Min(0.01f)]
        [SerializeField] protected float _gridStepInterval = 0.5f;

        // Guarda la corrutina activa del recorrido por cuadricula.
        protected Coroutine _gridMovementCoroutine;

        // Configura eventos y referencias necesarias para la navegacion.
        protected override void Start()
        {
            base.Start();

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
                    "No se encontro MeshGrid. " +
                    "El personaje utilizara movimiento NavMesh normal.",
                    this
                );
            }
        }

        // Cambia el estado a espera cuando termina un movimiento.
        protected virtual void OnFinishMove()
        {
            _state = STANDBY;
        }

        // Revisa cada frame si el NavMeshAgent termino su desplazamiento.
        protected override void Update()
        {
            base.Update();
            CheckMoveState();
        }

        // Detecta cuando termina un movimiento normal o un paso de cuadricula.
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

        // Continua o finaliza el recorrido despues de completar una celda.
        protected virtual void OnGridStepFinished()
        {
            Vector2Int currentCell =
                _grid.WorldToCell(transform.position);

            if (currentCell == _gridTargetCell)
            {
                FinishGridMovement();
                return;
            }

            MoveToNextGridCell();
        }

        // Finaliza el recorrido por cuadricula y libera el destino del agente.
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

        // Inicia un desplazamiento hacia una posicion del mundo.
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

        // Ejecuta el recorrido por cuadricula respetando el intervalo configurado.
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

        // Intenta avanzar una celda hacia el destino usando Warp.
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

        // Intenta mover el agente a una celda vecina usando Warp.
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

        // Rota el personaje hacia el centro de la celda indicada.
        protected virtual void RotateTowardsCell(Vector3 destination)
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

        // Intenta avanzar una celda usando el movimiento normal del NavMeshAgent.
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

            CalculateDirections(
                difference,
                out Vector2Int primaryDirection,
                out Vector2Int secondaryDirection
            );

            if (TryMoveOneCell(currentCell, primaryDirection))
                return;

            if (TryMoveOneCell(currentCell, secondaryDirection))
                return;

            Debug.LogWarning(
                $"El NPC no puede avanzar desde la celda " +
                $"{currentCell} hasta {_gridTargetCell}.",
                this
            );

            CancelGridMovement();
        }

        // Calcula el orden de los ejes usados para acercarse al destino.
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

        // Intenta enviar el NavMeshAgent a una celda vecina valida.
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

            _state = MOVING;

            bool destinationAccepted =
                _navigator.SetDestination(navMeshHit.position);

            if (!destinationAccepted)
            {
                _state = STANDBY;
                return false;
            }

            return true;
        }

        // Mueve el personaje a una posicion sin usar la cuadricula.
        protected virtual void GoToNavMeshPosition(Vector3 position)
        {
            _movingByGrid = false;
            _state = MOVING;

            _navigator.SetDestination(position);
        }

        // Cancela el recorrido actual y limpia el destino del agente.
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
    }
}