using UnityEngine;

public class GridPlayerMovement : MonoBehaviour
{
    [Header("Cuadrícula")]
    [SerializeField] private MeshGrid grid;

    [Header("Jugador")]
    [SerializeField] private float heightOffset = 1f;
    [SerializeField] private bool rotateTowardsMovement = true;

    [Header("Ritmo")]
    [SerializeField] private bool requireRhythm = true;
    [SerializeField] private bool rhythmWindowOpen = true;

    public bool RhythmWindowOpen => rhythmWindowOpen;

    private Vector2Int currentCell;
    public Vector2Int CurrentCell => currentCell;

    private void Start()
    {
        if (grid == null)
            grid = FindFirstObjectByType<MeshGrid>();

        if (grid == null)
        {
            Debug.LogError(
                "No se encontró un objeto con el script MeshGrid.",
                this
            );

            enabled = false;
            return;
        }

        currentCell = grid.WorldToCell(transform.position);
        MoveToCurrentCell();
    }

    private void Update()
    {
        Vector2Int direction = Vector2Int.zero;

        if (Input.GetKeyDown(KeyCode.W))
            direction = Vector2Int.up;
        else if (Input.GetKeyDown(KeyCode.S))
            direction = Vector2Int.down;
        else if (Input.GetKeyDown(KeyCode.A))
            direction = Vector2Int.left;
        else if (Input.GetKeyDown(KeyCode.D))
            direction = Vector2Int.right;

        if (direction != Vector2Int.zero)
            TryMove(direction);
    }

    private void TryMove(Vector2Int direction)
    {
        if (!CanMoveOnRhythm())
        {
            OnMoveMissedRhythm();
            return;
        }

        if (!grid.TryGetNeighbour(
                currentCell,
                direction,
                out Vector2Int nextCell))
        {
            return;
        }

        currentCell = nextCell;

        if (rotateTowardsMovement)
        {
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

        MoveToCurrentCell();
    }

    private bool CanMoveOnRhythm()
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

    private void OnMoveMissedRhythm()
    {
        Debug.Log("Movimiento rechazado: fuera del ritmo.");
    }

    private void MoveToCurrentCell()
    {
        transform.position = grid.CellToWorldCenter(
            currentCell,
            heightOffset
        );
        Debug.Log("Player esta en" + currentCell);
    }
}