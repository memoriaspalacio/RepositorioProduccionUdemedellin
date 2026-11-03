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

        [Header("Proteccion de las cruces")]
        // Indica si el Monk es invulnerable mientras le quede alguna cruz (los golpes rebotan en su escudo).
        [SerializeField] private bool protectedByCrosses = true;

        // Indica si el Monk baja al infierno y lo invoca otra cruz cada vez que le rompen una.
        [SerializeField] private bool relocateWhenCrossDies = true;

        // Define cuanto tarda el Monk en hundirse en el infierno.
        [Min(0.1f)]
        [SerializeField] private float descentDuration = 1f;

        // Define cuanto tiempo lo invoca la cruz, ya bajo tierra, antes de que salga del suelo.
        [Min(0f)]
        [SerializeField] private float summonDelay = 0.35f;

        // Define cuantos golpes aguanta el Monk cuando ya no le quedan cruces.
        [Min(1)]
        [SerializeField] private int hitsToKillWhenExposed = 3;

        [Header("Efectos Visuales")]
        // Trail logic has been removed.

        // Guarda el prefab utilizado para crear las cruces.
        [SerializeField] private MonkCrossBehaviour crossPrefab;

        // El Monk siempre tiene 4 cruces, una en cada casilla a su alrededor.
        private const int CrossCount = 4;

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

        // Guarda los efectos del infierno (hundirse y salir del suelo).
        private SacerdoteInfierno hellEffect;

        // Guarda el enlace divino con las cruces y el escudo que lo protege.
        private EnlaceDivino divineLink;

        // Indica si el Monk esta bajando al infierno o saliendo en otra cruz.
        private bool relocating = false;

        // Indica si el Monk perdio todas sus cruces y quedo expuesto a los golpes.
        private bool exposed = false;

        // Cuenta los golpes recibidos desde que quedo expuesto.
        private int exposedHitsTaken = 0;

        // Casillas de reserva (diagonales) para las cruces cuando una vecina esta ocupada.
        private readonly Vector2Int[] fallbackDirections =
        {
            new Vector2Int(1, 1),
            new Vector2Int(1, -1),
            new Vector2Int(-1, -1),
            new Vector2Int(-1, 1)
        };

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

            SetupEffects();
            SpawnCrosses();
        }

        // Agrega (si faltan) los efectos del infierno y del enlace divino con las cruces.
        private void SetupEffects()
        {
            // Sin un modelo hijo no se puede hundir: los efectos se quedan sin cuerpo que mover.
            Transform model =
                visualModel != null && visualModel != transform
                    ? visualModel
                    : null;

            hellEffect = GetComponent<SacerdoteInfierno>();
            if (hellEffect == null)
                hellEffect = gameObject.AddComponent<SacerdoteInfierno>();

            hellEffect.Configurar(model);

            divineLink = GetComponent<EnlaceDivino>();
            if (divineLink == null)
                divineLink = gameObject.AddComponent<EnlaceDivino>();

            divineLink.Configurar(
                model,
                crosses
            );
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
            KeepLooseCrossesRegistered();

            if (divineLink != null)
            {
                divineLink.ActualizarFuerza(
                    CountLivingCrosses(),
                    CrossCount
                );
            }
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
                 i < CrossCount;
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

        // Mantiene cada cruz poseida flotando sobre su propia casilla alrededor del Monk.
        // Cada cruz ocupa esa casilla: nadie se le pone encima y el jugador la puede golpear.
        private void UpdatePossessedCrossPositions()
        {
            if (Grid == null)
                return;

            Vector2Int monkCell =
                CurrentGridCell;

            for (int i = 0;
                 i < crosses.Count;
                 i++)
            {
                MonkCrossBehaviour cross =
                    crosses[i];

                if (cross == null)
                    continue;

                if (cross.IsDead || !cross.IsPossessed)
                    continue;

                Vector2Int slot;
                if (!TryAssignCrossSlot(cross, i, monkCell, out slot))
                {
                    // Sin casilla libre alrededor: se queda en la que tenia o, si nunca tuvo, en su direccion.
                    slot = cross.HasPossessedSlot
                        ? cross.PossessedSlot
                        : monkCell + possessedDirections[i % possessedDirections.Length];
                }

                Vector3 slotCenter =
                    Grid.CellToWorldCenter(
                        slot,
                        0f
                    );

                Vector3 targetPosition =
                    new Vector3(
                        slotCenter.x,
                        transform.position.y + possessedCrossHeight,
                        slotCenter.z
                    );

                cross.UpdatePossessedPosition(
                    targetPosition
                );
            }
        }

        // Las cruces sueltas por el mapa siempre deben tener casilla: sin ella son intocables.
        private void KeepLooseCrossesRegistered()
        {
            for (int i = 0;
                 i < crosses.Count;
                 i++)
            {
                MonkCrossBehaviour cross =
                    crosses[i];

                if (cross == null || cross.IsDead)
                    continue;

                cross.EnsureCellRegistered();
            }
        }

        // Busca para la cruz una casilla libre junto al Monk: primero la suya, luego las otras vecinas y al final las diagonales.
        private bool TryAssignCrossSlot(
            MonkCrossBehaviour cross,
            int index,
            Vector2Int monkCell,
            out Vector2Int slot)
        {
            Vector2Int preferred =
                monkCell + possessedDirections[index % possessedDirections.Length];

            // Ya tiene una casilla pegada al Monk: la conserva salvo que pueda volver a la suya.
            if (cross.HasPossessedSlot &&
                cross.OwnsCell(cross.PossessedSlot) &&
                IsAroundCell(cross.PossessedSlot, monkCell))
            {
                if (cross.PossessedSlot != preferred &&
                    cross.TryClaimPossessedSlot(preferred))
                {
                    slot = preferred;
                    return true;
                }

                slot = cross.PossessedSlot;
                return true;
            }

            for (int k = 0;
                 k < possessedDirections.Length;
                 k++)
            {
                Vector2Int candidate =
                    monkCell + possessedDirections[(index + k) % possessedDirections.Length];

                if (cross.TryClaimPossessedSlot(candidate))
                {
                    slot = candidate;
                    return true;
                }
            }

            for (int k = 0;
                 k < fallbackDirections.Length;
                 k++)
            {
                Vector2Int candidate =
                    monkCell + fallbackDirections[(index + k) % fallbackDirections.Length];

                if (cross.TryClaimPossessedSlot(candidate))
                {
                    slot = candidate;
                    return true;
                }
            }

            slot = preferred;
            return false;
        }

        // Indica si la casilla esta pegada a la del Monk (incluidas las diagonales).
        private static bool IsAroundCell(
            Vector2Int cell,
            Vector2Int center)
        {
            Vector2Int difference =
                cell - center;

            return Mathf.Max(Mathf.Abs(difference.x), Mathf.Abs(difference.y)) == 1;
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

            // Bajo tierra o sin cruces no lanza nada; al terminar vuelve a detectar al jugador.
            if (relocating || exposed)
                return;

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

            // La cruz carga su ataque al ritmo de la oracion.
            if (cross != null)
            {
                cross.PrepareAttack(
                    alreadyPraying
                        ? 0.15f
                        : prayerWindup
                );
            }

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

        // Reacciona a una cruz rota: sin cruces el Monk queda expuesto; si quedan, baja al infierno y lo invoca otra.
        public void NotifyCrossDied(
            MonkCrossBehaviour deadCross)
        {
            if (isDead || exposed)
                return;

            // La secuencia en curso revisa al terminar si todavia quedan cruces.
            if (relocating)
                return;

            if (CountLivingCrosses() == 0 &&
                crosses.Count >= CrossCount)
            {
                ExposeMonk();
                return;
            }

            if (relocateWhenCrossDies &&
                TryStartRelocation())
            {
                return;
            }

            RecallAllLivingCrosses();
        }

        // Reacciona al dano recibido bajando al infierno y saliendo en una cruz.
        // Con la proteccion de las cruces activa los golpes rebotan (BlockHit) y no llegan aqui.
        public void NotifyMonkDamaged()
        {
            if (isDead || relocating)
                return;

            StopMovementFor(stunDurationAfterDamage);

            if (!TryStartRelocation())
                RecallAllLivingCrosses();
        }

        // Bloquea el golpe mientras alguna cruz lo proteja. Devuelve true si el golpe no le hace nada.
        public bool BlockHit(Vector3? attackerPosition)
        {
            if (isDead)
                return false;

            // Bajo tierra los golpes no cuentan.
            if (relocating)
                return true;

            // Sin cruces que lo protejan, los golpes entran: los primeros lo aturden y el ultimo lo mata.
            if (exposed)
                return !TakeExposedHit(attackerPosition);

            if (!protectedByCrosses)
                return false;

            if (CountLivingCrosses() == 0)
                return false;

            if (divineLink != null)
                divineLink.Bloquear(attackerPosition);

            return true;
        }

        // Cuenta las cruces que siguen vivas.
        private int CountLivingCrosses()
        {
            int living = 0;

            for (int i = 0;
                 i < crosses.Count;
                 i++)
            {
                if (crosses[i] != null &&
                    !crosses[i].IsDead)
                {
                    living++;
                }
            }

            return living;
        }

        // Empieza a bajar al infierno si hay una cruz suelta que lo pueda invocar.
        private bool TryStartRelocation()
        {
            if (isDead || exposed || relocating || Grid == null)
                return false;

            MonkCrossBehaviour summoner =
                GetRandomUnpossessedCross();

            if (summoner == null)
                return false;

            StartCoroutine(
                RelocationSequence(
                    summoner
                )
            );

            return true;
        }

        // Se hunde en el infierno, la cruz lo invoca y sale del suelo en su celda.
        private IEnumerator RelocationSequence(
            MonkCrossBehaviour summoner)
        {
            relocating = true;

            StopCrossAttacks();
            CancelVisualMovement();
            ResetVisualPosition();

            StopMovementFor(
                descentDuration +
                summonDelay +
                appearanceLockDuration +
                0.2f
            );

            if (divineLink != null)
                divineLink.MostrarEscudo(false);

            // La cruz empieza a invocarlo mientras el todavia se hunde.
            summoner.StartSummon();

            if (hellEffect != null)
                hellEffect.PrepararInvocacion(summoner.transform.position);

            if (hellEffect != null)
                yield return hellEffect.Hundirse(descentDuration);
            else
                yield return new WaitForSeconds(descentDuration);

            if (summonDelay > 0f)
                yield return new WaitForSeconds(summonDelay);

            // Si rompieron la cruz que lo invocaba, lo invoca otra; si no queda ninguna suelta, sale donde estaba.
            if (summoner == null ||
                summoner.IsDead ||
                summoner.IsPossessed)
            {
                if (summoner != null)
                    summoner.EndSummon();

                summoner = GetRandomUnpossessedCross();
            }

            if (summoner != null)
                TeleportToCross(summoner);

            // Las cruces vuelven volando a rodearlo mientras sale del suelo.
            RecallAllLivingCrosses();
            PlayAppearAnimation();

            if (summoner != null)
                summoner.EndSummon();

            if (divineLink != null)
                divineLink.MostrarEscudo(true);

            if (hellEffect != null)
                yield return hellEffect.Emerger(appearanceLockDuration);
            else
                yield return new WaitForSeconds(appearanceLockDuration);

            relocating = false;

            if (CountLivingCrosses() == 0 &&
                crosses.Count >= CrossCount)
            {
                ExposeMonk();
            }
        }

        // Sin cruces se rompe la proteccion: el Monk queda expuesto hasta que el jugador lo golpee.
        private void ExposeMonk()
        {
            if (isDead || exposed)
                return;

            exposed = true;

            StopCrossAttacks();

            if (divineLink != null)
                divineLink.Romper();

            // El efecto de muerte copia el cuerpo: tiene que estar visible y sin la capa de fuego.
            if (hellEffect != null)
                hellEffect.Restaurar();
        }

        // Cuenta un golpe ya expuesto. Devuelve true si es el golpe que lo mata: EnemyHealth
        // sigue con el dano y la muerte. Los anteriores solo lo aturden y lo hacen parpadear.
        private bool TakeExposedHit(Vector3? attackerPosition)
        {
            exposedHitsTaken++;

            EnemyHealth health =
                GetComponent<EnemyHealth>();

            if (exposedHitsTaken >= hitsToKillWhenExposed)
            {
                if (health != null)
                {
                    health.currentLife =
                        Mathf.Min(
                            health.currentLife,
                            0f
                        );
                }

                return true;
            }

            StopMovementFor(stunDurationAfterDamage);

            MinecraftHitFeedback hitFeedback =
                GetComponent<MinecraftHitFeedback>();

            if (hitFeedback == null)
                hitFeedback = gameObject.AddComponent<MinecraftHitFeedback>();

            if (attackerPosition.HasValue)
                hitFeedback.PlayHitFeedbackFromAttacker(attackerPosition.Value);
            else
                hitFeedback.PlayHitFeedback();

            return false;
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

        // Corta los ataques en curso (secuencias, cruces en vuelo y oracion) sin recuperar las cruces.
        private void StopCrossAttacks()
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

            initialLaunchRunning = false;

            InterruptPrayer();
        }

        // Devuelve todas las cruces vivas al estado de posesion; las sueltas vuelven volando.
        private void RecallAllLivingCrosses()
        {
            StopCrossAttacks();

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
            relocating = false;

            InterruptPrayer();
            StopAllCrossAttacks();
        }

        // Al destruir al Monk se van con el las cruces que quedaron sueltas por el mapa.
        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (!gameObject.scene.isLoaded)
                return;

            for (int i = 0;
                 i < crosses.Count;
                 i++)
            {
                MonkCrossBehaviour cross =
                    crosses[i];

                if (cross == null)
                    continue;

                if (cross.transform.IsChildOf(transform))
                    continue;

                Destroy(cross.gameObject);
            }
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
