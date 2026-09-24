using System.Collections;
using UnityEngine;

public class GridPlayerMovement : MonoBehaviour {
    // Indica si el jugador esta muerto y no puede realizar acciones.
    private bool isDead = false;

    // Referencia a la cuadricula usada para posicion y ocupacion de celdas.
    [Header("Cuadricula")]
    [SerializeField] private MeshGrid grid;

    // Referencia al sistema que valida las acciones segun el ritmo.
    [Header("Ritmo")]
    [SerializeField] private BeatActionJudge beatJudge;

    // Altura adicional aplicada al centro de cada celda.
    [Header("Jugador")]
    [SerializeField] private float heightOffset = 1f;

    // Indica si el jugador debe mirar hacia la direccion de movimiento.
    [SerializeField] private bool rotateTowardsMovement = true;

    // Tiempo usado para interpolar el movimiento entre celdas.
    [Header("Movimiento suave")]
    [Min(0.01f)]
    [SerializeField] private float moveDuration = 0.15f;

    // Curva usada para suavizar la interpolacion del movimiento.
    [SerializeField]
    private AnimationCurve movementCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    // Tecla usada para iniciar un ataque.
    [Header("Ataque")]
    [SerializeField] private KeyCode attackKey = KeyCode.Space;

    // Tiempo minimo entre ataques consecutivos.
    [Min(0f)]
    [SerializeField] private float attackCooldown = 0.3f;

    // Referencia al Animator del modelo visual del jugador.
    [SerializeField] private Animator _anim;

    [Header("Referencia a la Camara (Opcional)")]
    [SerializeField] private Transform mainCamera;

    [Header("Rotacion Suave")]
    [SerializeField] private float rotationSpeed = 15f;
    private Quaternion targetRotation;

    // Celda actual ocupada por el jugador.
    private Vector2Int currentCell;

    // Indica si el jugador esta desplazandose entre celdas.
    private bool isMoving;

    // Tiempo minimo en el que puede comenzar el siguiente ataque.
    private float nextAttackTime;

    // Direccion actual hacia la que mira el jugador.
    private Vector2Int facingDirection = Vector2Int.up;

    // Expone la celda actual del jugador.
    public Vector2Int CurrentCell => currentCell;

    // Expone si el jugador esta en movimiento.
    public bool IsMoving => isMoving;

    
    private void Start()
    {
        targetRotation = transform.rotation;

        if (mainCamera == null && Camera.main != null)
        {
            mainCamera = Camera.main.transform;
        }

        if (grid == null)
            grid = FindFirstObjectByType<MeshGrid>();

        if (grid == null) {
            Debug.LogError(
                "No se encontro un objeto con el script MeshGrid.",
                this
            );

            enabled = false;
            return;
        }

        if (beatJudge == null)
            beatJudge = FindFirstObjectByType<BeatActionJudge>();

        if (beatJudge == null) {
            Debug.LogError(
                "No se encontro un objeto con el script BeatActionJudge.",
                this
            );

            enabled = false;
            return;
        }

        currentCell = grid.WorldToCell(transform.position);

        if (!grid.TryOccupyCell(currentCell, gameObject)) {
            Debug.LogError(
                $"La celda inicial {currentCell} esta ocupada.",
                this
            );

            enabled = false;
            return;
        }

        transform.position = grid.CellToWorldCenter(
            currentCell,
            heightOffset
        );
    }

    // Lee las entradas de rotacion, ataque y movimiento.
    private void Update() {
        ReadRotationInput();

        if (!isMoving) {
            ReadMovementInput();
            ReadAttackInput();
        }

        // Rotacion suave hacia el objetivo
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            Time.deltaTime * rotationSpeed
        );
    }

    // Marca al jugador como muerto para bloquear nuevas acciones.
    public void SetDead() {
        isDead = true;
    }

    // Lee WASD y solicita un movimiento valido segun el ritmo.
    private void ReadMovementInput() {
        if (isDead)
            return;

        Vector2Int localDirection = Vector2Int.zero;

        if (Input.GetKeyDown(KeyCode.W))
            localDirection = Vector2Int.up;
        else if (Input.GetKeyDown(KeyCode.S))
            localDirection = Vector2Int.down;
        else if (Input.GetKeyDown(KeyCode.A))
            localDirection = Vector2Int.left;
        else if (Input.GetKeyDown(KeyCode.D))
            localDirection = Vector2Int.right;

        if (localDirection == Vector2Int.zero)
            return;


        if (!beatJudge.TryConsumeBeat())
            return;

        Vector2Int worldDirection = GetCameraRelativeDirection(localDirection);
        TryMove(worldDirection);
    }

    // Lee las flechas y cambia la direccion del jugador.
    private void ReadRotationInput()
    {
        Vector2Int localDirection = Vector2Int.zero;

        if (Input.GetKeyDown(KeyCode.UpArrow))
            localDirection = Vector2Int.up;
        else if (Input.GetKeyDown(KeyCode.DownArrow))
            localDirection = Vector2Int.down;
        else if (Input.GetKeyDown(KeyCode.LeftArrow))
            localDirection = Vector2Int.left;
        else if (Input.GetKeyDown(KeyCode.RightArrow))
            localDirection = Vector2Int.right;

        if (localDirection != Vector2Int.zero)
        {
            Vector2Int worldDirection = GetCameraRelativeDirection(localDirection);
            RotateTowards(worldDirection);
        }
    }

    // Convierte un input 2D en una direccion del mundo relativa a la camara.
    private Vector2Int GetCameraRelativeDirection(Vector2Int localInput)
    {
        if (mainCamera == null)
            return localInput; // Fallback absoluto si no hay camara

        Vector3 camForward = mainCamera.forward;
        camForward.y = 0;
        camForward.Normalize();

        Vector3 camRight = mainCamera.right;
        camRight.y = 0;
        camRight.Normalize();

        Vector3 worldDir = (camForward * localInput.y) + (camRight * localInput.x);

        if (Mathf.Abs(worldDir.x) > Mathf.Abs(worldDir.z))
        {
            return worldDir.x > 0 ? Vector2Int.right : Vector2Int.left;
        }
        else
        {
            return worldDir.z > 0 ? Vector2Int.up : Vector2Int.down;
        }
    }

    // Establece la rotacion objetivo hacia una direccion de la cuadricula.
    private void RotateTowards(Vector2Int direction)
    {
        facingDirection = direction;

        Vector3 lookDirection = new Vector3(
            direction.x,
            0f,
            direction.y
        );

        targetRotation = Quaternion.LookRotation(
            lookDirection,
            Vector3.up
        );
    }

    // Lee la tecla configurada e intenta iniciar un ataque.
    private void ReadAttackInput() {
        if (Input.GetKeyDown(attackKey))
            TryAttack();
    }

    // Valida la celda destino y comienza el desplazamiento del jugador.
    private void TryMove(Vector2Int direction) {
        if (isDead)
            return;

        if (isMoving)
            return;

        if (!grid.TryGetNeighbour(
                currentCell,
                direction,
                out Vector2Int nextCell)) {
            return;
        }

        if (grid.IsCellOccupiedByOther(nextCell, gameObject)) {
            Debug.Log(
                $"No puedes moverte. La celda {nextCell} esta ocupada.",
                this
            );

            return;
        }

        Vector2Int previousCell = currentCell;

        if (!grid.TryMoveOccupant(
                previousCell,
                nextCell,
                gameObject)) {
            return;
        }

        if (rotateTowardsMovement)
            RotateTowards(direction);

        if (_anim != null)
        {
            string animTrigger = GetRelativeAnimationTrigger(direction);
            Debug.Log($"[AnimDebug] Moviendo a: {direction}. Mirando a: {facingDirection}. Disparando: {animTrigger}");
            _anim.SetTrigger(animTrigger);
        }
        else
        {
            Debug.LogError("[AnimDebug] El Animator es NULL. No has arrastrado el componente al Inspector o se perdio la referencia.");
        }

        StartCoroutine(
            MoveToCell(nextCell)
        );
    }

    // Calcula que animacion disparar basandose en la diferencia entre la rotacion y la direccion de movimiento.
    private string GetRelativeAnimationTrigger(Vector2Int moveDirection)
    {
        Vector2 move = new Vector2(moveDirection.x, moveDirection.y);
        Vector2 face = new Vector2(facingDirection.x, facingDirection.y);

        float dot = Vector2.Dot(face, move);
        float cross = (face.x * move.y) - (face.y * move.x);

        if (dot > 0.5f) return "onForward";
        if (dot < -0.5f) return "onBackward";
        if (cross > 0.5f) return "onLeft";
        if (cross < -0.5f) return "onRight";

        return "onForward"; 
    }

    // Comprueba restricciones de ataque y ejecuta la accion.
    private void TryAttack() {
        if (isDead)
            return;

        if (isMoving)
            return;

        if (!beatJudge.TryConsumeBeat())
            return;

        if (_anim != null)
            _anim.SetTrigger("onAttack");

        ExecuteAttack();
    }

    // Ataca la celda frontal y aplica dano al enemigo encontrado.
    protected virtual void ExecuteAttack() {
        Vector2Int attackCell = currentCell + facingDirection;

        if (!grid.IsCellInside(attackCell))
            return;

        GameObject target = grid.GetCellOccupant(attackCell);

        if (target == null) {
            Debug.Log(
                $"No hubo impacto. La celda {attackCell} esta vacia.",
                this
            );

            return;
        }

        EnemyHealth enemyHealth = target.GetComponent<EnemyHealth>();

        if (enemyHealth == null) {
            Debug.Log(
                $"La celda {attackCell} esta ocupada, pero {target.name} no tiene EnemyHealth.",
                this
            );

            return;
        }

        enemyHealth.Damage(HitFunction());
    }

    // Genera el valor aleatorio de dano del jugador.
    private float HitFunction() {
        return Random.Range(4, 8);
    }

    // Interpola la posicion del jugador hasta la celda destino.
    private IEnumerator MoveToCell(
        Vector2Int nextCell) {
        isMoving = true;

        Vector3 startPosition = transform.position;

        Vector3 targetPosition = grid.CellToWorldCenter(
            nextCell,
            heightOffset
        );

        float elapsedTime = 0f;

        while (elapsedTime < moveDuration) {
            elapsedTime += Time.deltaTime;

            float normalizedTime =
                Mathf.Clamp01(elapsedTime / moveDuration);

            float curvedTime =
                movementCurve.Evaluate(normalizedTime);

            transform.position = Vector3.Lerp(
                startPosition,
                targetPosition,
                curvedTime
            );

            yield return null;
        }

        transform.position = targetPosition;
        currentCell = nextCell;
        isMoving = false;
    }

    // Detecta una colision y comprueba la etiqueta de salida del jugador.
    private void OnCollisionEnter(Collision collision) {
        if (CompareTag("exit")) {
            ScreensInGame.singleton.ScreenWinActive();
        }
    }

    // Detecta un trigger y comprueba la etiqueta de salida del jugador.
    private void OnTriggerEnter(Collider other) {
        if (CompareTag("exit")) {
            ScreensInGame.singleton.ScreenWinActive();
        }
    }
}