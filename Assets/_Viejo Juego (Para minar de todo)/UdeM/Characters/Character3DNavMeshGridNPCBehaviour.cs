using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace UdeM.Characters
{
    public class Character3DNavMeshGridNPCBehaviour : Character3DNavMeshBehaviour
    {
        // Indica si la logica principal del NPC esta habilitada.
        protected bool behaviourEnabled = true;

        // Guarda los puntos que forman la ruta de patrulla.
        [Header("Patrulla")]
        [SerializeField] private List<Transform> patrolPoints;

        // Define cuanto espera el NPC al llegar a un punto de patrulla.
        [SerializeField] private float patrolPointWaitTime = 2f;

        // Define el minimo de celdas antes de una pausa de movimiento.
        [Header("Movimiento por celdas")]
        [Min(1)]
        [SerializeField] private int minimumCellsBeforePause = 1;

        // Define el maximo de celdas antes de una pausa de movimiento.
        [Min(1)]
        [SerializeField] private int maximumCellsBeforePause = 3;

        // Define la duracion de una pausa despues de varias celdas.
        [Min(0f)]
        [SerializeField] private float pauseDuration = 1f;

        // Define el tiempo minimo entre acciones de movimiento.
        [Min(0f)]
        [SerializeField] private float timeBetweenCells = 0.1f;

        // Indica si el NPC necesita una ventana de ritmo para actuar.
        [Header("Ritmo")]
        [SerializeField] private bool requireRhythm = true;

        // Indica si la ventana actual de ritmo permite una accion.
        [SerializeField] private bool rhythmWindowOpen = false;

        // Define la distancia usada para buscar una posicion valida en NavMesh.
        [Header("NavMesh")]
        [Min(0.1f)]
        [SerializeField] private float navMeshSampleDistance = 2f;

        // Guarda la celda ocupada actualmente por el NPC.
        [Header("Estado")]
        [SerializeField] private Vector2Int currentCell;

        // Indica si la accion de la ventana de ritmo ya fue consumida.
        [SerializeField] private bool movementConsumedThisWindow;

        // Cuenta las celdas recorridas desde la ultima pausa.
        [SerializeField] private int movedCells;

        // Guarda cuantas celdas se recorreran antes de la siguiente pausa.
        [SerializeField] private int cellsBeforePause;

        // Guarda el objetivo detectado actualmente por el NPC.
        private GameObject target;

        // Guarda el indice actual dentro de los puntos de patrulla.
        private int patrolIndex;

        // Guarda el proximo instante permitido para moverse.
        private float nextMovementTime;

        // Guarda el proximo instante permitido para continuar la patrulla.
        private float nextPatrolTime;

        // Guarda el estado actual entre patrulla y ataque.
        private int actionState;

        // Indica si la celda actual esta registrada como ocupada.
        private bool cellRegistered;

        // Representa el estado de patrulla del NPC.
        private const int PATROLLING = 2;

        // Representa el estado de ataque del NPC.
        private const int ATTACKING = 3;

        // Expone la celda actual para otros sistemas.
        public Vector2Int CurrentCell => currentCell;

        // Expone el estado actual de la ventana de ritmo.
        public bool RhythmWindowOpen => rhythmWindowOpen;

        // Expone si el NPC se encuentra patrullando.
        public bool IsPatrolling => actionState == PATROLLING;

        // Expone si el NPC se encuentra atacando.
        public bool IsAttacking => actionState == ATTACKING;

        // Expone la cuadricula a las clases derivadas.
        protected MeshGrid Grid => _grid;

        // Expone el objetivo actual a las clases derivadas.
        protected GameObject CurrentTarget => target;

        // Expone la celda actual a las clases derivadas.
        protected Vector2Int CurrentGridCell => currentCell;

        // Indica si existe un bloqueo temporal de movimiento.
        private bool movementStopped;

        // Guarda el instante en que termina el bloqueo temporal.
        private float movementStopUntil;

        // Inicializa el estado de patrulla y el contador de movimiento.
        protected override void Awake()
        {
            base.Awake();

            actionState = PATROLLING;
            patrolIndex = 0;
            movedCells = 0;

            SelectNewCellsBeforePause();
        }

        // Configura referencias, ocupacion de celda, navegacion y vision.
        protected override void Start()
        {
            base.Start();

            ValidateValues();

            if (_grid == null)
                _grid = FindFirstObjectByType<MeshGrid>();

            if (_grid == null)
            {
                Debug.LogError(
                    "No se encontro un objeto con MeshGrid.",
                    this
                );

                enabled = false;
                return;
            }

            if (_navigator == null)
            {
                Debug.LogError(
                    "No se encontro un NavMeshAgent.",
                    this
                );

                enabled = false;
                return;
            }

            if (!_navigator.isOnNavMesh)
            {
                if (NavMesh.SamplePosition(
                    transform.position,
                    out NavMeshHit hit,
                    100f,
                    NavMesh.AllAreas))
                {
                    _navigator.enabled = false;
                    transform.position = hit.position;
                    _navigator.enabled = true;

                    Debug.Log(
                        $"Enemy ajustado al NavMesh: {hit.position}",
                        this
                    );
                }
                else
                {
                    Debug.LogError(
                        $"No se encontró NavMesh cerca de {transform.position}",
                        this
                    );

                    enabled = false;
                    return;
                }
            }

            currentCell = _grid.WorldToCell(transform.position);

            if (!_grid.TryOccupyCell(currentCell, gameObject))
            {
                Debug.LogError(
                    $"La celda inicial {currentCell} ya esta ocupada.",
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

        // Ejecuta acciones automaticas cuando el NPC no depende del ritmo.
        protected override void Update()
        {
            base.Update();

            if (!behaviourEnabled)
                return;

            if (!requireRhythm)
                TryPerformGridAction();
        }

        // Bloquea temporalmente el movimiento y limpia cualquier destino activo.
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

        // Libera la celda registrada cuando el objeto es destruido.
        protected virtual void OnDestroy()
        {
            ReleaseCurrentCell();
        }

        // Libera la celda registrada cuando el componente se desactiva.
        private void OnDisable()
        {
            if (!gameObject.scene.isLoaded)
                return;

            ReleaseCurrentCell();
        }

        // Configura el NavMeshAgent para el movimiento por celdas.
        private void ConfigureNavigator()
        {
            _navigator.updatePosition = true;
            _navigator.updateRotation = true;
            _navigator.autoBraking = true;

            if (_navigator.isOnNavMesh)
            {
                _navigator.isStopped = true;
                _navigator.ResetPath();
            }
            else
            {
                Debug.LogWarning(
                    $"{name} no está sobre el NavMesh. Posición: {transform.position}",
                    this
                );
            }

            _state = STANDBY;
        }

        // Configura el componente encargado de detectar al jugador.
        private void ConfigureVision()
        {
            Transform visionTransform =
                transform.Find("Vision");

            if (visionTransform == null)
                return;

            GridVisionBehaviour vision =
                visionTransform.GetComponent<GridVisionBehaviour>();

            if (vision == null)
            {
                vision =
                    visionTransform.gameObject
                        .AddComponent<GridVisionBehaviour>();
            }

            vision.Initialize(this);
        }

        // Ajusta los valores configurables para mantener rangos validos.
        private void ValidateValues()
        {
            minimumCellsBeforePause =
                Mathf.Max(1, minimumCellsBeforePause);

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
                Mathf.Max(0.1f, navMeshSampleDistance);
        }

        // Decide si debe atacar, patrullar o avanzar hacia una celda.
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
                    _grid.WorldToCell(target.transform.position);

                if (TryAttackAdjacentTarget(destinationCell))
                    return;
            }
            else
            {
                actionState = PATROLLING;

                if (!TryGetPatrolDestination(out destinationCell))
                    return;
            }

            if (destinationCell == currentCell)
            {
                HandleDestinationReached();
                return;
            }

            TryMoveOneCellTowards(destinationCell);
        }

        // Intenta atacar cuando el objetivo se encuentra en una celda vecina.
        private bool TryAttackAdjacentTarget(Vector2Int targetCell)
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

            FaceGridDirection(attackDirection);

            if (!Attack(attackDirection, targetCell))
                return false;

            if (requireRhythm)
                movementConsumedThisWindow = true;

            nextMovementTime =
                Time.time + timeBetweenCells;

            return true;
        }

        // Permite que las clases derivadas definan su propia accion de ataque.
        protected virtual bool Attack(
            Vector2Int direction,
            Vector2Int targetCell)
        {
            return false;
        }

        // Orienta el NPC hacia una direccion expresada en coordenadas de cuadricula.
        protected void FaceGridDirection(Vector2Int direction)
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

        // Comprueba si el NPC puede realizar una nueva accion de cuadricula.
        private bool CanPerformGridAction()
        {
            if (movementStopped)
            {
                if (Time.time < movementStopUntil)
                    return false;

                movementStopped = false;
            }

            if (_grid == null ||
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

        // Obtiene la celda del punto de patrulla actualmente seleccionado.
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
                _grid.WorldToCell(patrolPoint.position);

            return true;
        }

        // Intenta avanzar una celda hacia el destino indicado.
        private bool TryMoveOneCellTowards(Vector2Int destinationCell)
        {
            Vector2Int difference =
                destinationCell - currentCell;

            CalculateDirections(
                difference,
                out Vector2Int primaryDirection,
                out Vector2Int secondaryDirection
            );

            if (TryMoveInDirection(primaryDirection))
                return true;

            return TryMoveInDirection(secondaryDirection);
        }

        // Calcula primero el eje con mayor distancia hacia el destino.
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

        // Intenta ocupar y mover el NPC hacia una celda vecina valida.
        private bool TryMoveInDirection(Vector2Int direction)
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

            if (nextCell == currentCell)
                return false;

            if (_grid.IsCellOccupiedByOther(
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

            if (!_grid.TryMoveOccupant(
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
                    _grid.TryMoveOccupant(
                        nextCell,
                        previousCell,
                        gameObject
                    );

                if (!rollbackSucceeded)
                {
                    Debug.LogError(
                        "No se pudo restaurar la celda " +
                        "del enemigo despues de fallar Warp.",
                        this
                    );

                    cellRegistered = false;
                }

                return false;
            }

            currentCell = nextCell;
            _state = STANDBY;

            if (requireRhythm)
                movementConsumedThisWindow = true;

            RegisterCellMovement();
            HandleDestinationReached();

            return true;
        }

        // Busca una posicion del NavMesh que pertenezca a la celda indicada.
        private bool TryGetNavMeshPosition(
            Vector2Int cell,
            out Vector3 navMeshPosition)
        {
            Vector3 cellPosition =
                _grid.CellToWorldCenter(cell, 0f);

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
                navMeshPosition = Vector3.zero;
                return false;
            }

            Vector2Int sampledCell =
                _grid.WorldToCell(hit.position);

            if (sampledCell != cell)
            {
                navMeshPosition = Vector3.zero;
                return false;
            }

            navMeshPosition = hit.position;
            return true;
        }

        // Registra una celda recorrida y programa la siguiente pausa o movimiento.
        private void RegisterCellMovement()
        {
            movedCells++;

            if (movedCells >= cellsBeforePause)
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

        // Selecciona una cantidad aleatoria de celdas antes de la siguiente pausa.
        private void SelectNewCellsBeforePause()
        {
            cellsBeforePause =
                Random.Range(
                    minimumCellsBeforePause,
                    maximumCellsBeforePause + 1
                );
        }

        // Procesa la llegada a una celda objetivo de ataque o patrulla.
        private void HandleDestinationReached()
        {
            if (actionState == ATTACKING &&
                target != null)
            {
                Vector2Int targetCell =
                    _grid.WorldToCell(target.transform.position);

                if (currentCell == targetCell)
                    OnPlayerCellReached();

                return;
            }

            if (actionState != PATROLLING)
                return;

            CheckPatrolPointReached();
        }

        // Comprueba si el NPC alcanzo el punto de patrulla actual.
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
                _grid.WorldToCell(patrolPoint.position);

            if (currentCell != patrolCell)
                return;

            AdvancePatrolPoint();

            nextPatrolTime =
                Time.time + patrolPointWaitTime;

            nextMovementTime =
                Mathf.Max(
                    nextMovementTime,
                    nextPatrolTime
                );
        }

        // Avanza el indice hacia el siguiente punto de patrulla.
        private void AdvancePatrolPoint()
        {
            if (patrolPoints == null ||
                patrolPoints.Count == 0)
            {
                patrolIndex = 0;
                return;
            }

            patrolIndex++;

            if (patrolIndex >= patrolPoints.Count)
                patrolIndex = 0;
        }

        // Libera la celda ocupada por el NPC dentro de la cuadricula.
        private void ReleaseCurrentCell()
        {
            if (!cellRegistered ||
                _grid == null)
            {
                return;
            }

            _grid.ReleaseCell(
                currentCell,
                gameObject
            );

            cellRegistered = false;
        }

        // Informa cuando una celda vecina esta ocupada por otro objeto.
        protected virtual void OnCellBlocked(Vector2Int blockedCell)
        {
            Debug.Log(
                $"El enemigo no puede entrar en {blockedCell}: " +
                "la celda esta ocupada.",
                this
            );
        }

        // Informa cuando el enemigo alcanza la misma celda que el jugador.
        protected virtual void OnPlayerCellReached()
        {
            Debug.Log(
                $"El enemigo alcanzo la celda del jugador: {currentCell}.",
                this
            );
        }

        // Abre o cierra la ventana de ritmo y permite una nueva accion al abrirla.
        public void SetRhythmWindow(bool isOpen)
        {
            rhythmWindowOpen = isOpen;

            if (!isOpen)
                return;

            movementConsumedThisWindow = false;
            TryPerformGridAction();
        }

        // Activa o desactiva la dependencia del sistema de ritmo.
        public void SetRequireRhythm(bool value)
        {
            requireRhythm = value;
            movementConsumedThisWindow = false;

            if (!requireRhythm)
                TryPerformGridAction();
        }

        // Cambia el comportamiento a ataque cuando se detecta un jugador.
        public virtual void PlayerDetected(GameObject detectedTarget)
        {
            if (!behaviourEnabled)
                return;

            if (detectedTarget == null)
                return;

            target = detectedTarget;
            actionState = ATTACKING;

            TryPerformGridAction();
        }

        // Regresa el comportamiento a patrulla cuando se pierde el objetivo.
        public virtual void PlayerLost(GameObject lostTarget)
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

        private class GridVisionBehaviour : MonoBehaviour
        {
            // Guarda el NPC que recibe los eventos del area de vision.
            private Character3DNavMeshGridNPCBehaviour owner;

            // Asigna el NPC propietario de este detector de vision.
            public void Initialize(Character3DNavMeshGridNPCBehaviour npc)
            {
                owner = npc;
            }

            // Notifica al NPC cuando el jugador entra en el area de vision.
            private void OnTriggerEnter(Collider other)
            {
                if (owner == null)
                    return;

                if (other.CompareTag("Player"))
                    owner.PlayerDetected(other.gameObject);
            }

            // Mantiene actualizado al NPC mientras el jugador sigue en el area de vision.
            private void OnTriggerStay(Collider other)
            {
                if (owner == null)
                    return;

                if (other.CompareTag("Player"))
                    owner.PlayerDetected(other.gameObject);
            }

            // Notifica al NPC cuando el jugador sale del area de vision.
            private void OnTriggerExit(Collider other)
            {
                if (owner == null)
                    return;

                if (other.CompareTag("Player"))
                    owner.PlayerLost(other.gameObject);
            }
        }
    }
}