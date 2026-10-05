using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace UdeM.Characters
{
    public class MonkBehaviour : Character3DNavMeshGridNPCBehaviour
    {
        // Guarda el Animator usado por las animaciones del Monk.
        [SerializeField] private Animator animator;

        // Guarda el prefab utilizado para crear las cruces.
        [SerializeField] private MonkCrossBehaviour crossPrefab;

        // Define la cantidad de cruces creadas al comenzar el juego.
        [Min(1)]
        [SerializeField] private int crossCount = 4;

        // Define la altura a la que flotan las cruces mientras estan en posesion.
        [SerializeField] private float possessedCrossHeight = 1.5f;

        // Define el tiempo entre el lanzamiento inicial de cada cruz.
        [Min(0f)]
        [SerializeField] private float timeBetweenInitialLaunches = 0.6f;

        // Define el tiempo minimo antes de un ataque autonomo de las cruces.
        [Min(0f)]
        [SerializeField] private float minimumAutonomousDelay = 1f;

        // Define el tiempo maximo antes de un ataque autonomo de las cruces.
        [Min(0f)]
        [SerializeField] private float maximumAutonomousDelay = 3f;

        // Guarda las cruces creadas y controladas por este Monk.
        private readonly List<MonkCrossBehaviour> crosses =
            new List<MonkCrossBehaviour>();

        // Guarda al jugador detectado actualmente.
        private GameObject detectedPlayer;

        // Indica si el Monk se encuentra muerto.
        private bool isDead = false;

        // Indica si las cruces estan realizando su secuencia inicial.
        private bool initialLaunchRunning = false;

        // Guarda la corrutina usada por los turnos autonomos de las cruces.
        private Coroutine autonomousCrossRoutine;

        // Guarda el indice usado para mantener turnos secuenciales entre cruces.
        private int autonomousTurnIndex = 0;

        // Guarda las cuatro direcciones usadas para colocar las cruces alrededor del Monk.
        private readonly Vector2Int[] possessedDirections =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left
        };

        // Inicializa referencias y crea las cruces pertenecientes al Monk.
        protected override void Start()
        {
            base.Start();

            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            SpawnCrosses();
        }

        // Actualiza visualmente las cruces que actualmente estan en posesion.
        protected override void Update()
        {
            base.Update();

            if (isDead)
                return;

            UpdatePossessedCrossPositions();
        }

        // Crea las cruces iniciales y las coloca bajo la jerarquia del Monk.
        private void SpawnCrosses()
        {
            if (crossPrefab == null)
            {
                Debug.LogError(
                    "MonkBehaviour no tiene asignado Cross Prefab.",
                    this
                );

                return;
            }

            crosses.Clear();

            for (int i = 0;
                 i < crossCount;
                 i++)
            {
                MonkCrossBehaviour newCross =
                    Instantiate(
                        crossPrefab,
                        transform.position,
                        Quaternion.identity,
                        transform
                    );

                newCross.Initialize(
                    this,
                    Grid,
                    i
                );

                crosses.Add(
                    newCross
                );
            }

            UpdatePossessedCrossPositions();
        }

        // Mantiene cada cruz poseida flotando alrededor del Monk segun la cuadricula.
        private void UpdatePossessedCrossPositions()
        {
            if (Grid == null)
                return;
            Vector2Int monkCell =
                Grid.WorldToCell(
                    transform.position
                );

            for (int i = 0;
                 i < crosses.Count;
                 i++)
            {
                MonkCrossBehaviour cross =
                    crosses[i];

                if (cross == null)
                    continue;

                if (!cross.IsPossessed)
                    continue;

                Vector2Int direction =
                    possessedDirections[
                        i % possessedDirections.Length
                    ];

                Vector3 offset =
                    new Vector3(
                        direction.x * Grid.CellSize,
                        possessedCrossHeight,
                        direction.y * Grid.CellSize
                    );

                Vector3 targetPosition =
                    transform.position +
                    offset;

                cross.UpdatePossessedPosition(
                    targetPosition
                );
            }

        }

        // Detecta al jugador y comienza la secuencia de lanzamiento de las cruces.
        public override void PlayerDetected(GameObject detectedTarget)
        {
            if (isDead)
                return;

            if (detectedTarget == null)
                return;

            detectedPlayer =
                detectedTarget;

            behaviourEnabled = false;

            if (initialLaunchRunning)
                return;

            if (!HasPossessedCross())
                return;

            StartCoroutine(
                LaunchPossessedCrosses()
            );
        }

        // Mantiene la referencia del jugador aunque salga temporalmente del area de vision.
        public override void PlayerLost(GameObject lostTarget)
        {
            if (detectedPlayer != lostTarget)
                return;
        }

        // Lanza una por una todas las cruces que actualmente pertenecen al Monk.
        private IEnumerator LaunchPossessedCrosses()
        {
            initialLaunchRunning = true;

            StopMovementFor(
                9999f
            );

            for (int i = 0;
                 i < crosses.Count;
                 i++)
            {
                if (isDead)
                    break;

                MonkCrossBehaviour cross =
                    crosses[i];

                if (cross == null)
                    continue;

                if (!cross.IsPossessed)
                    continue;

                if (detectedPlayer == null)
                    break;

                yield return StartCoroutine(
                    cross.AttackPlayer(
                        detectedPlayer
                    )
                );

                yield return new WaitForSeconds(
                    timeBetweenInitialLaunches
                );
            }

            initialLaunchRunning = false;

            StartAutonomousCrossTurns();
        }

        // Inicia el ciclo independiente de ataques para las cruces fuera de posesion.
        private void StartAutonomousCrossTurns()
        {
            if (autonomousCrossRoutine != null)
                return;

            autonomousCrossRoutine =
                StartCoroutine(
                    AutonomousCrossTurnRoutine()
                );
        }

        // Ejecuta ataques autonomos de una cruz por turno usando intervalos aleatorios.
        private IEnumerator AutonomousCrossTurnRoutine()
        {
            while (!isDead)
            {
                if (detectedPlayer == null)
                {
                    yield return null;
                    continue;
                }

                float delay =
                    Random.Range(
                        minimumAutonomousDelay,
                        maximumAutonomousDelay
                    );

                yield return new WaitForSeconds(
                    delay
                );

                MonkCrossBehaviour cross =
                    GetNextAvailableUnpossessedCross();

                if (cross == null)
                {
                    yield return null;
                    continue;
                }

                yield return StartCoroutine(
                    cross.AttackPlayer(
                        detectedPlayer
                    )
                );
            }

            autonomousCrossRoutine = null;
        }

        // Devuelve la siguiente cruz viva y no poseida respetando el orden de turnos.
        private MonkCrossBehaviour GetNextAvailableUnpossessedCross()
        {
            if (crosses.Count == 0)
                return null;

            for (int checkedCount = 0;
                 checkedCount < crosses.Count;
                 checkedCount++)
            {
                int index =
                    autonomousTurnIndex %
                    crosses.Count;

                autonomousTurnIndex =
                    (autonomousTurnIndex + 1) %
                    crosses.Count;

                MonkCrossBehaviour cross =
                    crosses[index];

                if (cross == null)
                    continue;

                if (cross.IsDead)
                    continue;

                if (cross.IsPossessed)
                    continue;

                if (cross.IsAttacking)
                    continue;

                return cross;
            }

            return null;
        }

        // Comprueba si existe al menos una cruz viva actualmente en posesion.
        private bool HasPossessedCross()
        {
            for (int i = 0;
                 i < crosses.Count;
                 i++)
            {
                MonkCrossBehaviour cross =
                    crosses[i];

                if (cross == null)
                    continue;

                if (cross.IsDead)
                    continue;

                if (cross.IsPossessed)
                    return true;
            }

            return false;
        }

        // Recupera todas las cruces vivas cuando una de ellas muere.
        public void NotifyCrossDied(
            MonkCrossBehaviour deadCross)
        {
            if (isDead)
                return;

            RecallAllLivingCrosses();
        }

        // Reacciona al dano recibido teletransportandose hasta una cruz y recuperandolas.
        public void NotifyMonkDamaged()
        {
            if (isDead)
                return;

            MonkCrossBehaviour teleportCross =
                GetRandomUnpossessedCross();

            if (teleportCross != null)
            {
                TeleportToCross(
                    teleportCross
                );
            }

            RecallAllLivingCrosses();
        }

        // Busca aleatoriamente una cruz viva que actualmente no este en posesion.
        private MonkCrossBehaviour GetRandomUnpossessedCross()
        {
            List<MonkCrossBehaviour> availableCrosses =
                new List<MonkCrossBehaviour>();

            for (int i = 0;
                 i < crosses.Count;
                 i++)
            {
                MonkCrossBehaviour cross =
                    crosses[i];

                if (cross == null)
                    continue;

                if (cross.IsDead)
                    continue;

                if (cross.IsPossessed)
                    continue;

                availableCrosses.Add(
                    cross
                );
            }

            if (availableCrosses.Count == 0)
                return null;

            int randomIndex =
                Random.Range(
                    0,
                    availableCrosses.Count
                );

            return availableCrosses[
                randomIndex
            ];
        }

        // Teletransporta al Monk hasta la celda actualmente ocupada por una cruz.
        private void TeleportToCross(
            MonkCrossBehaviour cross)
        {
            if (cross == null)
                return;

            Vector2Int previousCell =
                CurrentGridCell;

            Vector2Int destinationCell =
                cross.CurrentCell;

            Grid.ReleaseCell(
                previousCell,
                gameObject
            );

            cross.ReleaseOccupiedCell();

            Vector3 destination =
                Grid.CellToWorldCenter(
                    destinationCell,
                    0f
                );

            if (_navigator != null &&
                _navigator.isOnNavMesh)
            {
                _navigator.ResetPath();

                if (NavMesh.SamplePosition(
                        destination,
                        out NavMeshHit hit,
                        Grid.CellSize,
                        NavMesh.AllAreas))
                {
                    _navigator.Warp(
                        hit.position
                    );

                    transform.position =
                        hit.position;
                }
                else
                {
                    transform.position =
                        destination;
                }
            }
            else
            {
                transform.position =
                    destination;
            }

            Grid.TryOccupyCell(
                destinationCell,
                gameObject
            );
        }

        // Devuelve inmediatamente todas las cruces vivas al estado de posesion.
        private void RecallAllLivingCrosses()
        {
            if (autonomousCrossRoutine != null)
            {
                StopCoroutine(
                    autonomousCrossRoutine
                );

                autonomousCrossRoutine = null;
            }

            StopAllCrossAttacks();

            for (int i = 0;
                 i < crosses.Count;
                 i++)
            {
                MonkCrossBehaviour cross =
                    crosses[i];

                if (cross == null)
                    continue;

                if (cross.IsDead)
                    continue;

                cross.ReturnToPossession();
            }

            initialLaunchRunning = false;

            UpdatePossessedCrossPositions();
        }

        // Detiene cualquier movimiento de ataque actualmente ejecutado por las cruces.
        private void StopAllCrossAttacks()
        {
            for (int i = 0;
                 i < crosses.Count;
                 i++)
            {
                MonkCrossBehaviour cross =
                    crosses[i];

                if (cross == null)
                    continue;

                cross.CancelCurrentAttack();
            }
        }

        // Desactiva completamente el comportamiento del Monk cuando muere.
        public void SetDead()
        {
            if (isDead)
                return;

            isDead = true;
            behaviourEnabled = false;

            StopAllCoroutines();

            for (int i = 0;
                 i < crosses.Count;
                 i++)
            {
                MonkCrossBehaviour cross =
                    crosses[i];

                if (cross == null)
                    continue;

                cross.CancelCurrentAttack();
            }
        }

        // Desactiva el ataque adyacente heredado porque el Monk utiliza sus cruces.
        protected override bool Attack(
            Vector2Int direction,
            Vector2Int targetCell)
        {
            return false;
        }
    }
}