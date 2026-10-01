using System.Collections;
using UnityEngine;

public class GridPlayerMovement : MonoBehaviour
{
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

// Tiempo minimo entre ataques consecutivos.
[Header("Ataque")]
[Min(0f)]
[SerializeField] private float attackCooldown = 0.3f;

// Referencia al Animator del modelo visual del jugador.
[SerializeField] private Animator _anim;

// Referencia opcional usada para convertir controles locales a direcciones del mundo.
[Header("Referencia a la Camara (Opcional)")]
[SerializeField] private Transform mainCamera;

// Define la velocidad usada para interpolar visualmente la rotacion.
[Header("Rotacion Suave")]
[SerializeField] private float rotationSpeed = 15f;

// Guarda la rotacion visual hacia la que debe orientarse el jugador.
private Quaternion targetRotation;

// Celda actual ocupada por el jugador.
private Vector2Int currentCell;

// Indica si el jugador esta desplazandose entre celdas.
private bool isMoving;

// Guarda el siguiente instante permitido para realizar un ataque.
private float nextAttackTime;

// Direccion logica actual hacia la que mira el jugador.
private Vector2Int facingDirection = Vector2Int.up;

// Expone la celda actual del jugador.
public Vector2Int CurrentCell => currentCell;

// Expone si el jugador esta en movimiento.
public bool IsMoving => isMoving;

// Configura referencias, ocupacion de celda y posicion inicial.
private void Start()
{
    targetRotation = transform.rotation;

    if (mainCamera == null &&
        Camera.main != null)
    {
        mainCamera =
            Camera.main.transform;
    }

    if (grid == null)
        grid = FindFirstObjectByType<MeshGrid>();

    if (grid == null)
    {
        Debug.LogError(
            "No se encontro un objeto con el script MeshGrid.",
            this
        );

        enabled = false;
        return;
    }

    if (beatJudge == null)
        beatJudge = FindFirstObjectByType<BeatActionJudge>();

    if (beatJudge == null)
    {
        Debug.LogError(
            "No se encontro un objeto con el script BeatActionJudge.",
            this
        );

        enabled = false;
        return;
    }

    currentCell =
        grid.WorldToCell(
            transform.position
        );

    if (!grid.TryOccupyCell(
            currentCell,
            gameObject))
    {
        Debug.LogError(
            $"La celda inicial {currentCell} esta ocupada.",
            this
        );

        enabled = false;
        return;
    }

    transform.position =
        grid.CellToWorldCenter(
            currentCell,
            heightOffset
        );
}

// Lee las entradas del jugador y actualiza suavemente su rotacion visual.
private void Update()
{
    ReadRotationInput();

    if (!isMoving)
        ReadMovementInput();

    transform.rotation =
        Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            Time.deltaTime * rotationSpeed
        );
}

// Marca al jugador como muerto para bloquear nuevas acciones.
public void SetDead()
{
    isDead = true;
}

// Lee WASD y decide entre moverse, girar hacia un enemigo o atacarlo.
private void ReadMovementInput()
{
    if (isDead)
        return;

    Vector2Int localDirection =
        Vector2Int.zero;

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

    Vector2Int worldDirection =
        GetCameraRelativeDirection(
            localDirection
        );

    TryMove(
        worldDirection
    );
}

// Lee las flechas y permite cambiar manualmente la direccion del jugador.
private void ReadRotationInput()
{
    Vector2Int localDirection =
        Vector2Int.zero;

    if (Input.GetKeyDown(KeyCode.UpArrow))
        localDirection = Vector2Int.up;
    else if (Input.GetKeyDown(KeyCode.DownArrow))
        localDirection = Vector2Int.down;
    else if (Input.GetKeyDown(KeyCode.LeftArrow))
        localDirection = Vector2Int.left;
    else if (Input.GetKeyDown(KeyCode.RightArrow))
        localDirection = Vector2Int.right;

    if (localDirection == Vector2Int.zero)
        return;

    Vector2Int worldDirection =
        GetCameraRelativeDirection(
            localDirection
        );

    RotateTowards(
        worldDirection
    );
}

// Convierte un input local en una direccion del mundo relativa a la camara.
private Vector2Int GetCameraRelativeDirection(
    Vector2Int localInput)
{
    if (mainCamera == null)
        return localInput;

    Vector3 camForward =
        mainCamera.forward;

    camForward.y = 0f;
    camForward.Normalize();

    Vector3 camRight =
        mainCamera.right;

    camRight.y = 0f;
    camRight.Normalize();

    Vector3 worldDir =
        camForward * localInput.y +
        camRight * localInput.x;

    if (Mathf.Abs(worldDir.x) >
        Mathf.Abs(worldDir.z))
    {
        return worldDir.x > 0f
            ? Vector2Int.right
            : Vector2Int.left;
    }

    return worldDir.z > 0f
        ? Vector2Int.up
        : Vector2Int.down;
}

// Establece la direccion logica y visual hacia la que mira el jugador.
private void RotateTowards(
    Vector2Int direction)
{
    if (direction == Vector2Int.zero)
        return;

    facingDirection =
        direction;

    Vector3 lookDirection =
        new Vector3(
            direction.x,
            0f,
            direction.y
        );

    targetRotation =
        Quaternion.LookRotation(
            lookDirection,
            Vector3.up
        );
}

// Decide si la direccion solicitada permite moverse o interactuar con un enemigo.
private void TryMove(
    Vector2Int direction)
{
    if (isDead)
        return;

    if (isMoving)
        return;

    if (!grid.TryGetNeighbour(
            currentCell,
            direction,
            out Vector2Int nextCell))
    {
        return;
    }

    if (grid.IsCellOccupiedByOther(
            nextCell,
            gameObject))
    {
        HandleOccupiedCell(
            direction,
            nextCell
        );

        return;
    }

    MoveToAvailableCell(
        direction,
        nextCell
    );
}

// Decide si una celda ocupada contiene un enemigo que puede ser orientado o atacado.
private void HandleOccupiedCell(
    Vector2Int direction,
    Vector2Int occupiedCell)
{
    GameObject target =
        grid.GetCellOccupant(
            occupiedCell
        );

    if (target == null)
    {
        Debug.Log(
            $"La celda {occupiedCell} esta registrada como ocupada pero no tiene ocupante.",
            this
        );

        return;
    }

    EnemyHealth enemyHealth =
        target.GetComponentInParent<EnemyHealth>();

    if (enemyHealth == null)
    {
        Debug.Log(
            $"No puedes moverte. La celda {occupiedCell} esta ocupada por {target.name}.",
            this
        );

        return;
    }

    if (facingDirection != direction)
    {
        RotateTowards(
            direction
        );

        Debug.Log(
            $"Jugador orientado hacia el enemigo en {occupiedCell}.",
            this
        );

        return;
    }

    TryAttack();
}

// Traslada al jugador hacia una celda disponible manteniendo el sistema original de movimiento.
private void MoveToAvailableCell(
    Vector2Int direction,
    Vector2Int nextCell)
{
    Vector2Int previousCell =
        currentCell;

    if (!grid.TryMoveOccupant(
            previousCell,
            nextCell,
            gameObject))
    {
        return;
    }

    string animationTrigger =
        GetRelativeAnimationTrigger(
            direction
        );

    if (rotateTowardsMovement)
        RotateTowards(direction);

    if (_anim != null)
    {
        Debug.Log(
            $"[AnimDebug] Moviendo a: {direction}. Disparando: {animationTrigger}.",
            this
        );

        _anim.SetTrigger(
            animationTrigger
        );
    }
    else
    {
        Debug.LogError(
            "[AnimDebug] El Animator es NULL.",
            this
        );
    }

    StartCoroutine(
        MoveToCell(
            nextCell
        )
    );
}

// Calcula la animacion de movimiento relativa a la direccion hacia la que mira el jugador.
private string GetRelativeAnimationTrigger(
    Vector2Int moveDirection)
{
    Vector2 move =
        new Vector2(
            moveDirection.x,
            moveDirection.y
        );

    Vector2 face =
        new Vector2(
            facingDirection.x,
            facingDirection.y
        );

    float dot =
        Vector2.Dot(
            face,
            move
        );

    float cross =
        face.x * move.y -
        face.y * move.x;

    if (dot > 0.5f)
        return "onForward";

    if (dot < -0.5f)
        return "onBackward";

    if (cross > 0.5f)
        return "onLeft";

    if (cross < -0.5f)
        return "onRight";

    return "onForward";
}

// Comprueba el cooldown y ejecuta el ataque hacia la direccion actual.
private void TryAttack()
{
    if (isDead)
        return;

    if (isMoving)
        return;

    if (Time.time < nextAttackTime)
        return;

    nextAttackTime =
        Time.time + attackCooldown;

    if (_anim != null)
        _anim.SetTrigger("onAttack");

    ExecuteAttack();
}

// Ataca exclusivamente la celda situada frente al jugador.
protected virtual void ExecuteAttack()
{
    Vector2Int attackCell =
        currentCell +
        facingDirection;

    if (!grid.IsCellInside(
            attackCell))
    {
        return;
    }

    GameObject target =
        grid.GetCellOccupant(
            attackCell
        );

    if (target == null)
    {
        Debug.Log(
            $"No hubo impacto. La celda {attackCell} esta vacia.",
            this
        );

        return;
    }

    EnemyHealth enemyHealth =
        target.GetComponentInParent<EnemyHealth>();

    if (enemyHealth == null)
    {
        Debug.Log(
            $"La celda {attackCell} esta ocupada, pero {target.name} no tiene EnemyHealth.",
            this
        );

        return;
    }

    enemyHealth.Damage(
        HitFunction()
    );
}

// Genera el valor aleatorio de dano realizado por el jugador.
private float HitFunction()
{
    return Random.Range(
        4,
        8
    );
}

// Interpola la posicion del jugador hasta el centro de una celda destino.
private IEnumerator MoveToCell(
    Vector2Int nextCell)
{
    isMoving = true;

    Vector3 startPosition =
        transform.position;

    Vector3 targetPosition =
        grid.CellToWorldCenter(
            nextCell,
            heightOffset
        );

    float elapsedTime = 0f;

    while (elapsedTime < moveDuration)
    {
        elapsedTime +=
            Time.deltaTime;

        float normalizedTime =
            Mathf.Clamp01(
                elapsedTime /
                moveDuration
            );

        float curvedTime =
            movementCurve.Evaluate(
                normalizedTime
            );

        transform.position =
            Vector3.Lerp(
                startPosition,
                targetPosition,
                curvedTime
            );

        yield return null;
    }

    transform.position =
        targetPosition;

    currentCell =
        nextCell;

    isMoving = false;
}

// Detecta una colision y comprueba la etiqueta usada para completar el nivel.
private void OnCollisionEnter(
    Collision collision)
{
    if (CompareTag("exit"))
        ScreensInGame.singleton.ScreenWinActive();
}

// Detecta un trigger y comprueba la etiqueta usada para completar el nivel.
private void OnTriggerEnter(
    Collider other)
{
    if (CompareTag("exit"))
        ScreensInGame.singleton.ScreenWinActive();
}

}