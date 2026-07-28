using UnityEngine;

[ExecuteAlways]
public class MeshGrid : MonoBehaviour
{
    [Header("Tamaño de las casillas")]
    [Min(0.1f)]
    [SerializeField] private float cellSize = 2f;

    [Header("Visualización")]
    [SerializeField] private bool showGrid = true;
    [SerializeField] private Color gridColor = Color.cyan;

    [Header("Detección del suelo")]
    [SerializeField] private LayerMask groundLayer = ~0;
    [SerializeField] private float raycastHeight = 100f;

    private Renderer groundRenderer;

    public float CellSize => cellSize;

    public int Columns
    {
        get
        {
            Bounds bounds = GetBounds();
            return Mathf.Max(1, Mathf.FloorToInt(bounds.size.x / cellSize));
        }
    }

    public int Rows
    {
        get
        {
            Bounds bounds = GetBounds();
            return Mathf.Max(1, Mathf.FloorToInt(bounds.size.z / cellSize));
        }
    }

    private void Awake()
    {
        FindRenderer();
    }

    private void OnEnable()
    {
        FindRenderer();
    }

    private void OnValidate()
    {
        cellSize = Mathf.Max(0.1f, cellSize);
        FindRenderer();
    }

    private void FindRenderer()
    {
        groundRenderer = GetComponent<Renderer>();

        if (groundRenderer == null)
            groundRenderer = GetComponentInChildren<Renderer>();
    }

    private Bounds GetBounds()
    {
        if (groundRenderer == null)
            FindRenderer();

        if (groundRenderer != null)
            return groundRenderer.bounds;

        return new Bounds(transform.position, Vector3.one);
    }

    public Vector2Int WorldToCell(Vector3 worldPosition)
    {
        Bounds bounds = GetBounds();

        int x = Mathf.FloorToInt(
            (worldPosition.x - bounds.min.x) / cellSize
        );

        int z = Mathf.FloorToInt(
            (worldPosition.z - bounds.min.z) / cellSize
        );

        x = Mathf.Clamp(x, 0, Columns - 1);
        z = Mathf.Clamp(z, 0, Rows - 1);

        return new Vector2Int(x, z);
    }

    public Vector3 CellToWorldCenter(
        Vector2Int cell,
        float playerHeightOffset = 0f)
    {
        Bounds bounds = GetBounds();

        float x = bounds.min.x + cell.x * cellSize + cellSize * 0.5f;
        float z = bounds.min.z + cell.y * cellSize + cellSize * 0.5f;

        Vector3 rayOrigin = new Vector3(
            x,
            bounds.max.y + raycastHeight,
            z
        );

        if (Physics.Raycast(
                rayOrigin,
                Vector3.down,
                out RaycastHit hit,
                raycastHeight * 2f + bounds.size.y,
                groundLayer,
                QueryTriggerInteraction.Ignore))
        {
            return hit.point + Vector3.up * playerHeightOffset;
        }

        return new Vector3(
            x,
            bounds.max.y + playerHeightOffset,
            z
        );
    }

    public bool IsCellInside(Vector2Int cell)
    {
        return cell.x >= 0 &&
               cell.y >= 0 &&
               cell.x < Columns &&
               cell.y < Rows;
    }

    public bool TryGetNeighbour(
        Vector2Int currentCell,
        Vector2Int direction,
        out Vector2Int neighbour)
    {
        neighbour = currentCell + direction;
        return IsCellInside(neighbour);
    }

    private void OnDrawGizmos()
    {
        if (!showGrid || cellSize <= 0f)
            return;

        FindRenderer();

        if (groundRenderer == null)
            return;

        Gizmos.color = gridColor;

        Bounds bounds = GetBounds();

        for (int x = 0; x <= Columns; x++)
        {
            float worldX = bounds.min.x + x * cellSize;

            Vector3 previous = GetGroundPoint(
                worldX,
                bounds.min.z
            );

            for (int z = 1; z <= Rows; z++)
            {
                float worldZ = bounds.min.z + z * cellSize;
                Vector3 current = GetGroundPoint(worldX, worldZ);

                Gizmos.DrawLine(previous, current);
                previous = current;
            }
        }

        for (int z = 0; z <= Rows; z++)
        {
            float worldZ = bounds.min.z + z * cellSize;

            Vector3 previous = GetGroundPoint(
                bounds.min.x,
                worldZ
            );

            for (int x = 1; x <= Columns; x++)
            {
                float worldX = bounds.min.x + x * cellSize;
                Vector3 current = GetGroundPoint(worldX, worldZ);

                Gizmos.DrawLine(previous, current);
                previous = current;
            }
        }
    }

    private Vector3 GetGroundPoint(float x, float z)
    {
        Bounds bounds = GetBounds();

        Vector3 rayOrigin = new Vector3(
            x,
            bounds.max.y + raycastHeight,
            z
        );

        if (Physics.Raycast(
                rayOrigin,
                Vector3.down,
                out RaycastHit hit,
                raycastHeight * 2f + bounds.size.y,
                groundLayer,
                QueryTriggerInteraction.Ignore))
        {
            return hit.point + Vector3.up * 0.05f;
        }

        return new Vector3(x, bounds.max.y, z);
    }
}