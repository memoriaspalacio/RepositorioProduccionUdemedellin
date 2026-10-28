using UnityEngine;
using System.Collections.Generic;

//ExecuteAlways permite que el script funcione tanto en Play Mode como en el editor de Unity
[ExecuteAlways]
public class MeshGrid : MonoBehaviour
{
    //Tamaño de cada casilla de la cuadricula en unidades de Unity
    [Header("Tamaño de las casillas")]
    [Min(0.1f)] //El inspector no permite un valor mas bajo que 0.1f
    [SerializeField] private float cellSize = 2f;

    [Header("Visualizacion")]
    [SerializeField] private bool showGrid = true;
    [SerializeField] private Color gridColor = Color.cyan; //Color cuadricula

    [Header("Deteccion del suelo")]
    [SerializeField] private LayerMask groundLayer = ~0; // El valor ~0 incluye todas las capas.
                                                         // ~ normalmente es para indicar excepcion pero con 0 se invierte
    [SerializeField] private float raycastHeight = 100f; //Altura adicional desde la que comienza cada raycast
                                                         //Un valor alto permite detectar terrenos con grandes diferencias

    private Renderer groundRenderer; //Renderer usado para calcular los límites de la cuadrícula del modelo del terreno
    private readonly Dictionary<Vector2Int, GameObject> occupiedCells = new Dictionary<Vector2Int, GameObject>();

    public float CellSize => cellSize; // propiedad de solo lectura,
                                       // => es el equivalente de usar get


    //Devuelve la cantidad de columnas de la cuadricula.
    //Las columnas se calculan dividiendo el tamaño global del Renderer
    //en el eje X entre el tamaño de las casillas
    // FloorToInt descarta cualquier espacio sobrante que no alcance
    public int Columns
    {
        get
        {
            Bounds bounds = GetBounds();
            return Mathf.Max(1, Mathf.FloorToInt(bounds.size.x / cellSize));
        }
    }

    //Lo mismo que Columns pero en el eje y

    public int Rows
    {
        get
        {
            Bounds bounds = GetBounds();
            return Mathf.Max(1, Mathf.FloorToInt(bounds.size.z / cellSize));
        }
    }
    // Busca el Renderer necesario al inicializar el componente.
    private void Awake()
    {
        FindRenderer();
    }

    // Actualiza la referencia al Renderer cuando el componente se habilita.
    private void OnEnable()
    {
        FindRenderer();
    }

    // Valida valores editados desde el Inspector y actualiza referencias.
    private void OnValidate()
    {
        cellSize = Mathf.Max(0.1f, cellSize);
        FindRenderer();
    }

    // Busca el Renderer en este objeto o en alguno de sus hijos.
    private void FindRenderer()
    {
        groundRenderer = GetComponent<Renderer>();

        if (groundRenderer == null)
            groundRenderer = GetComponentInChildren<Renderer>();
    }

    // Devuelve los limites usados como referencia para construir la cuadricula.
    private Bounds GetBounds()
    {
        if (groundRenderer == null)
            FindRenderer();

        if (groundRenderer != null)
            return groundRenderer.bounds;

        return new Bounds(transform.position, Vector3.one);
    }

    // Convierte una posicion del mundo a una celda valida de la cuadricula.
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

    // Convierte una celda a la posicion central correspondiente en el mundo.
    public Vector3 CellToWorldCenter(
        Vector2Int cell,
        float playerHeightOffset = 0f)
    {
        Bounds bounds = GetBounds();

        float x =
            bounds.min.x +
            cell.x * cellSize +
            cellSize * 0.5f;

        float z =
            bounds.min.z +
            cell.y * cellSize +
            cellSize * 0.5f;

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
            return hit.point +
                   Vector3.up * playerHeightOffset;
        }

        return new Vector3(
            x,
            bounds.max.y + playerHeightOffset,
            z
        );
    }

    // Comprueba si una celda se encuentra dentro de los limites de la cuadricula.
    public bool IsCellInside(Vector2Int cell)
    {
        return cell.x >= 0 &&
               cell.y >= 0 &&
               cell.x < Columns &&
               cell.y < Rows;
    }

    // Calcula una celda vecina y comprueba que siga dentro de los limites.
    public bool TryGetNeighbour(
        Vector2Int currentCell,
        Vector2Int direction,
        out Vector2Int neighbour)
    {
        neighbour = currentCell + direction;
        return IsCellInside(neighbour);
    }

    // Dibuja la cuadricula sobre la superficie detectada dentro del editor.
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
            float worldX =
                bounds.min.x + x * cellSize;

            Vector3 previous =
                GetGroundPoint(
                    worldX,
                    bounds.min.z
                );

            for (int z = 1; z <= Rows; z++)
            {
                float worldZ =
                    bounds.min.z + z * cellSize;

                Vector3 current =
                    GetGroundPoint(
                        worldX,
                        worldZ
                    );

                Gizmos.DrawLine(previous, current);
                previous = current;
            }
        }

        for (int z = 0; z <= Rows; z++)
        {
            float worldZ =
                bounds.min.z + z * cellSize;

            Vector3 previous =
                GetGroundPoint(
                    bounds.min.x,
                    worldZ
                );

            for (int x = 1; x <= Columns; x++)
            {
                float worldX =
                    bounds.min.x + x * cellSize;

                Vector3 current =
                    GetGroundPoint(
                        worldX,
                        worldZ
                    );

                Gizmos.DrawLine(previous, current);
                previous = current;
            }
        }
    }

    // Obtiene la altura del suelo para un punto usado al dibujar la cuadricula.
    private Vector3 GetGroundPoint(
        float x,
        float z)
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

        return new Vector3(
            x,
            bounds.max.y,
            z
        );
    }

    // Comprueba si una celda tiene algun ocupante registrado.
    public bool IsCellOccupied(Vector2Int cell)
    {
        CleanDestroyedOccupant(cell);
        return occupiedCells.ContainsKey(cell);
    }

    // Comprueba si una celda esta ocupada por un objeto diferente al solicitante.
    public bool IsCellOccupiedByOther(
        Vector2Int cell,
        GameObject requester)
    {
        CleanDestroyedOccupant(cell);

        if (!occupiedCells.TryGetValue(
                cell,
                out GameObject occupant))
        {
            return false;
        }

        return occupant != requester;
    }

    // Registra un GameObject como ocupante de una celda disponible.
    public bool TryOccupyCell(
        Vector2Int cell,
        GameObject occupant)
    {
        if (occupant == null)
            return false;

        if (!IsCellInside(cell))
            return false;

        CleanDestroyedOccupant(cell);

        if (occupiedCells.TryGetValue(
                cell,
                out GameObject currentOccupant))
        {
            return currentOccupant == occupant;
        }

        occupiedCells.Add(cell, occupant);
        return true;
    }

    // Traslada el registro de ocupacion de una celda hacia otra.
    public bool TryMoveOccupant(
        Vector2Int previousCell,
        Vector2Int nextCell,
        GameObject occupant)
    {
        if (occupant == null)
            return false;

        if (!IsCellInside(nextCell))
            return false;

        if (IsCellOccupiedByOther(
                nextCell,
                occupant))
        {
            return false;
        }

        ReleaseCell(
            previousCell,
            occupant
        );

        occupiedCells[nextCell] = occupant;

        return true;
    }

    // Libera una celda cuando pertenece al ocupante indicado.
    public void ReleaseCell(
        Vector2Int cell,
        GameObject occupant)
    {
        CleanDestroyedOccupant(cell);

        if (!occupiedCells.TryGetValue(
                cell,
                out GameObject currentOccupant))
        {
            return;
        }

        if (currentOccupant == occupant)
            occupiedCells.Remove(cell);
    }

    // Devuelve el GameObject registrado actualmente en una celda.
    public GameObject GetCellOccupant(
        Vector2Int cell)
    {
        CleanDestroyedOccupant(cell);

        occupiedCells.TryGetValue(
            cell,
            out GameObject occupant
        );

        return occupant;
    }

    // Elimina referencias destruidas que hayan quedado registradas en una celda.
    private void CleanDestroyedOccupant(
        Vector2Int cell)
    {
        if (!occupiedCells.TryGetValue(
                cell,
                out GameObject occupant))
        {
            return;
        }

        if (occupant == null)
            occupiedCells.Remove(cell);
    }
}