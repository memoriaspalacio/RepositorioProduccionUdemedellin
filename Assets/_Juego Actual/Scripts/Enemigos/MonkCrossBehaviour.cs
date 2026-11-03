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

        // Define la altura del salto de la cruz al moverse.
        [Min(0f)]
        [SerializeField] private float attackHopHeight = 0.6f;

        // Define la distancia minima en celdas necesaria para iniciar un ataque.
        [Min(2)]
        [SerializeField] private int minimumAttackCellDistance = 2;

        // Define cuanto se eleva visualmente la cruz respecto a la altura del Monk.
        [SerializeField] private float unpossessedHeightOffset = 0.5f;

        // Define la velocidad usada para rotar mientras la cruz esta poseida.
        [SerializeField] private float possessedRotationSpeed = 90f;

        // Guarda la referencia al sistema de vida del jugador.
        [SerializeField] private PlayerHealth playerHealth;

        [Header("Efectos Visuales")]
        [SerializeField] private Renderer crossRenderer;
        [SerializeField] private Color teleportHighlightColor = Color.cyan;

        // Define cuanto tarda la cruz en volver volando junto al Monk al ser recuperada.
        [Min(0.05f)]
        [SerializeField] private float returnDuration = 0.45f;

        // Define cuanto se inclina la cruz hacia adelante mientras vuela hacia el jugador.
        [Range(0f, 60f)]
        [SerializeField] private float flightTiltAngle = 25f;

        // Guarda el componente que anima la cruz (aparecer, flotar, cargar, volar, romperse).
        private MonkCrossVisual visual;

        // Indica si la cruz esta volviendo hacia el Monk despues de ser recuperada.
        private bool returning = false;

        // Guarda desde donde y cuando empezo la vuelta hacia el Monk.
        private Vector3 returnStartPosition;
        private float returnStartTime;

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

        // Expone si la cruz sigue volviendo hacia el Monk (todavia no puede atacar).
        public bool IsReturning =>
            returning;

        // Expone si la cruz poseida tiene registrada su casilla alrededor del Monk.
        public bool HasPossessedSlot =>
            IsPossessed &&
            cellRegistered;

        // Expone la casilla que ocupa la cruz poseida junto al Monk.
        public Vector2Int PossessedSlot =>
            currentCell;

        // Expone el centro visible de la cruz, usado por el enlace divino con el Monk.
        public Vector3 CentroVisual =>
            visual != null
                ? visual.Centro
                : transform.position;

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

            // Sus casillas no bloquean al Monk: puede caminar donde esta su propia cruz.
            grid.SetOccupantOwner(
                gameObject,
                monk.gameObject
            );

            // Convierte todos los colliders en triggers para que no levanten fisicamente a los personajes
            Collider[] colliders = GetComponentsInChildren<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].isTrigger = true;
            }

            visual = GetComponent<MonkCrossVisual>();
            if (visual == null)
                visual = gameObject.AddComponent<MonkCrossVisual>();

            visual.Inicializar(index);
            visual.AlAparecer();
        }

        // Actualiza la posicion y celda logica mientras la cruz permanece poseida.
        public void UpdatePossessedPosition(
            Vector3 position)
        {
            if (!IsPossessed)
                return;

            if (grid == null)
                return;

            if (returning)
            {
                // Vuelve en arco hacia su puesto alrededor del Monk.
                float progress =
                    Mathf.Clamp01(
                        (Time.time - returnStartTime) /
                        returnDuration
                    );

                float eased =
                    1f - Mathf.Pow(1f - progress, 3f);

                transform.position =
                    Vector3.Lerp(
                        returnStartPosition,
                        position,
                        eased
                    ) +
                    Vector3.up * (Mathf.Sin(progress * Mathf.PI) * 0.8f);

                if (progress >= 1f)
                {
                    returning = false;

                    if (visual != null)
                        visual.AlTerminarVuelta();
                }
            }
            else
            {
                // Sigue suave a su casilla cuando el Monk se mueve o cambia de puesto.
                transform.position =
                    Vector3.Lerp(
                        transform.position,
                        position,
                        1f - Mathf.Exp(-14f * Time.deltaTime)
                    );
            }

            // Con casilla registrada la logica usa esa casilla, no la posicion visual.
            if (!cellRegistered)
            {
                currentCell =
                    grid.WorldToCell(
                        transform.position
                    );
            }

            float rotationMultiplier =
                visual != null
                    ? visual.MultiplicadorGiro
                    : 1f;

            transform.Rotate(
                Vector3.up,
                possessedRotationSpeed *
                rotationMultiplier *
                Time.deltaTime,
                Space.World
            );
        }

        // Comprueba si existe suficiente distancia en celdas para permitir un ataque.
        public bool CanAttackPlayer(
            GameObject player)
        {
            if (IsDead)
                return false;

            // Mientras vuelve hacia el Monk todavia no esta lista para atacar.
            if (returning)
                return false;

            if (player == null)
                return false;

            if (grid == null)
                return false;

            Vector2Int playerCell =
                grid.WorldToCell(
                    player.transform.position
                );

            Vector2Int attackOriginCell =
                currentCell;

            if (IsPossessed &&
                owner != null &&
                !cellRegistered)
            {
                attackOriginCell =
                    grid.WorldToCell(
                        transform.position
                    );
            }

            int deltaX =
                Mathf.Abs(
                    playerCell.x -
                    attackOriginCell.x
                );

            int deltaY =
                Mathf.Abs(
                    playerCell.y -
                    attackOriginCell.y
                );

            int cellDistance =
                Mathf.Max(
                    deltaX,
                    deltaY
                );

            return cellDistance >=
                   minimumAttackCellDistance;
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

            if (!CanAttackPlayer(player))
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

            // Suelta ya no pertenece al Monk: si el pisara su casilla se la quitaria y quedaria
            // sin casilla, imposible de golpear.
            grid.SetOccupantOwner(
                gameObject,
                null
            );

            if (visual != null)
                visual.AlDespegar();

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

                if (visual != null)
                    visual.AlAterrizar();

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

            if (visual != null)
                visual.AlAterrizar();
        }

        // Cambia la cruz a no poseida e intenta registrar su celda actual.
        private bool LeavePossession()
        {
            if (!IsPossessed)
                return true;

            // Si ya ocupa su casilla junto al Monk, sale desde ahi.
            if (OwnsCell(currentCell))
            {
                currentState =
                    CrossState.Unpossessed;

                transform.SetParent(
                    null,
                    true
                );

                return true;
            }

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
                // Vuela inclinada hacia adelante; MonkCrossVisual la endereza al aterrizar.
                transform.rotation =
                    Quaternion.LookRotation(
                        direction.normalized,
                        Vector3.up
                    ) *
                    Quaternion.Euler(
                        flightTiltAngle,
                        0f,
                        0f
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

                Vector3 currentPos =
                    Vector3.Lerp(
                        startPosition,
                        targetPosition,
                        progress
                    );

                // Parabola para el salto de la cruz
                float hop = Mathf.Sin(progress * Mathf.PI) * attackHopHeight;
                currentPos.y += hop;

                transform.position = currentPos;

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
                    attackDamage,
                    transform.position
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

        // Indica si la celda sigue registrada a nombre de esta cruz.
        public bool OwnsCell(
            Vector2Int cell)
        {
            return cellRegistered &&
                   currentCell == cell &&
                   grid != null &&
                   grid.GetCellOccupant(cell) == gameObject;
        }

        // Registra la cruz poseida en una casilla junto al Monk: asi no se solapa con nadie
        // y el jugador puede golpearla.
        public bool TryClaimPossessedSlot(
            Vector2Int cell)
        {
            if (!IsPossessed ||
                grid == null)
            {
                return false;
            }

            if (OwnsCell(cell))
                return true;

            if (!grid.IsCellInside(cell) ||
                grid.IsCellOccupiedByOther(
                    cell,
                    gameObject))
            {
                return false;
            }

            ReleaseOccupiedCell();

            if (!grid.TryOccupyCell(
                    cell,
                    gameObject))
            {
                return false;
            }

            currentCell = cell;
            cellRegistered = true;
            return true;
        }

        // Si la cruz suelta perdio su casilla (otro se la quito), registra la libre mas cercana
        // para que el jugador siempre la pueda golpear.
        public void EnsureCellRegistered()
        {
            if (!IsUnpossessed ||
                isAttacking ||
                grid == null)
            {
                return;
            }

            if (OwnsCell(currentCell))
                return;

            cellRegistered = false;

            Vector2Int here =
                grid.WorldToCell(
                    transform.position
                );

            Vector2Int[] offsets =
            {
                Vector2Int.zero,
                Vector2Int.up,
                Vector2Int.right,
                Vector2Int.down,
                Vector2Int.left,
                new Vector2Int(1, 1),
                new Vector2Int(1, -1),
                new Vector2Int(-1, -1),
                new Vector2Int(-1, 1)
            };

            for (int i = 0;
                 i < offsets.Length;
                 i++)
            {
                Vector2Int candidate =
                    here + offsets[i];

                if (!grid.IsCellInside(candidate) ||
                    grid.IsCellOccupiedByOther(
                        candidate,
                        gameObject))
                {
                    continue;
                }

                if (!grid.TryOccupyCell(
                        candidate,
                        gameObject))
                {
                    continue;
                }

                currentCell = candidate;
                cellRegistered = true;
                SnapToCurrentCell();
                return;
            }
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

            bool wasPossessed =
                IsPossessed;

            CancelCurrentAttack();

            ReleaseOccupiedCell();

            currentState =
                CrossState.Possessed;

            if (owner != null)
            {
                // De nuevo junto al Monk: el puede pasar por su casilla.
                grid.SetOccupantOwner(
                    gameObject,
                    owner.gameObject
                );

                transform.SetParent(
                    owner.transform,
                    true
                );

                currentCell =
                    grid.WorldToCell(
                        owner.transform.position
                    );
            }

            // Si estaba suelta por el mapa, vuelve volando en vez de aparecer de golpe junto al Monk.
            if (!wasPossessed)
            {
                returning = true;
                returnStartPosition = transform.position;
                returnStartTime = Time.time;

                if (visual != null)
                    visual.AlVolver();
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

            if (visual != null)
                visual.DetenerEstela();

            if (IsUnpossessed)
                SnapToCurrentCell();
        }

        // El Monk empieza a rezar para lanzar esta cruz: la cruz carga su ataque.
        public void PrepareAttack(
            float duration)
        {
            if (visual != null)
                visual.PrepararAtaque(duration);
        }

        // La cruz invoca al Monk para que salga del suelo en su celda.
        public void StartSummon()
        {
            if (visual != null)
                visual.Invocar();
        }

        // Termina la invocacion cuando el Monk ya salio.
        public void EndSummon()
        {
            if (visual != null)
                visual.TerminarInvocacion();
        }

        // Marca la cruz como muerta, libera su celda y ordena recuperar las restantes.
        public void SetDead()
        {
            if (IsDead)
                return;

            CancelCurrentAttack();

            ReleaseOccupiedCell();

            returning = false;

            if (visual != null)
                visual.AlMorir();

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

            if (grid != null)
            {
                grid.SetOccupantOwner(
                    gameObject,
                    null
                );
            }
        }

        // Ejecuta un destello visual cuando el Monje se teletransporta hacia esta cruz.
        public void TriggerTeleportEffect()
        {
            // Puedes añadir instanciacion de particulas aqui despues.
            StartCoroutine(TeleportFlashRoutine());
        }

        private IEnumerator TeleportFlashRoutine()
        {
            // La raiz de la cruz trae un cubo deshabilitado: hay que usar la malla que se ve.
            if (crossRenderer == null || !crossRenderer.enabled)
            {
                Renderer[] visibles = RecursosSacerdote.RenderersVisibles(transform);
                if (visibles.Length > 0)
                    crossRenderer = visibles[0];
            }

            if (crossRenderer != null && crossRenderer.material != null)
            {
                Color originalColor = crossRenderer.material.color;
                
                // Cambiar al color de resalte
                crossRenderer.material.color = teleportHighlightColor;
                
                // Esperar un instante para que sea visible
                yield return new WaitForSeconds(0.5f);
                
                // Volver al color original
                if (crossRenderer != null && crossRenderer.material != null)
                    crossRenderer.material.color = originalColor;
            }
        }
    }
}