using System.Collections;
using UnityEngine;

public class GridPlayerMovement : MonoBehaviour
{
    
    private float hitValue;
    private float hitPlayer;
    private bool isDead = false;

    [Header("Cuadricula")]
    [SerializeField] private MeshGrid grid;

    [Header("Jugador")]
    [SerializeField] private float heightOffset = 1f;
    [SerializeField] private bool rotateTowardsMovement = true;

    [Header("Movimiento suave")]
    [Min(0.01f)]
    [SerializeField] private float moveDuration = 0.15f;

    [SerializeField]
    private AnimationCurve movementCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Ataque")]
    [SerializeField] private KeyCode attackKey = KeyCode.Space;

    [Min(0f)]
    [SerializeField] private float attackCooldown = 0.3f;

    [Header("Ritmo")]
    [SerializeField] private bool requireRhythm = true;
    [SerializeField] private bool rhythmWindowOpen = true;

    private Animator _anim;
    private Vector2Int currentCell;
    private bool isMoving;
    private float nextAttackTime;

    public bool RhythmWindowOpen => rhythmWindowOpen;
    public Vector2Int CurrentCell => currentCell;
    private Vector2Int facingDirection = Vector2Int.up;
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
            Debug.LogError( "No se encontro un objeto con el script MeshGrid.", this );

            enabled = false;
            return;
        }

        currentCell = grid.WorldToCell(transform.position);

        if (!grid.TryOccupyCell(currentCell, gameObject))
        {
            Debug.LogError(
                $"La celda inicial {currentCell} est� ocupada.",
                this
            );

            enabled = false;
            return;
        }

        transform.position = grid.CellToWorldCenter(
            currentCell,
            heightOffset
        );

        BeatManager.Instance.OnBeat.AddListener(ReadMovementInput);
    }

    private void Update() {
        ReadRotationInput();
        ReadAttackInput();

        if (!isMoving)
            //ReadMovementInput()
            ;
    }

    public void SetDead()
    {
        isDead = true;
    }

    private void ReadMovementInput()
    {
        if (isDead)
            return;
        Vector2Int direction = Vector2Int.zero;

        if (Input.GetKey(KeyCode.W))
            direction = Vector2Int.up;
        else if (Input.GetKey(KeyCode.S))
            direction = Vector2Int.down;
        else if (Input.GetKey(KeyCode.A))
            direction = Vector2Int.left;
        else if (Input.GetKey(KeyCode.D))
            direction = Vector2Int.right;

        if (direction != Vector2Int.zero)
            TryMove(direction);
    }

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

    private void ReadAttackInput()
    {
        if (Input.GetKeyDown(attackKey))
            TryAttack();
    }

    private void TryMove(Vector2Int direction)
    {
        if (isDead)
            return;
        if (!CanActOnRhythm())
        {
            OnActionMissedRhythm("movimiento");
            return;
        }

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
                $"No puedes moverte. La celda {nextCell} est� ocupada.",
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

        if (_anim != null)
            // _anim.SetTrigger("OnJump"); ## Desactivado porque se veia feo mientras se ajustaba movimiento por ritmo

        StartCoroutine(
            MoveToCell(previousCell, nextCell)
        );
    }

    private void TryAttack()
    {
        if (isDead)
            return;
        if (!CanActOnRhythm())
        {
            OnActionMissedRhythm("ataque");
            return;
        }

        if (isMoving)
            return;

        if (Time.time < nextAttackTime)
            return;

        nextAttackTime = Time.time + attackCooldown;

        if (_anim != null)
            _anim.SetTrigger("onAttack");

        ExecuteAttack();
    }

    protected virtual void ExecuteAttack()
    {
        Vector2Int attackCell = currentCell + facingDirection;

        if (!grid.IsCellInside(attackCell))
            return;

        GameObject target = grid.GetCellOccupant(attackCell);

        if (target == null)
        {
            Debug.Log(
                $"No hubo impacto. La celda {attackCell} está vacía.",
                this
            );

            return;
        }

        EnemyHealth enemyHealth = target.GetComponent<EnemyHealth>();

        if (enemyHealth == null)
        {
            Debug.Log(
                $"La celda {attackCell} está ocupada, pero {target.name} no tiene EnemyHealth.",
                this
            );

            return;
        }

        hitPlayer = HitFunction();

        enemyHealth.Damage(hitPlayer);
    }

    private float HitFunction()
    {
        hitValue = Random.Range(4, 8);
        return hitValue;
    }

    private IEnumerator MoveToCell(
    Vector2Int previousCell,
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

    private bool CanActOnRhythm()
    {
        if (!requireRhythm)
            return true;

        return rhythmWindowOpen;
    }

    public void SetRhythmWindow(bool isOpen)
    {
        rhythmWindowOpen = isOpen;
    }

    public void SetRequireRhythm(bool value)
    {
        requireRhythm = value;
    }

    private void OnActionMissedRhythm(string actionName)
    {
        Debug.Log(
            $"Acci�n rechazada: {actionName} fuera del ritmo.",
            this
        );
    }
}