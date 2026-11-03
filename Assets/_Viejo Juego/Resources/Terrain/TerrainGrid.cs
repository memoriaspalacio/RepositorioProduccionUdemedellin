using UnityEngine;

[ExecuteAlways]
public class TerrainGrid : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Terrain terrain;

    [Header("Configuración de la cuadrícula")]
    [Min(0.1f)]
    [SerializeField] private float cellSize = 2f;

    [Header("Visualización")]
    [SerializeField] private bool showGrid = true;
    [SerializeField] private Color gridColor = new Color(0f, 1f, 1f, 0.7f);

    public float CellSize => cellSize;
    public Terrain Terrain => terrain;

    public int Columns
    {
        get
        {
            if (terrain == null)
                return 0;

            return Mathf.FloorToInt(terrain.terrainData.size.x / cellSize);
        }
    }

    public int Rows
    {
        get
        {
            if (terrain == null)
                return 0;

            return Mathf.FloorToInt(terrain.terrainData.size.z / cellSize);
        }
    }

    private void Reset()
    {
        terrain = GetComponent<Terrain>();
    }

    private void OnValidate()
    {
        if (terrain == null)
            terrain = GetComponent<Terrain>();

        cellSize = Mathf.Max(0.1f, cellSize);
    }

    /// <summary>
    /// Convierte una posicion del mundo en una coordenada de la cuadricula.
    /// </summary>
    public Vector2Int WorldToCell(Vector3 worldPosition)
    {
        if (terrain == null)
            return Vector2Int.zero;

        Vector3 terrainOrigin = terrain.transform.position;
        Vector3 localPosition = worldPosition - terrainOrigin;

        int x = Mathf.FloorToInt(localPosition.x / cellSize);
        int z = Mathf.FloorToInt(localPosition.z / cellSize);

        x = Mathf.Clamp(x, 0, Mathf.Max(0, Columns - 1));
        z = Mathf.Clamp(z, 0, Mathf.Max(0, Rows - 1));

        return new Vector2Int(x, z);
    }

    /// <summary>
    /// Obtiene el centro de una celda en coordenadas del mundo.
    /// </summary>
    public Vector3 CellToWorldCenter(Vector2Int cell, float heightOffset = 0f)
    {
        if (terrain == null)
            return transform.position;

        Vector3 terrainOrigin = terrain.transform.position;

        float worldX = terrainOrigin.x + (cell.x * cellSize) + (cellSize * 0.5f);
        float worldZ = terrainOrigin.z + (cell.y * cellSize) + (cellSize * 0.5f);

        Vector3 position = new Vector3(worldX, terrainOrigin.y, worldZ);

        // SampleHeight devuelve la altura respecto a la posición del Terrain.
        position.y =
            terrain.SampleHeight(position) +
            terrainOrigin.y +
            heightOffset;

        return position;
    }

    /// <summary>
    /// Comprueba si una coordenada está dentro de la cuadrícula.
    /// </summary>
    public bool IsCellInside(Vector2Int cell)
    {
        return cell.x >= 0 &&
               cell.y >= 0 &&
               cell.x < Columns &&
               cell.y < Rows;
    }

    /// <summary>
    /// Devuelve la celda vecina en una dirección si es válida.
    /// </summary>
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
        if (!showGrid || terrain == null || cellSize <= 0f)
            return;

        Gizmos.color = gridColor;

        Vector3 terrainOrigin = terrain.transform.position;
        Vector3 terrainSize = terrain.terrainData.size;

        int columns = Columns;
        int rows = Rows;

        // Líneas verticales en el eje Z.
        for (int x = 0; x <= columns; x++)
        {
            float worldX = terrainOrigin.x + x * cellSize;

            Vector3 previousPoint = GetTerrainPoint(
                worldX,
                terrainOrigin.z
            );

            for (int z = 1; z <= rows; z++)
            {
                float worldZ = terrainOrigin.z + z * cellSize;

                Vector3 currentPoint = GetTerrainPoint(worldX, worldZ);
                Gizmos.DrawLine(previousPoint, currentPoint);

                previousPoint = currentPoint;
            }
        }

        // Líneas horizontales en el eje X.
        for (int z = 0; z <= rows; z++)
        {
            float worldZ = terrainOrigin.z + z * cellSize;

            Vector3 previousPoint = GetTerrainPoint(
                terrainOrigin.x,
                worldZ
            );

            for (int x = 1; x <= columns; x++)
            {
                float worldX = terrainOrigin.x + x * cellSize;

                Vector3 currentPoint = GetTerrainPoint(worldX, worldZ);
                Gizmos.DrawLine(previousPoint, currentPoint);

                previousPoint = currentPoint;
            }
        }
    }

    private Vector3 GetTerrainPoint(float worldX, float worldZ)
    {
        Vector3 terrainOrigin = terrain.transform.position;

        Vector3 point = new Vector3(
            worldX,
            terrainOrigin.y,
            worldZ
        );

        point.y =
            terrain.SampleHeight(point) +
            terrainOrigin.y +
            0.05f;

        return point;
    }
}
