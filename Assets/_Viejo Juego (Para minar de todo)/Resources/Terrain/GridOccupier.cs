using UnityEngine;
using System.Collections.Generic;

// Detecta y registra las celdas de la cuadricula ocupadas por este objeto
[ExecuteAlways]
public class GridOccupier : MonoBehaviour
{
    [Header("Cuadricula")]

    // Referencia al MeshGrid que contiene la cuadricula
    [SerializeField] private MeshGrid grid;

    [Header("Deteccion")]

    // Indica si la ocupacion se actualiza cuando cambia el Transform
    [SerializeField] private bool updateWhenTransformChanges = true;

    // Reduce ligeramente el area comprobada de cada celda
    [Range(0.8f, 1f)]
    [SerializeField] private float cellCheckScale = 0.98f;

    [Header("Debug")]

    // Indica si se muestran las celdas ocupadas mediante Gizmos
    [SerializeField] private bool showOccupiedCells = true;

    // Guarda la cantidad actual de celdas ocupadas
    [SerializeField] private int occupiedCellCount;

    // Guarda todas las coordenadas de las celdas ocupadas
    [SerializeField] private List<Vector2Int> occupiedCells = new List<Vector2Int>();

    // Guarda la ultima posicion conocida del objeto
    private Vector3 lastPosition;

    // Guarda la ultima rotacion conocida del objeto
    private Quaternion lastRotation;

    // Guarda la ultima escala conocida del objeto
    private Vector3 lastScale;

    // Devuelve la cantidad actual de celdas ocupadas
    public int OccupiedCellCount => occupiedCells.Count;

    // Devuelve la lista de celdas ocupadas sin permitir modificarla directamente
    public IReadOnlyList<Vector2Int> OccupiedCells => occupiedCells;

    // Busca la cuadricula y calcula la ocupacion cuando el componente se activa
    private void OnEnable()
    {
        FindGrid();
        RefreshOccupation();
        SaveTransform();
    }

    // Comprueba cada frame si el objeto ha cambiado de posicion rotacion o escala
    private void Update()
    {
        if (!updateWhenTransformChanges)
            return;

        if (TransformChanged())
        {
            RefreshOccupation();
            SaveTransform();
        }
    }

    // Libera las celdas ocupadas cuando el componente se desactiva
    private void OnDisable()
    {
        ReleaseOccupiedCells();
    }

    // Libera las celdas ocupadas cuando el objeto es destruido
    private void OnDestroy()
    {
        ReleaseOccupiedCells();
    }

    // Busca automaticamente un MeshGrid en la escena si no existe una referencia
    private void FindGrid()
    {
        if (grid != null)
            return;

#if UNITY_2022_2_OR_NEWER
        grid = FindFirstObjectByType<MeshGrid>();
#else
        grid = FindObjectOfType<MeshGrid>();
#endif
    }

    // Recalcula y registra todas las celdas ocupadas actualmente por el objeto
    [ContextMenu("Recalcular ocupacion")]
    public void RefreshOccupation()
    {
        if (grid == null)
        {
            FindGrid();

            if (grid == null)
            {
                Debug.LogWarning(
                    $"No se encontro ningun MeshGrid para {name}.",
                    this
                );

                return;
            }
        }

        // Guarda temporalmente las nuevas celdas detectadas
        List<Vector2Int> detectedCells = DetectOccupiedCells();

        // Recorre las nuevas celdas para comprobar si alguna pertenece a otro objeto
        foreach (Vector2Int cell in detectedCells)
        {
            if (grid.IsCellOccupiedByOther(cell, gameObject))
            {
                Debug.LogWarning(
                    $"{name} intenta ocupar la celda {cell}, pero ya esta ocupada por otro objeto.",
                    this
                );

                return;
            }
        }

        ReleaseOccupiedCells();

        // Registra cada nueva celda como ocupada por este objeto
        foreach (Vector2Int cell in detectedCells)
        {
            if (grid.TryOccupyCell(cell, gameObject))
                occupiedCells.Add(cell);
        }

        occupiedCellCount = occupiedCells.Count;
    }

    // Detecta todas las celdas que son ocupadas por los Colliders o Renderers del objeto
    private List<Vector2Int> DetectOccupiedCells()
    {
        // Lista utilizada para almacenar las celdas detectadas
        List<Vector2Int> result = new List<Vector2Int>();

        // Guarda los limites generales del objeto
        Bounds objectBounds;

        // Obtiene todos los Colliders pertenecientes al objeto y sus hijos
        Collider[] colliders = GetComponentsInChildren<Collider>(true);

        if (TryGetColliderBounds(colliders, out objectBounds))
        {
            DetectUsingColliders(
                colliders,
                objectBounds,
                result
            );

            return result;
        }

        // Obtiene todos los Renderers cuando el objeto no contiene Colliders
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

        if (TryGetRendererBounds(renderers, out objectBounds))
        {
            DetectUsingRendererBounds(
                objectBounds,
                result
            );
        }

        return result;
    }

    // Recorre las posibles celdas utilizando los limites generales de los Colliders
    private void DetectUsingColliders(
        Collider[] objectColliders,
        Bounds objectBounds,
        List<Vector2Int> result)
    {
        // Obtiene la primera celda posible ocupada por los limites del objeto
        Vector2Int minCell = grid.WorldToCell(objectBounds.min);

        // Obtiene la ultima celda posible ocupada por los limites del objeto
        Vector2Int maxCell = grid.WorldToCell(objectBounds.max);

        // Recorre todas las columnas comprendidas dentro de los limites del objeto
        for (int x = minCell.x; x <= maxCell.x; x++)
        {
            // Recorre todas las filas comprendidas dentro de los limites del objeto
            for (int z = minCell.y; z <= maxCell.y; z++)
            {
                // Crea las coordenadas de la celda que se esta comprobando
                Vector2Int cell = new Vector2Int(x, z);

                if (!grid.IsCellInside(cell))
                    continue;

                if (DoesObjectOverlapCell(cell, objectBounds))
                    result.Add(cell);
            }
        }
    }

    // Comprueba si algun Collider del objeto entra dentro del espacio de una celda
    private bool DoesObjectOverlapCell(
        Vector2Int cell,
        Bounds objectBounds)
    {
        // Obtiene la posicion central de la celda en coordenadas del mundo
        Vector3 cellWorldPosition = grid.CellToWorldCenter(cell);

        // Define el centro de la caja utilizada para comprobar la celda
        Vector3 checkCenter = new Vector3(
            cellWorldPosition.x,
            objectBounds.center.y,
            cellWorldPosition.z
        );

        // Calcula la mitad del ancho de la celda aplicando el margen de comprobacion
        float halfCell = grid.CellSize * 0.5f * cellCheckScale;

        // Define el tamano medio de la caja utilizada para detectar Colliders
        Vector3 halfExtents = new Vector3(
            halfCell,
            Mathf.Max(objectBounds.extents.y, 0.05f),
            halfCell
        );

        // Obtiene todos los Colliders que se encuentran dentro de la celda
        Collider[] hits = Physics.OverlapBox(
            checkCenter,
            halfExtents,
            Quaternion.identity,
            ~0,
            QueryTriggerInteraction.Collide
        );

        // Recorre todos los Colliders encontrados dentro de la celda
        foreach (Collider hit in hits)
        {
            if (IsOwnCollider(hit))
                return true;
        }

        return false;
    }

    // Comprueba si un Collider pertenece a este objeto o a alguno de sus hijos
    private bool IsOwnCollider(Collider target)
    {
        if (target == null)
            return false;

        // Guarda el Transform del Collider que se esta comprobando
        Transform targetTransform = target.transform;

        return targetTransform == transform ||
               targetTransform.IsChildOf(transform);
    }

    // Calcula unos limites generales utilizando todos los Colliders activos del objeto
    private bool TryGetColliderBounds(
        Collider[] colliders,
        out Bounds bounds)
    {
        // Guarda los limites combinados de todos los Colliders
        bounds = new Bounds();

        // Indica si se ha encontrado al menos un Collider valido
        bool found = false;

        // Recorre todos los Colliders encontrados en el objeto
        foreach (Collider col in colliders)
        {
            if (col == null || !col.enabled)
                continue;

            if (!found)
            {
                bounds = col.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(col.bounds);
            }
        }

        return found;
    }

    // Calcula unos limites generales utilizando todos los Renderers activos del objeto
    private bool TryGetRendererBounds(
        Renderer[] renderers,
        out Bounds bounds)
    {
        // Guarda los limites combinados de todos los Renderers
        bounds = new Bounds();

        // Indica si se ha encontrado al menos un Renderer valido
        bool found = false;

        // Recorre todos los Renderers encontrados en el objeto
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || !renderer.enabled)
                continue;

            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return found;
    }

    // Detecta de forma aproximada las celdas ocupadas utilizando los limites del Renderer
    private void DetectUsingRendererBounds(
        Bounds objectBounds,
        List<Vector2Int> result)
    {
        // Obtiene la primera celda incluida dentro de los limites del objeto
        Vector2Int minCell = grid.WorldToCell(objectBounds.min);

        // Obtiene la ultima celda incluida dentro de los limites del objeto
        Vector2Int maxCell = grid.WorldToCell(objectBounds.max);

        // Recorre todas las columnas que pueden ser ocupadas por el objeto
        for (int x = minCell.x; x <= maxCell.x; x++)
        {
            // Recorre todas las filas que pueden ser ocupadas por el objeto
            for (int z = minCell.y; z <= maxCell.y; z++)
            {
                // Guarda las coordenadas de la celda actual
                Vector2Int cell = new Vector2Int(x, z);

                if (!grid.IsCellInside(cell))
                    continue;

                // Obtiene el centro de la celda actual en el mundo
                Vector3 center = grid.CellToWorldCenter(cell);

                // Calcula la mitad del tamano de una celda
                float halfCell = grid.CellSize * 0.5f;

                // Calcula el limite izquierdo de la celda
                float cellMinX = center.x - halfCell;

                // Calcula el limite derecho de la celda
                float cellMaxX = center.x + halfCell;

                // Calcula el limite inferior de la celda
                float cellMinZ = center.z - halfCell;

                // Calcula el limite superior de la celda
                float cellMaxZ = center.z + halfCell;

                // Comprueba si el objeto coincide con la celda en el eje X
                bool overlapsX =
                    objectBounds.max.x > cellMinX &&
                    objectBounds.min.x < cellMaxX;

                // Comprueba si el objeto coincide con la celda en el eje Z
                bool overlapsZ =
                    objectBounds.max.z > cellMinZ &&
                    objectBounds.min.z < cellMaxZ;

                if (overlapsX && overlapsZ)
                    result.Add(cell);
            }
        }
    }

    // Libera todas las celdas que estaban registradas como ocupadas por este objeto
    public void ReleaseOccupiedCells()
    {
        if (grid == null)
        {
            occupiedCells.Clear();
            occupiedCellCount = 0;
            return;
        }

        // Recorre todas las celdas registradas actualmente por este objeto
        foreach (Vector2Int cell in occupiedCells)
        {
            grid.ReleaseCell(
                cell,
                gameObject
            );
        }

        occupiedCells.Clear();
        occupiedCellCount = 0;
    }

    // Comprueba si la posicion rotacion o escala del objeto ha cambiado
    private bool TransformChanged()
    {
        return
            transform.position != lastPosition ||
            transform.rotation != lastRotation ||
            transform.lossyScale != lastScale;
    }

    // Guarda el estado actual del Transform para detectar futuros cambios
    private void SaveTransform()
    {
        lastPosition = transform.position;
        lastRotation = transform.rotation;
        lastScale = transform.lossyScale;
    }

    // Dibuja en el editor las celdas que actualmente ocupa el objeto
    private void OnDrawGizmosSelected()
    {
        if (!showOccupiedCells)
            return;

        if (grid == null)
            return;

        Gizmos.color = Color.red;

        // Recorre todas las celdas ocupadas para dibujarlas en la escena
        foreach (Vector2Int cell in occupiedCells)
        {
            // Obtiene el centro de la celda que se va a dibujar
            Vector3 center = grid.CellToWorldCenter(
                cell,
                0.1f
            );

            // Define el tamano del Gizmo que representa la celda ocupada
            Vector3 size = new Vector3(
                grid.CellSize * 0.9f,
                0.05f,
                grid.CellSize * 0.9f
            );

            Gizmos.DrawWireCube(
                center,
                size
            );
        }
    }
}