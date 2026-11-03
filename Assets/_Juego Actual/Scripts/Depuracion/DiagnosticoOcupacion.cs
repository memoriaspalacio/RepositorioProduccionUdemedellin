#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Text;
using UdeM.Characters;
using UnityEngine;

/// <summary>
/// Diagnostico de solapamiento en la cuadricula (solo en el editor y en builds de desarrollo).
/// Durante la partida revisa a todos los personajes y avisa en consola cuando:
/// - dos personajes se ven en la misma casilla,
/// - un personaje esta registrado en una casilla pero su raiz o su modelo estan en otra,
/// - un personaje no tiene ninguna casilla registrada.
/// Se crea solo en cada escena con MeshGrid; cada aviso se repite como mucho cada pocos segundos.
/// </summary>
public class DiagnosticoOcupacion : MonoBehaviour
{
    const float Intervalo = 0.2f;
    const float SilencioPorAviso = 3f;
    // Un problema tiene que durar esto para avisarse: al caminar o saltar entre casillas el
    // modelo pasa un instante fuera de la suya y eso es normal.
    const float Persistencia = 0.5f;

    MeshGrid grid;
    float siguienteRevision;
    readonly Dictionary<string, float> ultimoAviso = new Dictionary<string, float>();
    readonly Dictionary<string, float> primeraVez = new Dictionary<string, float>();
    readonly HashSet<string> vistosAhora = new HashSet<string>();
    readonly List<string> porOlvidar = new List<string>();
    readonly Dictionary<Vector2Int, List<string>> vistosPorCelda = new Dictionary<Vector2Int, List<string>>();
    readonly HashSet<GameObject> ocupantes = new HashSet<GameObject>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Instalar()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (escena, modo) => Crear();
        Crear();
    }

    static void Crear()
    {
        if (FindFirstObjectByType<DiagnosticoOcupacion>() != null)
            return;
        if (FindFirstObjectByType<MeshGrid>() == null)
            return;

        new GameObject("Diagnostico ocupacion").AddComponent<DiagnosticoOcupacion>();
    }

    void Update()
    {
        if (Time.time < siguienteRevision)
            return;
        siguienteRevision = Time.time + Intervalo;

        if (grid == null)
            grid = FindFirstObjectByType<MeshGrid>();
        if (grid == null)
            return;

        vistosPorCelda.Clear();
        ocupantes.Clear();
        vistosAhora.Clear();

        // 1. Cada ocupante registrado: su raiz y su modelo deberian estar en su casilla.
        foreach (KeyValuePair<Vector2Int, GameObject> par in grid.OccupiedCellsView)
        {
            GameObject ocupante = par.Value;
            if (ocupante == null)
                continue;
            if (!EsPersonaje(ocupante))
                continue;

            ocupantes.Add(ocupante);

            Vector2Int celdaRaiz = grid.WorldToCell(ocupante.transform.position);
            Vector2Int celdaModelo = grid.WorldToCell(CentroVisible(ocupante));

            if (celdaRaiz != par.Key)
                Avisar($"raiz {ocupante.GetInstanceID()}",
                    $"{Nombre(ocupante)} esta registrado en {par.Key} pero su raiz esta en {celdaRaiz} " +
                    $"(posicion {ocupante.transform.position}). La logica y la posicion se separaron.", ocupante);

            if (celdaModelo != par.Key)
                Avisar($"modelo {ocupante.GetInstanceID()}",
                    $"{Nombre(ocupante)} esta registrado en {par.Key} pero su modelo se ve en {celdaModelo} " +
                    $"(centro visible {CentroVisible(ocupante)}). El modelo esta desplazado de su raiz.", ocupante);

            Anotar(celdaModelo, Nombre(ocupante));
        }

        // 2. Personajes que deberian ocupar una casilla y no la tienen.
        RevisarSinCasilla(FindObjectsByType<GridPlayerMovement>(FindObjectsSortMode.None));
        RevisarSinCasilla(FindObjectsByType<Character3DNavMeshGridNPCBehaviour>(FindObjectsSortMode.None));

        MonkCrossBehaviour[] cruces = FindObjectsByType<MonkCrossBehaviour>(FindObjectsSortMode.None);
        for (int i = 0; i < cruces.Length; i++)
        {
            MonkCrossBehaviour cruz = cruces[i];
            if (cruz.IsDead || !cruz.gameObject.activeInHierarchy)
                continue;

            // Las cruces poseidas flotan alrededor del Monk sin ocupar casilla (es intencional).
            if (cruz.IsPossessed)
                continue;

            if (!ocupantes.Contains(cruz.gameObject) && !cruz.IsAttacking)
                Avisar($"sin casilla {cruz.GetInstanceID()}",
                    $"{Nombre(cruz.gameObject)} esta suelta en {grid.WorldToCell(cruz.transform.position)} sin casilla registrada.", cruz.gameObject);
        }

        // 3. Dos personajes que se ven en la misma casilla.
        foreach (KeyValuePair<Vector2Int, List<string>> par in vistosPorCelda)
        {
            if (par.Value.Count < 2)
                continue;

            par.Value.Sort();
            string nombres = string.Join(" y ", par.Value);
            Avisar($"misma {par.Key} {nombres}", $"SOLAPAMIENTO: {nombres} se ven en la misma casilla {par.Key}.", null);
        }

        // Olvida los problemas que ya no estan para que, si vuelven, cuenten desde cero.
        porOlvidar.Clear();
        foreach (string clave in primeraVez.Keys)
        {
            if (!vistosAhora.Contains(clave))
                porOlvidar.Add(clave);
        }
        for (int i = 0; i < porOlvidar.Count; i++)
            primeraVez.Remove(porOlvidar[i]);
    }

    void RevisarSinCasilla<T>(T[] personajes) where T : MonoBehaviour
    {
        for (int i = 0; i < personajes.Length; i++)
        {
            T personaje = personajes[i];
            if (personaje == null || !personaje.gameObject.activeInHierarchy)
                continue;

            GameObject objeto = personaje.gameObject;
            if (ocupantes.Contains(objeto))
                continue;

            Vector2Int celdaModelo = grid.WorldToCell(CentroVisible(objeto));
            Anotar(celdaModelo, Nombre(objeto));

            string estado = personaje.enabled ? "activo" : "con el script DESACTIVADO";
            Avisar($"sin casilla {objeto.GetInstanceID()}",
                $"{Nombre(objeto)} ({typeof(T).Name}, {estado}) no tiene casilla registrada; se ve en {celdaModelo}. " +
                "Nada impide que otro personaje entre ahi.", objeto);
        }
    }

    void Anotar(Vector2Int celda, string nombre)
    {
        if (!vistosPorCelda.TryGetValue(celda, out List<string> lista))
        {
            lista = new List<string>();
            vistosPorCelda[celda] = lista;
        }
        if (!lista.Contains(nombre))
            lista.Add(nombre);
    }

    static bool EsPersonaje(GameObject objeto)
    {
        return objeto.GetComponent<GridPlayerMovement>() != null ||
               objeto.GetComponent<Character3DNavMeshGridNPCBehaviour>() != null ||
               objeto.GetComponent<MonkCrossBehaviour>() != null;
    }

    // Centro de lo que realmente se ve del personaje (sin efectos, lineas ni particulas).
    static Vector3 CentroVisible(GameObject objeto)
    {
        Renderer[] renderers = objeto.GetComponentsInChildren<Renderer>();
        Bounds limites = new Bounds(objeto.transform.position, Vector3.zero);
        bool hay = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer render = renderers[i];
            if (!render.enabled)
                continue;
            if (render is ParticleSystemRenderer || render is TrailRenderer || render is LineRenderer)
                continue;
            // Las cruces poseidas son hijas del Monk pero no son parte de su cuerpo.
            if (render.GetComponentInParent<MonkCrossBehaviour>() != null &&
                objeto.GetComponent<MonkCrossBehaviour>() == null)
                continue;

            if (!hay)
            {
                limites = render.bounds;
                hay = true;
            }
            else
            {
                limites.Encapsulate(render.bounds);
            }
        }

        return limites.center;
    }

    static string Nombre(GameObject objeto)
    {
        StringBuilder nombre = new StringBuilder(objeto.name);
        Transform padre = objeto.transform.parent;
        if (padre != null)
            nombre.Insert(0, padre.name + "/");
        return nombre.ToString();
    }

    void Avisar(string clave, string mensaje, Object contexto)
    {
        vistosAhora.Add(clave);

        if (!primeraVez.TryGetValue(clave, out float desde))
        {
            primeraVez[clave] = Time.time;
            return;
        }

        if (Time.time - desde < Persistencia)
            return;

        if (ultimoAviso.TryGetValue(clave, out float ultimo) && Time.time - ultimo < SilencioPorAviso)
            return;

        ultimoAviso[clave] = Time.time;
        Debug.LogWarning("[Ocupacion] " + mensaje, contexto);
    }
}
#endif
