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

        // Guarda el nombre del parametro de oracion (Bool) usado mientras las cruces atacan.
        // Si el parametro del controller es un Trigger, se dispara una vez por oracion.
        [SerializeField]
        private string attackTrigger = "Attack";

        [Tooltip("Escribe aqui el nombre del Trigger de Hurt (ej. onHurt) para reproducir la animacion despues del teletransporte.")]
        [SerializeField] private string hurtTriggerAfterWarp = "onHurt";

        // Define cuanto reza el Monk antes de soltar la primera cruz, para que el lanzamiento coincida con la animacion.
        [Min(0f)]
        [SerializeField] private float prayerWindup = 0.35f;

        // Define el tiempo minimo que se mantiene la oracion para que la animacion no se corte a medias.
        [Min(0f)]
        [SerializeField] private float minimumPrayerDuration = 1.2f;

        // Indica si el Monk deja de patrullar mientras reza.
        [SerializeField] private bool stopMovementWhilePraying = true;

        // Define la velocidad con la que el modelo gira hacia donde camina o hacia el jugador.
        [Min(0f)]
        [SerializeField] private float visualTurnSpeed = 540f;

        [Header("Configuracion de Teletransporte")]
        [Min(0f)]
        [SerializeField] private float stunDurationAfterDamage = 1f;

        // Define cuanto queda quieto el Monk mientras reproduce la animacion de aparicion.
        [Min(0f)]
        [SerializeField] private float appearanceLockDuration = 1.2f;

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

        // Guarda el modelo visual que gira y salta (el transform del Animator).
        private Transform visualModel;

        // Guarda la rotacion del modelo relativa a la orientacion del Monk, para girarlo sin deformar su pose base.
        private Quaternion visualRotationOffset = Quaternion.identity;

        // Guarda hacia donde debe mirar el modelo.
        private Vector3 visualFacingDirection;

        // Guarda la posicion del Monk en el frame anterior para detectar hacia donde se movio.
        private Vector3 lastRootPosition;

        // Guarda los tipos de parametros del Animator para no llamar parametros inexistentes.
        private readonly Dictionary<string, AnimatorControllerParameterType> animatorParameters =
            new Dictionary<string, AnimatorControllerParameterType>();

        // Cuenta los ataques de cruz en curso (la secuencia inicial y la autonoma pueden solaparse).
        private int activeCrossAttacks = 0;

        // Guarda hasta cuando debe mantenerse la oracion aunque no haya ataques activos.
        private float prayerHoldUntil = 0f;

        // Indica si la animacion de oracion esta activa.
        private bool isPraying = false;

        // Identifica la tanda de ataques actual; cambia al recuperar las cruces para invalidar corrutinas viejas.
        private int attackSequenceId = 0;

        // Inicializa referencias y crea las cruces pertenecientes al Monk.
        protected override void Start()
        {
            base.Start();

            ResolveAnimator();

            if (animator != null)
            {
                visualModel = animator.transform;
                initialAnimatorLocalPos = visualModel.localPosition;
                CacheAnimatorParameters();
            }

            FaceGridDirection(initialFacingDirection);

            if (visualModel != null)
            {
                visualRotationOffset =
                    Quaternion.Inverse(transform.rotation) *
                    visualModel.rotation;
            }

            visualFacingDirection = transform.forward;
            lastRootPosition = transform.position;

            SpawnCrosses();
        }

        // Actualiza la oracion, la orientacion del modelo y las cruces en posesion.
        protected override void Update()
        {
            base.Update();

            if (isDead)
                return;

            UpdatePrayerState();
            UpdateVisualFacing();
            UpdatePossessedCrossPositions();
        }

        // Usa el Animator del modelo hijo; el Animator de la raiz del enemigo no reproduce las animaciones del Monk.
        private void ResolveAnimator()
        {
            Animator assignedAnimator =
                animator;

            if (IsUsableAnimator(animator) &&
                animator.gameObject != gameObject)
            {
                LogResolvedAnimator();
                return;
            }

            Animator[] animators =
                GetComponentsInChildren<Animator>(true);

            for (int i = 0;
                 i < animators.Length;
                 i++)
            {
                if (animators[i].gameObject == gameObject)
                    continue;

                if (!IsUsableAnimator(animators[i]))
                    continue;

                animator = animators[i];
                break;
            }

            if (animator == null &&
                animators.Length > 0)
            {
                animator = animators[0];
            }

            if (animator != assignedAnimator)
            {
                Debug.LogWarning(
                    $"MonkBehaviour: el Animator asignado ({(assignedAnimator != null ? assignedAnimator.name : "ninguno")}) " +
                    $"no sirve para las animaciones; se usa el de '{(animator != null ? animator.name : "ninguno")}'. " +
                    "Asigna ese Animator en el Inspector para evitar este aviso.",
                    this
                );
            }

            LogResolvedAnimator();
        }

        // Indica si el Animator esta activo y tiene un controller que pueda reproducir.
        private static bool IsUsableAnimator(
            Animator candidate)
        {
            return candidate != null &&
                   candidate.isActiveAndEnabled &&
                   candidate.runtimeAnimatorController != null;
        }

        // Deja en consola que Animator y controller usa el Monk, para verificarlo rapido.
        private void LogResolvedAnimator()
        {
            if (animator == null)
            {
                Debug.LogWarning(
                    "MonkBehaviour: no se encontro ningun Animator para el Monk.",
                    this
                );

                return;
            }

            string controllerName =
                animator.runtimeAnimatorController != null
                    ? animator.runtimeAnimatorController.name
                    : "sin controller";

            Debug.Log(
                $"MonkBehaviour usa el Animator de '{animator.name}' con el controller '{controllerName}'.",
                this
            );
        }

        // Registra los parametros existentes en el controller del Animator.
        private void CacheAnimatorParameters()
        {
            animatorParameters.Clear();

            if (animator == null ||
                animator.runtimeAnimatorController == null)
            {
                Debug.LogWarning(
                    "MonkBehaviour: el Animator no tiene un Animator Controller asignado.",
                    this
                );

                return;
            }

            AnimatorControllerParameter[] parameters =
                animator.parameters;

            for (int i = 0;
                 i < parameters.Length;
                 i++)
            {
                animatorParameters[parameters[i].name] =
                    parameters[i].type;
            }
        }

        // Activa o desactiva un parametro Bool; si es Trigger, lo dispara al activarlo.
        private void SetAnimatorFlag(
            string parameterName,
            bool value)
        {
            if (animator == null ||
                string.IsNullOrEmpty(parameterName))
            {
                return;
            }

            if (!animatorParameters.TryGetValue(
                    parameterName,
                    out AnimatorControllerParameterType type))
            {
                return;
            }

            if (type == AnimatorControllerParameterType.Bool)
            {
                animator.SetBool(
                    parameterName,
                    value
                );
            }
            else if (type == AnimatorControllerParameterType.Trigger)
            {
                if (value)
                    animator.SetTrigger(parameterName);
                else
                    animator.ResetTrigger(parameterName);
            }
        }

        // Dispara un Trigger del Animator si existe en el controller.
        private void FireAnimatorTrigger(
            string parameterName)
        {
            if (animator == null ||
                string.IsNullOrEmpty(parameterName))
            {
                return;
            }

            if (!animatorParameters.TryGetValue(
                    parameterName,
                    out AnimatorControllerParameterType type))
            {
                return;
            }

            if (type == AnimatorControllerParameterType.Trigger)
                animator.SetTrigger(parameterName);
        }

        // Mantiene la animacion de oracion mientras haya ataques de cruz en curso.
        private void UpdatePrayerState()
        {
            bool shouldPray =
                activeCrossAttacks > 0 ||
                Time.time < prayerHoldUntil;

            // Mientras reza no salta entre celdas; el bloqueo se renueva cada frame y se libera al terminar.
            if (shouldPray &&
                stopMovementWhilePraying)
            {
                StopMovementFor(0.1f);
            }

            if (shouldPray == isPraying)
                return;

            isPraying = shouldPray;

            SetAnimatorFlag(
                attackTrigger,
                isPraying
            );
        }

        // Corta la oracion de inmediato (por dano, recuperacion de cruces o muerte).
        private void InterruptPrayer()
        {
            activeCrossAttacks = 0;
            prayerHoldUntil = 0f;

            if (!isPraying)
                return;

            isPraying = false;

            SetAnimatorFlag(
                attackTrigger,
                false
            );
        }

        // Gira suavemente el modelo hacia el jugador mientras reza o hacia donde se movio el Monk.
        private void UpdateVisualFacing()
        {
            Vector3 rootDelta =
                transform.position -
                lastRootPosition;

            rootDelta.y = 0f;

            lastRootPosition =
                transform.position;

            if (isPraying &&
                detectedPlayer != null)
            {
                Vector3 toPlayer =
                    detectedPlayer.transform.position -
                    transform.position;

                toPlayer.y = 0f;

                if (toPlayer.sqrMagnitude > 0.001f)
                    visualFacingDirection = toPlayer.normalized;
            }
            else if (rootDelta.sqrMagnitude > 0.0001f)
            {
                visualFacingDirection = rootDelta.normalized;
            }

            if (visualModel == null ||
                visualModel == transform)
            {
                return;
            }

            if (visualFacingDirection.sqrMagnitude < 0.001f)
                return;

            Quaternion targetRotation =
                Quaternion.LookRotation(
                    visualFacingDirection,
                    Vector3.up
                ) *
                visualRotationOffset;

            visualModel.rotation =
                Quaternion.RotateTowards(
                    visualModel.rotation,
                    targetRotation,
                    visualTurnSpeed *
                    Time.deltaTime
                );
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

        // Detecta al jugador sin abandonar la patrulla ni perseguirlo directamente.
        public override void PlayerDetected(GameObject detectedTarget)
        {
            if (isDead)
                return;

            if (detectedTarget == null)
                return;

            detectedPlayer =
                detectedTarget;

            if (initialLaunchRunning)
                return;

            // Evita reiniciar la secuencia cada frame cuando ninguna cruz poseida puede atacar.
            if (!HasLaunchablePossessedCross(detectedPlayer))
                return;

            launchRoutine = StartCoroutine(
                LaunchPossessedCrosses()
            );
        }

        // Elimina la referencia cuando el jugador sale de la vision sin alterar la patrulla.
        public override void PlayerLost(GameObject lostTarget)
        {
            if (detectedPlayer != lostTarget)
                return;

            detectedPlayer = null;
        }

        // Lanza una por una todas las cruces que actualmente pertenecen al Monk.
        private IEnumerator LaunchPossessedCrosses()
        {
            initialLaunchRunning = true;

            int sequenceId =
                attackSequenceId;

            // El Monk reza durante toda la secuencia; las cruces salen una tras otra.
            for (int i = 0;
                 i < crosses.Count;
                 i++)
            {
                if (isDead ||
                    sequenceId != attackSequenceId)
                {
                    yield break;
                }

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

                if (!cross.CanAttackPlayer(detectedPlayer))
                    continue;

                // Espera a que la cruz termine su ataque antes de lanzar la siguiente.
                yield return PerformCrossAttack(
                    cross,
                    detectedPlayer,
                    sequenceId
                );

                if (sequenceId != attackSequenceId)
                    yield break;

                // Sigue rezando durante la pausa para no cortar la animacion entre cruz y cruz.
                prayerHoldUntil =
                    Mathf.Max(
                        prayerHoldUntil,
                        Time.time + timeBetweenInitialLaunches + 0.1f
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
            int sequenceId =
                attackSequenceId;

            while (!isDead &&
                   sequenceId == attackSequenceId)
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

                if (sequenceId != attackSequenceId)
                    yield break;

                MonkCrossBehaviour attackerCross =
                    GetNextCrossForTurn(
                        detectedPlayer
                    );

                if (attackerCross == null)
                {
                    yield return null;
                    continue;
                }

                // Solo una cruz ataca por turno; espera a que termine.
                yield return PerformCrossAttack(
                    attackerCross,
                    detectedPlayer,
                    sequenceId
                );
            }

            if (sequenceId == attackSequenceId)
                autonomousCrossRoutine = null;
        }

        // Reza, suelta la cruz en el momento de la oracion y espera a que termine su ataque.
        private IEnumerator PerformCrossAttack(
            MonkCrossBehaviour cross,
            GameObject player,
            int sequenceId)
        {
            bool alreadyPraying =
                isPraying;

            activeCrossAttacks++;

            prayerHoldUntil =
                Mathf.Max(
                    prayerHoldUntil,
                    Time.time + minimumPrayerDuration
                );

            UpdatePrayerState();

            // Solo la primera cruz de una oracion espera la preparacion; las siguientes salen directo.
            if (!alreadyPraying &&
                prayerWindup > 0f)
            {
                yield return new WaitForSeconds(
                    prayerWindup
                );
            }

            if (sequenceId != attackSequenceId)
                yield break;

            if (CanLaunchCross(cross, player))
            {
                // La corrutina corre en la cruz para que CancelCurrentAttack la detenga de verdad.
                cross.StartCoroutine(
                    cross.AttackPlayer(
                        player
                    )
                );

                while (!isDead &&
                       sequenceId == attackSequenceId &&
                       cross != null &&
                       cross.IsAttacking)
                {
                    yield return null;
                }
            }

            if (sequenceId != attackSequenceId)
                yield break;

            activeCrossAttacks =
                Mathf.Max(
                    0,
                    activeCrossAttacks - 1
                );
        }

        // Comprueba que la cruz siga en condiciones de atacar despues de la preparacion.
        private bool CanLaunchCross(
            MonkCrossBehaviour cross,
            GameObject player)
        {
            if (isDead)
                return false;

            if (cross == null ||
                !cross.isActiveAndEnabled)
            {
                return false;
            }

            if (cross.IsDead ||
                cross.IsAttacking)
            {
                return false;
            }

            if (player == null)
                return false;

            return cross.CanAttackPlayer(player);
        }

        // Selecciona la siguiente cruz en el orden definido para su ataque autonomo.
        private MonkCrossBehaviour GetNextCrossForTurn(
            GameObject player)
        {
            if (crosses.Count == 0)
                return null;

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
                    !cross.IsAttacking &&
                    cross.CanAttackPlayer(player))
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

        // Verifica si alguna cruz poseida puede atacar al jugador desde su posicion actual.
        private bool HasLaunchablePossessedCross(
            GameObject player)
        {
            if (!HasPossessedCross())
                return false;

            for (int i = 0;
                 i < crosses.Count;
                 i++)
            {
                MonkCrossBehaviour cross =
                    crosses[i];

                if (cross == null)
                    continue;

                if (cross.IsDead ||
                    !cross.IsPossessed)
                {
                    continue;
                }

                if (cross.CanAttackPlayer(player))
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

                bool teleported =
                    TeleportToCross(
                        teleportCross
                    );

                // Queda quieto mientras sale del suelo para no saltar a medio aparecer.
                if (teleported)
                {
                    StopMovementFor(
                        Mathf.Max(
                            stunDurationAfterDamage,
                            appearanceLockDuration
                        )
                    );
                }
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
        private bool TeleportToCross(
            MonkCrossBehaviour cross)
        {
            if (cross == null ||
                Grid == null)
            {
                return false;
            }

            Vector2Int destinationCell =
                cross.CurrentCell;

            cross.ReleaseOccupiedCell();

            // Mueve la ocupacion del Monk a la celda de la cruz; si no se puede, no se teletransporta.
            if (!ClaimGridCell(destinationCell))
            {
                Debug.LogWarning(
                    $"El Monk no pudo teletransportarse a {destinationCell}: la celda esta ocupada.",
                    this
                );

                return false;
            }

            Vector3 destination =
                Grid.CellToWorldCenter(
                    destinationCell,
                    0f
                );

            CancelVisualMovement();
            ResetVisualPosition();

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

            // El teletransporte no cuenta como desplazamiento para la orientacion del modelo.
            lastRootPosition =
                transform.position;

            PlayAppearAnimation();

            return true;
        }

        // Devuelve el modelo a su posicion local original si el salto entre celdas quedo a medias.
        private void ResetVisualPosition()
        {
            if (visualModel == null ||
                visualModel == transform)
            {
                return;
            }

            visualModel.localPosition =
                initialAnimatorLocalPos;
        }

        // Reproduce la aparicion en el mismo frame del teletransporte para que no se vea un salto.
        private void PlayAppearAnimation()
        {
            InterruptPrayer();

            FireAnimatorTrigger(
                hurtTriggerAfterWarp
            );
        }

        // Devuelve inmediatamente todas las cruces vivas al estado de posesion.
        private void RecallAllLivingCrosses()
        {
            // Invalida cualquier corrutina de ataque anterior aunque siga en ejecucion.
            attackSequenceId++;

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

            InterruptPrayer();

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
            attackSequenceId++;

            StopAllCoroutines();

            autonomousCrossRoutine = null;
            launchRoutine = null;
            initialLaunchRunning = false;

            InterruptPrayer();
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
