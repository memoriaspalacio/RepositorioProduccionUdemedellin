using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace UdeM.Characters
{
    public class AngelBehaviour : Character3DNavMeshGridNPCBehaviour
    {
        // Guarda todos los angeles activos que pertenecen al sistema grupal.
        private static readonly List<AngelBehaviour> allAngels =
        new List<AngelBehaviour>();

        // Guarda los angeles que todavia no han atacado durante la ronda actual.
        private static readonly List<AngelBehaviour> turnPool =
            new List<AngelBehaviour>();

        // Guarda temporalmente los candidatos disponibles para una nueva tanda.
        private static readonly List<AngelBehaviour> batchCandidates =
            new List<AngelBehaviour>();

        // Guarda la referencia compartida al jugador despues de iniciar el combate.
        private static GameObject sharedPlayer;

        // Guarda el angel que fue golpeado y no puede participar en la primera tanda.
        private static AngelBehaviour firstExcludedAngel;

        // Indica si el grupo de angeles ya entro en combate.
        private static bool combatActive = false;

        // Indica si el grupo ya completo su unica oleada de ataques por provocacion del jugador.
        private static bool groupAttackCycleComplete = false;

        // Indica si existe actualmente una tanda de ataque activa.
        private static bool batchActive = false;

        // Indica si ya existe una nueva tanda programada.
        private static bool batchScheduled = false;

        // Indica si todavia debe aplicarse la exclusion de la primera tanda.
        private static bool firstBatchPending = false;

        // Guarda cuantos angeles de la tanda actual todavia no terminan su ataque.
        private static int activeAttackers = 0;

        // Indica si este angel se encuentra muerto.
        private bool isDead = false;

        // Indica si este angel ya dejo de patrullar para participar en el combate.
        private bool lockedInCombat = false;

        // Indica si este angel pertenece actualmente a una tanda de ataque.
        private bool isCurrentBatchMember = false;

        // Indica si este angel ya golpeo al jugador durante su carga actual.
        private bool playerHitThisCharge = false;

        // Indica si este angel ya completo su unico ataque de combate grupal.
        private bool hasCompletedAttack = false;

        // Guarda la ultima celda del jugador registrada al comenzar la recarga.
        private Vector2Int recordedPlayerCell;

        // Guarda el Animator usado por las animaciones del angel.
        [SerializeField] private Animator animator;

        // Guarda el nombre del trigger usado para preparar el ataque.
        [SerializeField] private string reloadTrigger = "onReload";

        // Guarda el nombre del trigger usado para comenzar la carga.
        [SerializeField] private string attackTrigger = "onAttack";

        // Define cuanto tiempo recarga el angel antes de iniciar su carga.
        [Min(0f)]
        [SerializeField] private float reloadTime = 0.75f;

        // Define aproximadamente cuanto tarda el angel en recorrer una celda.
        [Min(0.01f)]
        [SerializeField] private float chargeTimePerCell = 0.12f;

        // Define el dano realizado cuando el angel impacta al jugador.
        [Min(0f)]
        [SerializeField] private float attackDamage = 5f;

        // Define cuanto espera el grupo antes de comenzar la siguiente tanda.
        [Min(0f)]
        [SerializeField] private float timeBetweenBatches = 0.75f;

        // Guarda la referencia al componente de vida del jugador.
        [SerializeField] private PlayerHealth playerHealth;

        // Reinicia todas las variables compartidas cuando comienza una nueva ejecucion.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSharedState()
        {
            allAngels.Clear();
            turnPool.Clear();
            batchCandidates.Clear();

            sharedPlayer = null;
            firstExcludedAngel = null;

            combatActive = false;
            batchActive = false;
            batchScheduled = false;
            firstBatchPending = false;
            groupAttackCycleComplete = false;

            activeAttackers = 0;
        }

        // Registra este angel dentro del sistema compartido antes de comenzar.
        protected override void Awake()
        {
            base.Awake();

            if (!allAngels.Contains(this))
                allAngels.Add(this);
        }

        // Configura las referencias necesarias al comenzar el comportamiento.
        protected override void Start()
        {
            base.Start();

            if (animator == null)
                animator = GetComponentInChildren<Animator>();
        }

        // Ignora la deteccion normal para impedir que el angel persiga al jugador.
        public override void PlayerDetected(GameObject detectedTarget)
        {
        }

        // Ignora la perdida de vision porque el combate depende del sistema grupal.
        public override void PlayerLost(GameObject lostTarget)
        {
        }

        // Activa el combate grupal cuando este angel es atacado por el jugador.
        public void NotifyAngelAttacked(GameObject attacker)
        {
            if (isDead)
                return;

            GameObject player =
                GetPlayerFromAttacker(
                    attacker
                );

            if (player == null)
                return;

            sharedPlayer = player;

            playerHealth =
                ResolvePlayerHealth(
                    player
                );

            RestartGroupAttackCycle(
                this
            );
        }

        // Activa el combate grupal buscando automaticamente al objeto con tag Player.
        public void NotifyAngelAttacked()
        {
            if (isDead)
                return;

            GameObject player =
                GameObject.FindGameObjectWithTag(
                    "Player"
                );

            NotifyAngelAttacked(
                player
            );
        }

        // Obtiene al jugador desde el atacante o lo busca automaticamente en la escena.
        private GameObject GetPlayerFromAttacker(GameObject attacker)
        {
            if (attacker != null &&
                attacker.CompareTag("Player"))
            {
                return attacker;
            }

            GameObject player =
                GameObject.FindGameObjectWithTag(
                    "Player"
                );

            return player;
        }

        // Resuelve el PlayerHealth correcto cuando hay mas de uno en el jugador.
        private PlayerHealth ResolvePlayerHealth(GameObject player)
        {
            if (player == null)
                return playerHealth;

            if (playerHealth != null &&
                playerHealth.gameObject == player)
            {
                return playerHealth;
            }

            PlayerHealth[] healthComponents =
                player.GetComponents<PlayerHealth>();

            for (int i = 0;
                 i < healthComponents.Length;
                 i++)
            {
                PlayerHealth candidate =
                    healthComponents[i];

                if (candidate == null)
                    continue;

                if (candidate.lifeBar != null)
                    return candidate;
            }

            if (healthComponents.Length > 0)
                return healthComponents[0];

            return null;
        }

        // Reinicia la oleada grupal cuando el jugador golpea a cualquier angel.
        private static void RestartGroupAttackCycle(
            AngelBehaviour struckAngel)
        {
            if (struckAngel == null ||
                struckAngel.isDead)
            {
                return;
            }

            batchActive = false;
            batchScheduled = false;
            activeAttackers = 0;
            groupAttackCycleComplete = false;
            combatActive = true;

            firstExcludedAngel = struckAngel;
            firstBatchPending = true;

            batchCandidates.Clear();
            turnPool.Clear();

            for (int i = 0;
                 i < allAngels.Count;
                 i++)
            {
                AngelBehaviour angel =
                    allAngels[i];

                if (angel == null)
                    continue;

                if (angel.isDead)
                    continue;

                angel.StopAllCoroutines();
                angel.isCurrentBatchMember = false;
                angel.playerHitThisCharge = false;
                angel.hasCompletedAttack = false;
            }

            RefillTurnPool();

            ScheduleNextBatch(
                0f
            );
        }

        // Comprueba si todos los angeles vivos ya realizaron su ataque de combate.
        private static bool AllLivingAngelsHaveCompletedAttack()
        {
            bool anyLiving = false;

            for (int i = 0;
                 i < allAngels.Count;
                 i++)
            {
                AngelBehaviour angel =
                    allAngels[i];

                if (angel == null)
                    continue;

                if (angel.isDead)
                    continue;

                if (!angel.isActiveAndEnabled)
                    continue;

                anyLiving = true;

                if (!angel.hasCompletedAttack)
                    return false;
            }

            return anyLiving;
        }

        // Cierra el combate grupal cuando ya no quedan angeles pendientes de atacar.
        private static void TryConcludeGroupCombat()
        {
            if (!AllLivingAngelsHaveCompletedAttack())
                return;

            groupAttackCycleComplete = true;
            combatActive = false;
            batchActive = false;
            batchScheduled = false;
            firstBatchPending = false;
            firstExcludedAngel = null;
        }

        // Llena nuevamente la ronda con todos los angeles vivos y disponibles.
        private static void RefillTurnPool()
        {
            turnPool.Clear();

            for (int i = 0;
                 i < allAngels.Count;
                 i++)
            {
                AngelBehaviour angel =
                    allAngels[i];

                if (angel == null)
                    continue;

                if (angel.isDead)
                    continue;

                if (!angel.isActiveAndEnabled)
                    continue;

                if (angel.hasCompletedAttack)
                    continue;

                turnPool.Add(
                    angel
                );
            }
        }

        // Elimina de la ronda cualquier angel destruido, muerto o desactivado.
        private static void RemoveInvalidAngelsFromPool()
        {
            for (int i = turnPool.Count - 1;
                 i >= 0;
                 i--)
            {
                AngelBehaviour angel =
                    turnPool[i];

                if (angel == null ||
                    angel.isDead ||
                    !angel.isActiveAndEnabled ||
                    angel.hasCompletedAttack)
                {
                    turnPool.RemoveAt(
                        i
                    );
                }
            }
        }

        // Busca un angel vivo que pueda coordinar el comienzo de la siguiente tanda.
        private static AngelBehaviour GetLivingCoordinator()
        {
            for (int i = 0;
                 i < allAngels.Count;
                 i++)
            {
                AngelBehaviour angel =
                    allAngels[i];

                if (angel == null)
                    continue;

                if (angel.isDead)
                    continue;

                if (!angel.isActiveAndEnabled)
                    continue;

                return angel;
            }

            return null;
        }

        // Programa la siguiente tanda usando un angel vivo como coordinador.
        private static void ScheduleNextBatch(float delay)
        {
            if (!combatActive)
                return;

            if (batchActive)
                return;

            if (batchScheduled)
                return;

            AngelBehaviour coordinator =
                GetLivingCoordinator();

            if (coordinator == null)
                return;

            batchScheduled = true;

            coordinator.StartCoroutine(
                coordinator.StartNextBatchAfterDelay(
                    delay
                )
            );
        }

        // Espera el intervalo indicado antes de comenzar la siguiente tanda.
        private IEnumerator StartNextBatchAfterDelay(float delay)
        {
            if (delay > 0f)
            {
                yield return new WaitForSeconds(
                    delay
                );
            }

            batchScheduled = false;

            BeginNextBatch();
        }

        // Selecciona aleatoriamente entre uno y tres angeles para la siguiente tanda.
        private static void BeginNextBatch()
        {
            if (!combatActive)
                return;

            if (sharedPlayer == null)
                return;

            RemoveInvalidAngelsFromPool();

            if (turnPool.Count == 0)
            {
                TryConcludeGroupCombat();
                return;
            }

            BuildBatchCandidates();

            if (batchCandidates.Count == 0)
                return;

            int requestedAttackers =
                Random.Range(
                    1,
                    4
                );

            int attackerCount =
                Mathf.Min(
                    requestedAttackers,
                    batchCandidates.Count
                );

            batchActive = true;
            activeAttackers = attackerCount;

            for (int i = 0;
                 i < attackerCount;
                 i++)
            {
                int randomIndex =
                    Random.Range(
                        0,
                        batchCandidates.Count
                    );

                AngelBehaviour selectedAngel =
                    batchCandidates[randomIndex];

                batchCandidates.RemoveAt(
                    randomIndex
                );

                turnPool.Remove(
                    selectedAngel
                );

                selectedAngel.BeginAttackTurn(
                    sharedPlayer
                );
            }

            if (firstBatchPending)
            {
                firstBatchPending = false;
                firstExcludedAngel = null;
            }

            batchCandidates.Clear();
        }

        // Construye la lista disponible respetando la exclusion de la primera tanda.
        private static void BuildBatchCandidates()
        {
            batchCandidates.Clear();

            for (int i = 0;
                 i < turnPool.Count;
                 i++)
            {
                AngelBehaviour angel =
                    turnPool[i];

                if (angel == null)
                    continue;

                if (firstBatchPending &&
                    angel == firstExcludedAngel)
                {
                    continue;
                }

                batchCandidates.Add(
                    angel
                );
            }

            if (batchCandidates.Count > 0)
                return;

            for (int i = 0;
                 i < turnPool.Count;
                 i++)
            {
                AngelBehaviour angel =
                    turnPool[i];

                if (angel == null)
                    continue;

                batchCandidates.Add(
                    angel
                );
            }
        }

        // Comienza el turno del angel y registra en ese instante la celda del jugador.
        private void BeginAttackTurn(GameObject player)
        {
            if (isDead ||
                hasCompletedAttack)
            {
                ReportAttackFinished();
                return;
            }

            if (player == null)
            {
                ReportAttackFinished();
                return;
            }

            isCurrentBatchMember = true;
            playerHitThisCharge = false;

            recordedPlayerCell =
                Grid.WorldToCell(
                    player.transform.position
                );

            playerHealth =
                ResolvePlayerHealth(
                    player
                );

            if (!lockedInCombat)
            {
                lockedInCombat = true;
                behaviourEnabled = false;
            }

            if (_navigator != null &&
                _navigator.isOnNavMesh)
            {
                _navigator.ResetPath();
            }

            StartCoroutine(
                ReloadAndCharge(
                    player
                )
            );
        }

        // Ejecuta la animacion de recarga antes de comenzar la carga hacia el objetivo.
        private IEnumerator ReloadAndCharge(GameObject player)
        {
            if (animator != null)
                animator.SetTrigger(reloadTrigger);

            yield return new WaitForSeconds(
                reloadTime
            );

            if (isDead)
            {
                ReportAttackFinished();
                yield break;
            }

            if (animator != null)
                animator.SetTrigger(attackTrigger);

            yield return StartCoroutine(
                ChargeToRecordedCell(
                    player
                )
            );

            ReportAttackFinished();
        }

        // Realiza una carga recta hacia la celda registrada sin actualizar el objetivo.
        private IEnumerator ChargeToRecordedCell(GameObject player)
        {
            Vector2Int startCell =
                Grid.WorldToCell(
                    transform.position
                );

            Vector2Int destinationCell =
                GetSafeDestinationCell(
                    startCell,
                    recordedPlayerCell,
                    player
                );

            Vector3 startPosition =
                transform.position;

            Vector3 targetPosition =
                Grid.CellToWorldCenter(
                    destinationCell,
                    0f
                );

            Vector3 chargeDirection =
                targetPosition - startPosition;

            chargeDirection.y = 0f;

            if (chargeDirection.sqrMagnitude > 0.001f)
            {
                transform.rotation =
                    Quaternion.LookRotation(
                        chargeDirection.normalized,
                        Vector3.up
                    );
            }

            float worldDistance =
                Vector3.Distance(
                    startPosition,
                    targetPosition
                );

            float cellDistance =
                worldDistance /
                Mathf.Max(
                    Grid.CellSize,
                    0.01f
                );

            float chargeDuration =
                Mathf.Max(
                    chargeTimePerCell,
                    cellDistance * chargeTimePerCell
                );

            ReleaseGridOccupancy();

            bool navigatorWasEnabled =
                _navigator != null &&
                _navigator.enabled;

            if (navigatorWasEnabled)
            {
                if (_navigator.isOnNavMesh)
                    _navigator.ResetPath();

                _navigator.enabled = false;
            }

            float elapsedTime = 0f;

            while (elapsedTime < chargeDuration)
            {
                if (isDead)
                    break;

                elapsedTime += Time.deltaTime;

                float progress =
                    Mathf.Clamp01(
                        elapsedTime / chargeDuration
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

            if (!isDead)
            {
                transform.position =
                    targetPosition;

                CheckPlayerImpact(
                    player
                );

                TryClaimDestinationCell(
                    destinationCell
                );
            }

            if (_navigator != null)
            {
                _navigator.ResetPath();
                _navigator.isStopped = true;

                if (lockedInCombat)
                {
                    _navigator.enabled = false;
                }
                else if (navigatorWasEnabled)
                {
                    _navigator.enabled = true;

                    if (NavMesh.SamplePosition(
                            transform.position,
                            out NavMeshHit hit,
                            Grid.CellSize,
                            NavMesh.AllAreas))
                    {
                        _navigator.Warp(
                            hit.position
                        );
                    }
                }
            }
        }

        // Calcula la celda final evitando que el angel termine encima del jugador.
        private Vector2Int GetSafeDestinationCell(
            Vector2Int startCell,
            Vector2Int targetCell,
            GameObject player)
        {
            if (player == null)
                return targetCell;

            Vector2Int currentPlayerCell =
                Grid.WorldToCell(
                    player.transform.position
                );

            if (currentPlayerCell != targetCell)
                return GetAvailableCellOrSelf(targetCell);

            if (startCell == targetCell)
                return GetAvailableCellOrSelf(startCell);

            Vector3 startWorld =
                Grid.CellToWorldCenter(
                    startCell,
                    0f
                );

            Vector3 targetWorld =
                Grid.CellToWorldCenter(
                    targetCell,
                    0f
                );

            Vector3 backwardDirection =
                startWorld - targetWorld;

            backwardDirection.y = 0f;

            if (backwardDirection.sqrMagnitude <= 0.001f)
                return startCell;

            backwardDirection.Normalize();

            float searchStep =
                Mathf.Max(
                    0.05f,
                    Grid.CellSize * 0.1f
                );

            float searchDistance =
                searchStep;

            float maximumSearchDistance =
                Grid.CellSize * 2f;

            while (searchDistance <= maximumSearchDistance)
            {
                Vector3 testPosition =
                    targetWorld +
                    backwardDirection * searchDistance;

                Vector2Int testCell =
                    Grid.WorldToCell(
                        testPosition
                    );

                if (testCell != targetCell &&
                    Grid.IsCellInside(testCell) &&
                    !Grid.IsCellOccupiedByOther(
                        testCell,
                        gameObject))
                {
                    return testCell;
                }

                searchDistance +=
                    searchStep;
            }

            return GetAvailableCellOrSelf(startCell);
        }

        // Devuelve la celda pedida si esta libre; si no, busca una vecina disponible.
        private Vector2Int GetAvailableCellOrSelf(Vector2Int preferredCell)
        {
            if (!Grid.IsCellOccupiedByOther(
                    preferredCell,
                    gameObject))
            {
                return preferredCell;
            }

            Vector2Int[] offsets =
            {
                Vector2Int.up,
                Vector2Int.down,
                Vector2Int.left,
                Vector2Int.right
            };

            for (int i = 0;
                 i < offsets.Length;
                 i++)
            {
                Vector2Int candidate =
                    preferredCell + offsets[i];

                if (!Grid.IsCellInside(candidate))
                    continue;

                if (Grid.IsCellOccupiedByOther(
                        candidate,
                        gameObject))
                {
                    continue;
                }

                return candidate;
            }

            return preferredCell;
        }

        // Intenta registrar la celda final evitando solaparse con otros angeles.
        private void TryClaimDestinationCell(Vector2Int preferredCell)
        {
            if (ClaimGridCell(preferredCell))
                return;

            Vector2Int fallback =
                GetAvailableCellOrSelf(preferredCell);

            if (fallback == preferredCell)
            {
                Debug.LogWarning(
                    $"El angel termino en {preferredCell} pero no pudo registrar la celda.",
                    this
                );

                return;
            }

            Vector3 fallbackPosition =
                Grid.CellToWorldCenter(
                    fallback,
                    0f
                );

            transform.position =
                fallbackPosition;

            if (!ClaimGridCell(fallback))
            {
                Debug.LogWarning(
                    $"El angel no pudo registrar la celda alternativa {fallback}.",
                    this
                );
            }
        }

        // Comprueba mediante las celdas si el angel impacto al jugador durante la carga.
        private void CheckPlayerImpact(GameObject player)
        {
            if (playerHitThisCharge)
                return;

            if (player == null)
                return;

            Vector2Int angelCell =
                Grid.WorldToCell(
                    transform.position
                );

            Vector2Int playerCell =
                Grid.WorldToCell(
                    player.transform.position
                );

            int cellDistance =
                Mathf.Abs(angelCell.x - playerCell.x) +
                Mathf.Abs(angelCell.y - playerCell.y);

            if (cellDistance == 0)
            {
                ApplyChargeDamage(player);
                return;
            }

            if (cellDistance != 1 ||
                playerCell != recordedPlayerCell)
            {
                return;
            }

            ApplyChargeDamage(player);
        }

        // Aplica el dano de la carga una sola vez por turno.
        private void ApplyChargeDamage(GameObject player)
        {
            if (playerHitThisCharge)
                return;

            playerHitThisCharge = true;

            PlayerHealth health =
                ResolvePlayerHealth(
                    player
                );

            if (health != null)
                health.Damage(attackDamage);

            Vector2Int playerCell =
                Grid.WorldToCell(
                    player.transform.position
                );

            Debug.Log(
                $"El angel impacto al jugador en la celda {playerCell}.",
                this
            );
        }

        // Informa al sistema grupal que este angel termino su ataque actual.
        private void ReportAttackFinished()
        {
            if (!isCurrentBatchMember)
                return;

            isCurrentBatchMember = false;
            hasCompletedAttack = true;

            turnPool.Remove(
                this
            );

            activeAttackers =
                Mathf.Max(
                    0,
                    activeAttackers - 1
                );

            if (activeAttackers > 0)
                return;

            batchActive = false;

            ScheduleNextBatch(
                timeBetweenBatches
            );
        }

        // Detiene completamente a este angel cuando muere.
        public void SetDead()
        {
            if (isDead)
                return;

            bool wasCurrentAttacker =
                isCurrentBatchMember;

            isDead = true;
            behaviourEnabled = false;

            StopAllCoroutines();

            ReleaseGridOccupancy();

            turnPool.Remove(
                this
            );

            batchCandidates.Remove(
                this
            );

            if (firstExcludedAngel == this)
                firstExcludedAngel = null;

            if (!wasCurrentAttacker)
                return;

            isCurrentBatchMember = false;

            activeAttackers =
                Mathf.Max(
                    0,
                    activeAttackers - 1
                );

            if (activeAttackers > 0)
                return;

            batchActive = false;

            ScheduleNextBatch(
                timeBetweenBatches
            );
        }

        // Desactiva el ataque normal heredado porque el angel utiliza una carga propia.
        protected override bool Attack(
            Vector2Int direction,
            Vector2Int targetCell)
        {
            return false;
        }

        // Elimina este angel de todos los registros cuando su objeto es destruido.
        protected override void OnDestroy()
        {
            ReleaseGridOccupancy();

            allAngels.Remove(
                this
            );

            turnPool.Remove(
                this
            );

            batchCandidates.Remove(
                this
            );

            if (firstExcludedAngel == this)
                firstExcludedAngel = null;

            if (isCurrentBatchMember)
            {
                isCurrentBatchMember = false;

                activeAttackers =
                    Mathf.Max(
                        0,
                        activeAttackers - 1
                    );

                if (activeAttackers == 0)
                {
                    batchActive = false;

                    ScheduleNextBatch(
                        timeBetweenBatches
                    );
                }
            }

            base.OnDestroy();
        }
    }

}