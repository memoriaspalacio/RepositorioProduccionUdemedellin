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

    // Tecla usada para iniciar un ataque.
    [Header("Ataque")]
    [SerializeField] private KeyCode attackKey = KeyCode.Space;

    // Tiempo minimo entre ataques consecutivos.
    [Min(0f)]
    [SerializeField] private float attackCooldown = 0.3f;

    // Referencia al Animator del modelo visual del jugador.
    private Animator _anim;

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
        Transform model = transform.Find("ModelChica");

        if (model != null)
            _anim = model.GetComponent<Animator>();

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

        currentCell = grid.WorldToCell(transform.position);

        if (!grid.TryOccupyCell(currentCell, gameObject))
        {
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
    private void Update()
    {
        ReadRotationInput();
        

        if (!isMoving)
            ReadMovementInput();
            ReadAttackInput();
    }

    // Marca al jugador como muerto para bloquear nuevas acciones.
    public void SetDead()
    {
        isDead = true;
    }

    // Lee WASD y solicita un movimiento valido segun el ritmo.
    private void ReadMovementInput()
    {
        if (isDead)
            return;

        Vector2Int direction = Vector2Int.zero;

        if (Input.GetKeyDown(KeyCode.W))
            direction = Vector2Int.up;
        else if (Input.GetKeyDown(KeyCode.S))
            direction = Vector2Int.down;
        else if (Input.GetKeyDown(KeyCode.A))
            direction = Vector2Int.left;
        else if (Input.GetKeyDown(KeyCode.D))
            direction = Vector2Int.right;

        if (direction == Vector2Int.zero)
            return;
       

        if (!beatJudge.TryConsumeBeat())
            return;

        TryMove(direction);
    }

    // Lee las flechas y cambia la direccion del jugador.
    private void ReadRotationInput()
    {
        Vector2Int direction = Vector2Int.zero;

        if (Input.GetKeyDown(KeyCode.UpArrow))
            direction = Vector2Int.up;
        else if (Input.GetKeyDown(KeyCode.DownArrow))
            direction = Vector2Int.down;
        else if (Input.GetKeyDown(KeyCode.LeftArrow))
            direction = Vector2Int.left;
        else if (Input.GetKeyDown(KeyCode.RightArrow))
            direction = Vector2Int.right;

        if (direction != Vector2Int.zero)
            RotateTowards(direction);
    }

    // Rota al jugador hacia una direccion de la cuadricula.
    private void RotateTowards(Vector2Int direction)
    {
        facingDirection = direction;

        Vector3 lookDirection = new Vector3(
            direction.x,
            0f,
            direction.y
        );

        transform.rotation = Quaternion.LookRotation(
            lookDirection,
            Vector3.up
        );
    }

    // Lee la tecla configurada e intenta iniciar un ataque.
    private void ReadAttackInput()
    {
        if (Input.GetKeyDown(attackKey))
            TryAttack();
    }

    // Valida la celda destino y comienza el desplazamiento del jugador.
    private void TryMove(Vector2Int direction)
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

        if (grid.IsCellOccupiedByOther(nextCell, gameObject))
        {
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
                gameObject))
        {
            return;
        }

        if (rotateTowardsMovement)
            RotateTowards(direction);

        StartCoroutine(
            MoveToCell(nextCell)
        );
    }

    // Comprueba restricciones de ataque y ejecuta la accion.
    private void TryAttack()
    {
        if (isDead)
            return;

        if (isMoving)
            return;

        if (Time.time < nextAttackTime)
            return;

        nextAttackTime = Time.time + attackCooldown;

        if (_anim != null)
            _anim.SetTrigger("onAttack");

        ExecuteAttack();
    }

    // Ataca la celda frontal y aplica dano al enemigo encontrado.
    protected virtual void ExecuteAttack()
    {
        Vector2Int attackCell = currentCell + facingDirection;

        if (!grid.IsCellInside(attackCell))
            return;

        GameObject target = grid.GetCellOccupant(attackCell);

        if (target == null)
        {
            Debug.Log(
                $"No hubo impacto. La celda {attackCell} esta vacia.",
                this
            );

            return;
        }

        EnemyHealth enemyHealth = target.GetComponent<EnemyHealth>();

        if (enemyHealth == null)
        {
            Debug.Log(
                $"La celda {attackCell} esta ocupada, pero {target.name} no tiene EnemyHealth.",
                this
            );

            return;
        }

        enemyHealth.Damage(HitFunction());
    }

    // Genera el valor aleatorio de dano del jugador.
    private float HitFunction()
    {
        return Random.Range(4, 8);
    }

    // Interpola la posicion del jugador hasta la celda destino.
    private IEnumerator MoveToCell(
        Vector2Int nextCell)
    {
        isMoving = true;

        Vector3 startPosition = transform.position;

        Vector3 targetPosition = grid.CellToWorldCenter(
            nextCell,
            heightOffset
        );

        float elapsedTime = 0f;

        while (elapsedTime < moveDuration)
        {
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
    private void OnCollisionEnter(Collision collision)
    {
        if (CompareTag("exit"))
        {
            ScreensInGame.singleton.ScreenWinActive();
        }
    }

    // Detecta un trigger y comprueba la etiqueta de salida del jugador.
    private void OnTriggerEnter(Collider other)
    {
        if (CompareTag("exit"))
        {
            ScreensInGame.singleton.ScreenWinActive();
        }
    }
}