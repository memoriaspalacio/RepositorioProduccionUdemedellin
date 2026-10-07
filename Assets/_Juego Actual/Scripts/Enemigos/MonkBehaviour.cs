using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace UdeM.Characters
{
    public class MonkBehaviour : Character3DNavMeshGridNPCBehaviour
    {
        // Guarda el Animator usado por las animaciones del Monk.
        [Header("Animaciones")]
        public Animator animator;

        // Guarda el nombre del trigger usado para ejecutar el ataque.
        [SerializeField]
        private string attackTrigger = "Attack";
        
        [Tooltip("Escribe aqui el nombre del Trigger de Hurt (ej. onHurt) para reproducir la animacion despues del teletransporte.")]
        [SerializeField] private string hurtTriggerAfterWarp = "onHurt";
        
        [Header("Configuracion de Teletransporte")]
        [Min(0f)]
        [SerializeField] private float stunDurationAfterDamage = 1f;
        
        [Header("Efectos Visuales")]
        // Trail logic has been removed.

        // Guarda el prefab utilizado para crear las cruces.
        [SerializeField] private MonkCrossBehaviour crossPrefab;

        // Define el numero de cruces (Maximo 4 para evitar que se superpongan en la misma casilla).
        [Range(1, 4)]
        [SerializeField] private int crossCount = 4;

        // Define la altura a la que flotan las cruces mientras estan en posesion.
        [SerializeField] private float possessedCrossHeight = 1.5f;

        // Define el tiempo entre el lanzamiento inicial de cada cruz.
        [Min(0f)]
        [SerializeField] private float timeBetweenInitialLaunches = 0.6f;

        // Define el tiempo que tarda en aparecer cada cruz de forma progresiva.
        [Min(0f)]
        [SerializeField] private float timeBetweenCrossSpawns = 0.5f;

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

        // Guarda la corrutina de ataque autonomo.
        private Coroutine autonomousCrossRoutine;

        // Guarda la corrutina de lanzamiento inicial de las cruces.
        private Coroutine launchRoutine;

        // Guarda el indice de la cruz que debe atacar en el siguiente turno.
        private int autonomousTurnIndex = 0;

        // Direcciones locales correspondientes a cada cruz (arriba, derecha, abajo, izquierda).
        private readonly Vector2Int[] possessedDirections =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left
        };

        [Header("Configuracion Inicial")]
        [SerializeField] private Vector2Int initialFacingDirection = Vector2Int.down;

        // Guarda la posicion local inicial del modelo 3D para restaurarla despues de aparecer.
        private Vector3 initialAnimatorLocalPos;

        // Inicializa referencias y crea las cruces pertenecientes al Monk.
        protected override void Start()
        {
            base.Start();

            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            if (animator != null)
                initialAnimatorLocalPos = animator.transform.localPosition;

            FaceGridDirection(initialFacingDirection);
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

        // Crea las cruces iniciales de forma progresiva y las coloca bajo la jerarquia del Monk.
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
            StartCoroutine(SpawnCrossesRoutine());
        }

        private IEnumerator SpawnCrossesRoutine()
        {
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

                UpdatePossessedCrossPositions();

                yield return new WaitForSeconds(timeBetweenCrossSpawns);
            }
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

        // Detecta al jugador y comienza la secuencia de lanzamiento de las cruces, y ahora persigue al jugador
        public override void PlayerDetected(GameObject detectedTarget)
        {
            if (isDead)
                return;

            if (detectedTarget == null)
                return;

            detectedPlayer =
                detectedTarget;

            // Antes: behaviourEnabled = false; (esto lo congelaba)
            // Ahora llamamos a la base para que persiga y salte al moverse:
            base.PlayerDetected(detectedTarget);

            if (initialLaunchRunning)
                return;

            if (!HasPossessedCross())
                return;

            launchRoutine = StartCoroutine(
                LaunchPossessedCrosses()
            );
        }

        // Mantiene la referencia del jugador aunque salga temporalmente del area de vision.
        public override void PlayerLost(GameObject lostTarget)
        {
            if (detectedPlayer != lostTarget)
                return;

            base.PlayerLost(lostTarget);
        }

        // Lanza una por una todas las cruces que actualmente pertenecen al Monk.
        private IEnumerator LaunchPossessedCrosses()
        {
            initialLaunchRunning = true;

            // Ya no lo congelamos para siempre, el seguira persiguiendo y saltando.
            // Eliminamos: StopMovementFor(9999f);

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

                if (cross.IsDead)
                    continue;

                if (!cross.IsPossessed)
                    continue;

                if (detectedPlayer == null)
                    break;

                if (animator != null)
                    animator.SetTrigger(attackTrigger);

                // Espera a que la cruz termine su ataque antes de lanzar la siguiente.
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
            launchRoutine = null;

            if (!isDead)
                StartAutonomousAttackRoutine();
        }

        // Comienza el ciclo en el que las cruces atacan por si solas.
        private void StartAutonomousAttackRoutine()
        {
            if (autonomousCrossRoutine != null)
                return;

            autonomousCrossRoutine =
                StartCoroutine(
                    AutonomousCrossSequence()
                );
        }

        // Elige una cruz cada cierto tiempo para atacar al jugador.
        private IEnumerator AutonomousCrossSequence()
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

                MonkCrossBehaviour attackerCross =
                    GetNextCrossForTurn();

                if (attackerCross == null)
                {
                    yield return null;
                    continue;
                }

                if (animator != null)
                    animator.SetTrigger(attackTrigger);

                // Solo una cruz ataca por turno; espera a que termine.
                yield return StartCoroutine(
                    attackerCross.AttackPlayer(
                        detectedPlayer
                    )
                );
            }

            autonomousCrossRoutine = null;
        }

        // Selecciona la siguiente cruz en el orden definido para su ataque autonomo.
        private MonkCrossBehaviour GetNextCrossForTurn()
        {
            int startIndex =
                autonomousTurnIndex;

            int attempts = 0;

            while (attempts < crosses.Count)
            {
                MonkCrossBehaviour cross =
                    crosses[
                        autonomousTurnIndex
                    ];

                autonomousTurnIndex =
                    (autonomousTurnIndex + 1) %
                    crosses.Count;

                attempts++;

                if (cross != null &&
                    !cross.IsDead &&
                    !cross.IsPossessed &&
                    !cross.IsAttacking)
                {
                    return cross;
                }
            }

            return null;
        }

        // Verifica si queda al menos una cruz lista para ser lanzada.
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

            StopMovementFor(stunDurationAfterDamage);

            MonkCrossBehaviour teleportCross =
                GetRandomUnpossessedCross();

            if (teleportCross != null)
            {
                teleportCross.TriggerTeleportEffect();
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

            CancelVisualMovement();

            Vector3 startWorldPos = transform.position;

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

            SetCurrentCell(destinationCell);

            if (animator != null && !string.IsNullOrEmpty(hurtTriggerAfterWarp))
            {
                StartCoroutine(TriggerAnimationAfterWarp());
            }
        }

        private System.Collections.IEnumerator TriggerAnimationAfterWarp()
        {
            yield return null;
            if (animator != null)
            {
                animator.SetTrigger(hurtTriggerAfterWarp);
            }
        }



        // Devuelve inmediatamente todas las cruces vivas al estado de posesion.
        private void RecallAllLivingCrosses()
        {
            if (launchRoutine != null)
            {
                StopCoroutine(
                    launchRoutine
                );

                launchRoutine = null;
            }

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

            autonomousCrossRoutine = null;
            initialLaunchRunning = false;

            StopAllCrossAttacks();
        }

        // Desactiva el ataque adyacente heredado porque el Monk ataca con sus cruces.
        protected override bool Attack(
            Vector2Int direction,
            Vector2Int targetCell)
        {
            return false;
        }
    }
}