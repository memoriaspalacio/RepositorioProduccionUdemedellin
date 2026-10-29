using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Edita a ojo las zonas de calificacion del corazon. Con la pista seleccionada (BeatTrackLane,
/// dentro de Gametime UI) la Scene view pinta BAD, GOOD y PERFECT sobre el corazon con los
/// colores del juego y deja arrastrar sus bordes:
///   - Inicio de la ventana: mueve el borde izquierdo de la caja de BeatWindowZone.
///   - Final de la ventana: mueve el borde izquierdo de la caja de BeatDestroyZone.
///   - GOOD y PERFECT: cambian goodWindowSeconds y perfectWindowSeconds del juez de la escena.
///
/// Funciona tambien en Play: la pista del juego se redibuja al momento y lo ajustado se
/// conserva al salir de Play (Unity normalmente lo descartaria).
///
/// GOOD y PERFECT son del juez, que esta en cada escena: se guardan por escena. Las cajas son
/// del prefab Gametime UI: al moverlas en una escena quedan como override; para todas las
/// escenas, aplica el override al prefab (o edita con el prefab abierto).
/// </summary>
[CustomEditor(typeof(BeatTrackLane))]
public class EditorZonasCorazon : Editor
{
    const string MenuEditar = "Herramientas/Ritmo/Editar zonas del corazon";

    /// <summary>Lo cambiado durante Play, por escena, para volver a aplicarlo al salir.</summary>
    class CambiosPlay
    {
        public float? perfect;
        public float? good;
        public float? inicioVentana;
        public float? finVentana;
    }

    static readonly Dictionary<string, CambiosPlay> cambiosPlay = new Dictionary<string, CambiosPlay>();

    struct Contexto
    {
        public BeatTrackUI pista;
        public BeatActionJudge juez;
        public RectTransform cajaInicio;
        public RectTransform cajaFin;
        public Vector3 corazon;
        public Vector3 direccion;
        public Vector3 normal;
        public float unidadesPorBeat;
        public float segundosPorBeat;
        public float alto;
    }

    #region Menu y Play

    [InitializeOnLoadMethod]
    static void Registrar()
    {
        EditorApplication.playModeStateChanged -= AlCambiarModo;
        EditorApplication.playModeStateChanged += AlCambiarModo;
    }

    [MenuItem(MenuEditar)]
    static void SeleccionarPista()
    {
        BeatTrackLane pista = Object.FindFirstObjectByType<BeatTrackLane>(FindObjectsInactive.Include);
        if (pista == null)
        {
            EditorUtility.DisplayDialog("Zonas del corazon", "La escena abierta no tiene una pista de ritmo (BeatTrackLane, dentro de Gametime UI).", "Vale");
            return;
        }

        Selection.activeGameObject = pista.gameObject;
        SceneView vista = SceneView.lastActiveSceneView;
        BeatTrackUI ui = Object.FindFirstObjectByType<BeatTrackUI>(FindObjectsInactive.Include);
        if (vista != null && ui != null && ui.HeartPoint != null)
        {
            vista.in2DMode = true;
            vista.Frame(new Bounds(ui.HeartPoint.position, Vector3.one * 400f * ui.HeartPoint.lossyScale.x), false);
        }
    }

    static void AlCambiarModo(PlayModeStateChange cambio)
    {
        if (cambio != PlayModeStateChange.EnteredEditMode || cambiosPlay.Count == 0)
            return;

        // Solo la escena activa: es la que tenia la pista seleccionada en Play
        Scene escena = SceneManager.GetActiveScene();
        if (cambiosPlay.TryGetValue(escena.path, out CambiosPlay cambios) && Preparar(out Contexto c))
        {
            if (cambios.perfect.HasValue || cambios.good.HasValue)
            {
                SerializedObject juez = new SerializedObject(c.juez);
                if (cambios.perfect.HasValue)
                    juez.FindProperty("perfectWindowSeconds").floatValue = cambios.perfect.Value;
                if (cambios.good.HasValue)
                    juez.FindProperty("goodWindowSeconds").floatValue = cambios.good.Value;
                juez.ApplyModifiedProperties();
            }

            if (cambios.inicioVentana.HasValue && c.cajaInicio != null)
                MoverBordeIzquierdo(c.cajaInicio, cambios.inicioVentana.Value);
            if (cambios.finVentana.HasValue && c.cajaFin != null)
                MoverBordeIzquierdo(c.cajaFin, cambios.finVentana.Value);

            EditorSceneManager.MarkSceneDirty(escena);
            Debug.Log("Zonas del corazon: se conservaron los cambios hechos en Play. Guarda la escena para quedartelos.");
        }

        cambiosPlay.Clear();
    }

    static CambiosPlay CambiosDeEscena()
    {
        string ruta = SceneManager.GetActiveScene().path;
        if (!cambiosPlay.TryGetValue(ruta, out CambiosPlay cambios))
        {
            cambios = new CambiosPlay();
            cambiosPlay[ruta] = cambios;
        }

        return cambios;
    }

    #endregion

    #region Inspector

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("Zonas del corazón", EditorStyles.boldLabel);

        if (!Preparar(out Contexto c))
        {
            EditorGUILayout.HelpBox("Abre una escena de nivel: el juez (BeatActionJudge) y la pista (BeatTrackUI) están en la escena, no en el prefab.", MessageType.Info);
            return;
        }

        EditorGUILayout.HelpBox("Arrastra los bordes en la Scene view, sobre el corazón. También funciona en Play y se conserva al salir.", MessageType.None);

        SerializedObject juez = new SerializedObject(c.juez);
        SerializedProperty perfect = juez.FindProperty("perfectWindowSeconds");
        SerializedProperty good = juez.FindProperty("goodWindowSeconds");

        EditorGUI.BeginChangeCheck();
        float perfectMs = EditorGUILayout.Slider("PERFECTO (± ms)", perfect.floatValue * 1000f, 0f, 300f);
        float goodMs = EditorGUILayout.Slider("BIEN (± ms)", good.floatValue * 1000f, 0f, 500f);
        if (EditorGUI.EndChangeCheck())
        {
            FijarVentanas(c, perfectMs / 1000f, goodMs / 1000f);
            SceneView.RepaintAll();
        }

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.FloatField("Ventana se abre (beats)", BeatsHastaCorazon(c, c.cajaInicio, -1f));
            EditorGUILayout.FloatField("Ventana se cierra (beats)", BeatsHastaCorazon(c, c.cajaFin, 1f));
        }

        if (c.cajaInicio == null || c.cajaFin == null)
            EditorGUILayout.HelpBox("No se encontraron las cajas de BeatWindowZone / BeatDestroyZone: el inicio y el final de la ventana no se pueden arrastrar.", MessageType.Warning);
    }

    #endregion

    #region Scene view

    void OnSceneGUI()
    {
        if (!Preparar(out Contexto c))
            return;

        float upb = c.unidadesPorBeat;
        float ups = upb / c.segundosPorBeat;
        float perfect = c.juez.PerfectWindowSeconds * ups;
        float good = c.juez.GoodWindowSeconds * ups;

        float inicio = c.cajaInicio != null ? DistanciaAlCorazon(c, BordeIzquierdo(c.cajaInicio)) : -c.juez.EarlyTolerance * upb;
        float fin = c.cajaFin != null ? DistanciaAlCorazon(c, BordeIzquierdo(c.cajaFin)) : c.juez.LateTolerance * upb;
        float temprana = Mathf.Max(0f, -inicio);
        float tardia = Mathf.Max(0f, fin);

        // Zonas: BAD toda la ventana, GOOD y PERFECT encima, recortadas a la ventana
        Zona(c, -temprana, tardia, ColoresCalificacion.Bad, 0.18f);
        Zona(c, -Mathf.Min(good, temprana), Mathf.Min(good, tardia), ColoresCalificacion.Good, 0.28f);
        Zona(c, -Mathf.Min(perfect, temprana), Mathf.Min(perfect, tardia), ColoresCalificacion.Perfect, 0.4f);

        Handles.color = Color.white;
        Handles.DrawLine(Punto(c, 0f, -0.6f), Punto(c, 0f, 0.6f), 2f);

        // Bordes arrastrables
        if (c.cajaInicio != null)
        {
            float nuevo = Borde(c, -temprana, 0.75f, ColoresCalificacion.Bad, $"Abre\n{temprana / upb:0.00} beats");
            if (!Mathf.Approximately(nuevo, -temprana))
                FijarBorde(c, c.cajaInicio, Mathf.Min(nuevo, -1f), false);
        }

        if (c.cajaFin != null)
        {
            float nuevo = Borde(c, tardia, 0.75f, ColoresCalificacion.Miss, $"Cierra\n{tardia / upb:0.00} beats");
            if (!Mathf.Approximately(nuevo, tardia))
                FijarBorde(c, c.cajaFin, Mathf.Max(nuevo, 1f), true);
        }

        float nuevoGood = Mathf.Abs(Borde(c, -good, 0.45f, ColoresCalificacion.Good, $"BIEN\n±{c.juez.GoodWindowSeconds * 1000f:0} ms"));
        float nuevoGoodTarde = Mathf.Abs(Borde(c, good, 0.45f, ColoresCalificacion.Good, null));
        if (!Mathf.Approximately(nuevoGood, good) || !Mathf.Approximately(nuevoGoodTarde, good))
        {
            float valor = !Mathf.Approximately(nuevoGood, good) ? nuevoGood : nuevoGoodTarde;
            FijarVentanas(c, Mathf.Min(c.juez.PerfectWindowSeconds, valor / ups), valor / ups);
        }

        float nuevoPerfect = Mathf.Abs(Borde(c, -perfect, 0.2f, ColoresCalificacion.Perfect, $"PERFECTO\n±{c.juez.PerfectWindowSeconds * 1000f:0} ms"));
        float nuevoPerfectTarde = Mathf.Abs(Borde(c, perfect, 0.2f, ColoresCalificacion.Perfect, null));
        if (!Mathf.Approximately(nuevoPerfect, perfect) || !Mathf.Approximately(nuevoPerfectTarde, perfect))
        {
            float valor = !Mathf.Approximately(nuevoPerfect, perfect) ? nuevoPerfect : nuevoPerfectTarde;
            FijarVentanas(c, valor / ups, Mathf.Max(c.juez.GoodWindowSeconds, valor / ups));
        }
    }

    /// <summary>Un borde con su asa: devuelve la nueva distancia al corazon (negativa = antes).</summary>
    static float Borde(Contexto c, float distancia, float alturaAsa, Color color, string etiqueta)
    {
        Vector3 punto = Punto(c, distancia, 0f);
        Vector3 asa = Punto(c, distancia, alturaAsa);

        Handles.color = color;
        Handles.DrawLine(Punto(c, distancia, -0.5f), asa, 2f);

        if (etiqueta != null)
        {
            GUIStyle estilo = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.LowerCenter };
            estilo.normal.textColor = color;
            Handles.Label(asa + c.normal * HandleUtility.GetHandleSize(asa) * 0.35f, etiqueta, estilo);
        }

        float tamano = HandleUtility.GetHandleSize(asa) * 0.09f;
        Vector3 movido = Handles.Slider(asa, c.direccion, tamano, Handles.CubeHandleCap, 0f);
        return distancia + Vector3.Dot(movido - asa, c.direccion);
    }

    static void Zona(Contexto c, float desde, float hasta, Color color, float opacidad)
    {
        if (hasta - desde <= 0.01f)
            return;

        Vector3[] esquinas =
        {
            Punto(c, desde, -0.5f), Punto(c, desde, 0.5f), Punto(c, hasta, 0.5f), Punto(c, hasta, -0.5f)
        };
        Handles.DrawSolidRectangleWithOutline(esquinas, new Color(color.r, color.g, color.b, opacidad), new Color(color.r, color.g, color.b, 0.8f));
    }

    /// <summary>Punto a 'distancia' del corazon sobre la pista y a 'altura' (en altos de zona) de ella.</summary>
    static Vector3 Punto(Contexto c, float distancia, float altura)
    {
        return c.corazon + c.direccion * distancia + c.normal * altura * c.alto;
    }

    #endregion

    #region Datos

    /// <summary>Junta pista, juez y cajas de la escena y la geometria en coordenadas de mundo.</summary>
    static bool Preparar(out Contexto c)
    {
        c = default;
        c.pista = Object.FindFirstObjectByType<BeatTrackUI>(FindObjectsInactive.Include);
        c.juez = Object.FindFirstObjectByType<BeatActionJudge>(FindObjectsInactive.Include);
        if (c.pista == null || c.juez == null || c.pista.SpawnPoint == null || c.pista.HeartPoint == null)
            return false;

        Vector3 aparicion = c.pista.SpawnPoint.position;
        c.corazon = c.pista.HeartPoint.position;
        Vector3 recorrido = c.corazon - aparicion;
        if (recorrido.magnitude < 0.001f)
            return false;

        c.direccion = recorrido.normalized;
        c.normal = Vector3.Cross(Vector3.forward, c.direccion).normalized;
        if (Vector3.Dot(c.normal, c.pista.HeartPoint.up) < 0f)
            c.normal = -c.normal;

        c.unidadesPorBeat = recorrido.magnitude / Mathf.Max(0.01f, c.pista.TravelBeats);
        c.segundosPorBeat = SegundosPorBeat();
        c.alto = 36f * c.pista.HeartPoint.lossyScale.y;

        BeatWindowZone ventana = Object.FindFirstObjectByType<BeatWindowZone>(FindObjectsInactive.Include);
        BeatDestroyZone destruccion = Object.FindFirstObjectByType<BeatDestroyZone>(FindObjectsInactive.Include);
        c.cajaInicio = ventana != null ? new SerializedObject(ventana).FindProperty("windowBox").objectReferenceValue as RectTransform : null;
        c.cajaFin = destruccion != null ? new SerializedObject(destruccion).FindProperty("destroyBox").objectReferenceValue as RectTransform : null;
        return true;
    }

    static float SegundosPorBeat()
    {
        if (BeatManager.Instance != null)
            return BeatManager.Instance.SecPerBeat;

        BeatManager manager = Object.FindFirstObjectByType<BeatManager>(FindObjectsInactive.Include);
        if (manager == null)
            return 60f / 280f;

        SerializedObject datos = new SerializedObject(manager);
        float bpm = datos.FindProperty("bpm").floatValue;
        float velocidad = datos.FindProperty("speedMultiplier").floatValue;
        return 60f / Mathf.Max(1f, bpm * Mathf.Max(0.01f, velocidad));
    }

    static Vector3 BordeIzquierdo(RectTransform caja)
    {
        Rect rect = caja.rect;
        return caja.TransformPoint(new Vector3(rect.xMin, rect.center.y, 0f));
    }

    static float DistanciaAlCorazon(Contexto c, Vector3 punto)
    {
        return Vector3.Dot(punto - c.corazon, c.direccion);
    }

    static float BeatsHastaCorazon(Contexto c, RectTransform caja, float signo)
    {
        if (caja == null)
            return signo < 0f ? c.juez.EarlyTolerance : c.juez.LateTolerance;

        return signo * DistanciaAlCorazon(c, BordeIzquierdo(caja)) / c.unidadesPorBeat;
    }

    static void FijarVentanas(Contexto c, float perfect, float good)
    {
        perfect = Mathf.Max(0f, perfect);
        good = Mathf.Max(perfect, good);

        SerializedObject juez = new SerializedObject(c.juez);
        juez.FindProperty("perfectWindowSeconds").floatValue = perfect;
        juez.FindProperty("goodWindowSeconds").floatValue = good;
        juez.ApplyModifiedProperties();

        if (EditorApplication.isPlaying)
        {
            CambiosPlay cambios = CambiosDeEscena();
            cambios.perfect = perfect;
            cambios.good = good;
        }
    }

    /// <summary>Mueve el borde izquierdo de una caja a 'distancia' del corazon, sin mover el derecho.</summary>
    static void FijarBorde(Contexto c, RectTransform caja, float distancia, bool esFin)
    {
        Vector3 mundo = c.corazon + c.direccion * distancia;
        float izquierdaLocal = caja.parent != null ? caja.parent.InverseTransformPoint(mundo).x : mundo.x;
        MoverBordeIzquierdo(caja, izquierdaLocal);

        if (EditorApplication.isPlaying)
        {
            CambiosPlay cambios = CambiosDeEscena();
            if (esFin)
                cambios.finVentana = izquierdaLocal;
            else
                cambios.inicioVentana = izquierdaLocal;
        }
    }

    /// <summary>Borde izquierdo de la caja en x local de su padre; el derecho se queda donde esta.</summary>
    static void MoverBordeIzquierdo(RectTransform caja, float izquierdaLocal)
    {
        Undo.RecordObject(caja, "Mover zona del corazon");

        Rect rect = caja.rect;
        float derecha = caja.localPosition.x + rect.xMax;
        float ancho = Mathf.Max(1f, derecha - izquierdaLocal);

        caja.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, ancho);
        Vector3 posicion = caja.localPosition;
        posicion.x = derecha - ancho + caja.pivot.x * ancho;
        caja.localPosition = posicion;

        PrefabUtility.RecordPrefabInstancePropertyModifications(caja);
    }

    #endregion
}
