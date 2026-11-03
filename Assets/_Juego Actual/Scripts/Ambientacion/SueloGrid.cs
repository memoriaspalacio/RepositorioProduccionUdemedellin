using System.Collections.Generic;
using UnityEngine;

// Plano de suelo alineado al MeshGrid: cubre el suelo modelado, le pasa al shader el origen y tamano
// de las celdas (para que las juntas caigan sobre el grid) y la luz horneada capturada del suelo original.
//
// En juego el plano tambien es el suelo fisico: el MeshGrid coloca las celdas (jugador, enemigos, el fuego
// de las monjas) y el medidor de ritmo se apoya con rayos contra el suelo, y si solo encontraran el collider
// del suelo modelado seguirian sus relieves y juntas aunque ya no se vean.
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class SueloGrid : MonoBehaviour
{
    public enum ModoAltura { CaraDeLasPiezas, TopeDelSuelo, Manual }

    static readonly int IdTamano = Shader.PropertyToID("_GridSize");
    static readonly int IdOrigen = Shader.PropertyToID("_GridOrigin");
    static readonly int IdLuz = Shader.PropertyToID("_LuzSuelo");
    static readonly int IdLuzRect = Shader.PropertyToID("_LuzSueloRect");
    static readonly int IdUsarLuz = Shader.PropertyToID("_UsarLuzSuelo");

    static MaterialPropertyBlock bloque;

    [Tooltip("Grid al que se alinea el suelo. Si esta vacio se busca en la escena.")]
    public MeshGrid meshGrid;
    [Tooltip("Deja de dibujar el suelo modelado. En el editor sigue existiendo para colisiones, navmesh y el MeshGrid; en juego su collider se apaga (ver colisionPlana).")]
    public bool ocultarSueloOriginal = true;
    [Tooltip("En juego pone un collider plano a la altura del plano, para que el grid y los rayos al suelo den con lo que se ve. Con el suelo modelado oculto ademas apaga su collider; si se ve, se queda para las partes mas altas (escalones, altar).")]
    public bool colisionPlana = true;
    [Tooltip("CaraDeLasPiezas: la cara de arriba de losas y ladrillos, sin contar juntas ni partes sueltas mas altas. TopeDelSuelo: lo mas alto del suelo modelado. Manual: la altura de abajo.")]
    public ModoAltura modoAltura = ModoAltura.CaraDeLasPiezas;
    [Tooltip("En Manual es la altura del plano; en los otros modos se suma a la altura calculada.")]
    public float altura = 0.005f;
    [Tooltip("Luz horneada del suelo original (Herramientas > Suelo Grid > Recapturar luz horneada). Se usa mientras el plano no tenga lightmap propio.")]
    public Texture2D luzHorneada;
    [HideInInspector] public Rect rectLuz;

    const float GrosorColision = 0.2f;

    Renderer sueloOriginal;
    GameObject colision;
    readonly List<Collider> collidersApagados = new List<Collider>();

    void OnEnable()
    {
        Ajustar();
        if (Application.isPlaying)
            PonerColisionPlana();
    }

    void OnValidate()
    {
        Ajustar();
    }

    void OnDisable()
    {
        if (sueloOriginal != null)
            sueloOriginal.forceRenderingOff = false;
        QuitarColisionPlana();
    }

    void PonerColisionPlana()
    {
        Renderer original = SueloOriginal();
        if (!colisionPlana || colision != null || original == null)
            return;

        // En la capa del suelo modelado, que es la que el MeshGrid y el medidor ya buscan
        colision = new GameObject("Colision suelo plano") { hideFlags = HideFlags.DontSave };
        colision.layer = original.gameObject.layer;
        colision.transform.SetParent(transform, false);

        // El Quad es de 1x1 en su XY y, girado boca arriba, su +Z local apunta hacia abajo: la caja
        // tiene la cara de arriba justo en el plano y crece hacia el suelo
        BoxCollider caja = colision.AddComponent<BoxCollider>();
        caja.size = new Vector3(1f, 1f, GrosorColision);
        caja.center = new Vector3(0f, 0f, GrosorColision * 0.5f);

        if (ocultarSueloOriginal)
        {
            foreach (Collider collider in original.GetComponents<Collider>())
            {
                if (collider.enabled && !collider.isTrigger)
                {
                    collider.enabled = false;
                    collidersApagados.Add(collider);
                }
            }
        }

        Physics.SyncTransforms();
    }

    void QuitarColisionPlana()
    {
        foreach (Collider collider in collidersApagados)
        {
            if (collider != null)
                collider.enabled = true;
        }
        collidersApagados.Clear();

        if (colision != null)
            Destroy(colision);
        colision = null;
    }

    public Renderer SueloOriginal()
    {
        if (meshGrid == null)
            return null;
        // Igual que MeshGrid: su Renderer o el primero de sus hijos.
        Renderer render = meshGrid.GetComponent<Renderer>();
        return render != null ? render : meshGrid.GetComponentInChildren<Renderer>();
    }

    public void Ajustar()
    {
        if (!gameObject.scene.IsValid())
            return;

        if (meshGrid == null)
            meshGrid = FindFirstObjectByType<MeshGrid>();

        Renderer original = SueloOriginal();
        if (original == null)
            return;

        if (sueloOriginal != null && sueloOriginal != original)
            sueloOriginal.forceRenderingOff = false;
        sueloOriginal = original;
        sueloOriginal.forceRenderingOff = ocultarSueloOriginal && isActiveAndEnabled;

        Bounds limites = original.bounds;
        // En juego el plano ya quedo guardado en su sitio; solo se recalcula en el editor.
        if (!Application.isPlaying)
            Colocar(limites);

        if (bloque == null)
            bloque = new MaterialPropertyBlock();

        bloque.Clear();
        bloque.SetFloat(IdTamano, meshGrid.CellSize);
        bloque.SetVector(IdOrigen, new Vector4(limites.min.x, limites.min.z, 0f, 0f));
        bool usarLuz = luzHorneada != null && rectLuz.width > 0f && rectLuz.height > 0f;
        if (usarLuz)
        {
            bloque.SetTexture(IdLuz, luzHorneada);
            bloque.SetVector(IdLuzRect, new Vector4(rectLuz.xMin, rectLuz.yMin, 1f / rectLuz.width, 1f / rectLuz.height));
        }
        bloque.SetFloat(IdUsarLuz, usarLuz ? 1f : 0f);
        GetComponent<MeshRenderer>().SetPropertyBlock(bloque);
    }

    void Colocar(Bounds limites)
    {
        Vector3 centro = new Vector3(limites.center.x, AlturaPlano(limites), limites.center.z);
        Vector3 escala = new Vector3(limites.size.x, limites.size.z, 1f);
        if (transform.parent != null)
        {
            Vector3 padre = transform.parent.lossyScale;
            escala = new Vector3(escala.x / padre.x, escala.y / padre.z, 1f);
        }

        // El Quad mira hacia -Z; girado 90 en X queda boca arriba.
        transform.SetPositionAndRotation(centro, Quaternion.Euler(90f, 0f, 0f));
        transform.localScale = escala;
    }

    float AlturaPlano(Bounds limites)
    {
        if (modoAltura == ModoAltura.Manual)
            return altura;
        if (modoAltura == ModoAltura.TopeDelSuelo)
            return limites.max.y + altura;

        // Rayos densos (4x4 por celda) solo contra el collider del suelo; los centros de celda no sirven porque
        // en algunos modelos caen justo en las juntas. La cara de las piezas es la altura plana que mas se repite
        // en la mitad de arriba: deja fuera juntas, biseles y partes altas pequenas como un altar.
        Physics.SyncTransforms();
        List<float> alturas = new List<float>();
        float paso = meshGrid.CellSize * 0.25f;
        float inicioRayo = limites.max.y + 10f;
        float largoRayo = inicioRayo - limites.min.y + 1f;
        for (float x = limites.min.x + paso * 0.5f; x < limites.max.x; x += paso)
        {
            for (float z = limites.min.z + paso * 0.5f; z < limites.max.z; z += paso)
            {
                RaycastHit[] golpes = Physics.RaycastAll(new Vector3(x, inicioRayo, z), Vector3.down, largoRayo, ~0, QueryTriggerInteraction.Ignore);
                float mayor = float.NegativeInfinity;
                foreach (RaycastHit golpe in golpes)
                {
                    if (golpe.collider.transform.IsChildOf(meshGrid.transform) && golpe.point.y > mayor)
                        mayor = golpe.point.y;
                }
                if (!float.IsNegativeInfinity(mayor))
                    alturas.Add(mayor);
            }
        }

        if (alturas.Count == 0)
            return limites.max.y + altura;

        alturas.Sort();
        Dictionary<int, int> repeticiones = new Dictionary<int, int>();
        int mejorCuenta = 0;
        float cara = alturas[alturas.Count - 1];
        for (int i = alturas.Count / 2; i < alturas.Count; i++)
        {
            int franja = Mathf.RoundToInt(alturas[i] / 0.005f);
            repeticiones.TryGetValue(franja, out int cuenta);
            repeticiones[franja] = ++cuenta;
            if (cuenta > mejorCuenta)
            {
                mejorCuenta = cuenta;
                cara = alturas[i];
            }
        }
        return cara + altura;
    }
}
