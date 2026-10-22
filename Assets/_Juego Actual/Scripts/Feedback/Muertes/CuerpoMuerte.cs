using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Copia congelada del cuerpo de un personaje en el instante en que muere. Hornea la pose de
/// las mallas animadas, las dibuja con el shader UdeM/Efectos/CuerpoMuerte (que se ilumina
/// igual que URP Lit) y reparte puntos sobre la superficie con el color de la textura en cada
/// punto, para que las particulas nazcan del lugar exacto que desaparece y con su mismo color.
/// </summary>
public class CuerpoMuerte
{
    public struct Muestra
    {
        public Vector3 posicion;
        public Vector3 normal;
        public Color color;
        // Valor del campo de disolucion: el punto desaparece cuando el progreso lo supera.
        public float campo;
    }

    struct Triangulo
    {
        public int malla;
        public int a, b, c;
        public Material material;
    }

    static readonly int IdBaseMap = Shader.PropertyToID("_BaseMap");
    static readonly int IdBaseColor = Shader.PropertyToID("_BaseColor");
    static readonly int IdMetallic = Shader.PropertyToID("_Metallic");
    static readonly int IdSmoothness = Shader.PropertyToID("_Smoothness");
    static readonly int IdGlossiness = Shader.PropertyToID("_Glossiness");
    static readonly int IdCull = Shader.PropertyToID("_Cull");

    public static readonly int IdProgreso = Shader.PropertyToID("_Progreso");
    public static readonly int IdAnchoBorde = Shader.PropertyToID("_AnchoBorde");
    public static readonly int IdColorBorde = Shader.PropertyToID("_ColorBorde");
    public static readonly int IdEscalaRuido = Shader.PropertyToID("_EscalaRuido");
    public static readonly int IdPesoAltura = Shader.PropertyToID("_PesoAltura");
    public static readonly int IdSentidoAltura = Shader.PropertyToID("_SentidoAltura");
    public static readonly int IdAlturaMin = Shader.PropertyToID("_AlturaMin");
    public static readonly int IdAlturaMax = Shader.PropertyToID("_AlturaMax");
    public static readonly int IdOrigenRuido = Shader.PropertyToID("_OrigenRuido");
    public static readonly int IdQuemado = Shader.PropertyToID("_Quemado");
    public static readonly int IdAnchoQuemado = Shader.PropertyToID("_AnchoQuemado");
    public static readonly int IdColorQuemado = Shader.PropertyToID("_ColorQuemado");
    public static readonly int IdBrasas = Shader.PropertyToID("_Brasas");
    public static readonly int IdColorBrasas = Shader.PropertyToID("_ColorBrasas");
    public static readonly int IdEscalaGrietas = Shader.PropertyToID("_EscalaGrietas");
    public static readonly int IdAnchoGrieta = Shader.PropertyToID("_AnchoGrieta");
    public static readonly int IdColorGrietas = Shader.PropertyToID("_ColorGrietas");
    public static readonly int IdBrilloGrietas = Shader.PropertyToID("_BrilloGrietas");
    public static readonly int IdPuntoImpacto = Shader.PropertyToID("_PuntoImpacto");
    public static readonly int IdDestello = Shader.PropertyToID("_Destello");
    public static readonly int IdColorDestello = Shader.PropertyToID("_ColorDestello");

    /// <summary>Contenedor de las copias. Moverlo mueve el cuerpo (temblor del angel).</summary>
    public Transform Raiz { get; }
    public Bounds Limites { get; private set; }
    /// <summary>Puntos de la superficie ordenados de menor a mayor campo de disolucion.</summary>
    public Muestra[] Muestras { get; private set; } = new Muestra[0];
    public Color ColorPromedio { get; private set; } = Color.gray;
    public float CampoMinimo { get; private set; }
    public float CampoMaximo { get; private set; } = 1f;
    /// <summary>Material original que cubre mas superficie (las rocas del angel lo usan).</summary>
    public Material MaterialPrincipal { get; private set; }
    public uint CapasDeLuz { get; private set; } = 1u;
    public int Capa { get; private set; }

    readonly List<Renderer> originales = new List<Renderer>();
    readonly List<MeshRenderer> copias = new List<MeshRenderer>();
    readonly List<Mesh> mallasPropias = new List<Mesh>();
    readonly List<Material> materiales = new List<Material>();
    readonly Dictionary<Material, Material> instancias = new Dictionary<Material, Material>();
    readonly List<Vector3[]> verticesMundo = new List<Vector3[]>();
    readonly List<Vector3[]> normalesMundo = new List<Vector3[]>();
    readonly List<Vector2[]> uvsMalla = new List<Vector2[]>();
    readonly List<Triangulo> triangulos = new List<Triangulo>();
    readonly List<float> areaAcumulada = new List<float>();

    // Parametros del campo (replica de CampoDisolucion en el shader).
    float pesoAltura;
    float sentidoAltura = 1f;
    float escalaRuido = 5f;
    Vector3 origenRuido;

    CuerpoMuerte(Transform raiz)
    {
        Raiz = raiz;
    }

    /// <summary>
    /// Captura el cuerpo visible del enemigo. Devuelve null si no encuentra mallas que copiar.
    /// </summary>
    public static CuerpoMuerte Capturar(GameObject enemigo, Transform raizVisual, Transform contenedor, int cantidadMuestras)
    {
        List<Renderer> fuentes = BuscarRenderers(enemigo, raizVisual);
        if (fuentes.Count == 0)
            return null;

        CuerpoMuerte cuerpo = new CuerpoMuerte(contenedor);
        for (int i = 0; i < fuentes.Count; i++)
            cuerpo.Copiar(fuentes[i]);

        if (cuerpo.copias.Count == 0)
        {
            cuerpo.Liberar();
            return null;
        }

        cuerpo.Muestrear(cantidadMuestras);
        return cuerpo;
    }

    #region Busqueda de las mallas del cuerpo

    // Se queda con las mallas animadas y con lo que cuelga de su modelo (armas, accesorios).
    // Asi no se copian el cubo de prueba, las marcas de ataque de la monja ni los indicadores,
    // ni otros enemigos que viajan como hijos (las cruces del Monk).
    static List<Renderer> BuscarRenderers(GameObject enemigo, Transform raizVisual)
    {
        List<Renderer> lista = new List<Renderer>();
        Transform raiz = raizVisual != null ? raizVisual : enemigo.transform;
        HashSet<Transform> modelos = new HashSet<Transform>();

        SkinnedMeshRenderer[] pieles = raiz.GetComponentsInChildren<SkinnedMeshRenderer>(false);
        for (int i = 0; i < pieles.Length; i++)
        {
            SkinnedMeshRenderer piel = pieles[i];
            if (!EsCuerpo(piel) || piel.sharedMesh == null || DeOtroEnemigo(piel.transform, enemigo))
                continue;

            lista.Add(piel);
            modelos.Add(piel.transform.parent != null ? piel.transform.parent : piel.transform);
        }

        MeshRenderer[] rigidos = raiz.GetComponentsInChildren<MeshRenderer>(false);
        for (int i = 0; i < rigidos.Length; i++)
        {
            MeshRenderer rigido = rigidos[i];
            if (!EsCuerpo(rigido) || DeOtroEnemigo(rigido.transform, enemigo))
                continue;

            MeshFilter filtro = rigido.GetComponent<MeshFilter>();
            if (filtro == null || filtro.sharedMesh == null)
                continue;

            if (raizVisual == null && modelos.Count > 0 && !DentroDe(rigido.transform, modelos))
                continue;

            lista.Add(rigido);
        }

        return lista;
    }

    static bool EsCuerpo(Renderer render)
    {
        if (render == null || !render.enabled || render.forceRenderingOff || !render.gameObject.activeInHierarchy)
            return false;

        Material material = render.sharedMaterial;
        return material != null && material.renderQueue < 2500;
    }

    static bool DeOtroEnemigo(Transform objeto, GameObject enemigo)
    {
        EnemyHealth vida = objeto.GetComponentInParent<EnemyHealth>(true);
        return vida != null && vida.gameObject != enemigo;
    }

    static bool DentroDe(Transform objeto, HashSet<Transform> raices)
    {
        for (Transform t = objeto; t != null; t = t.parent)
            if (raices.Contains(t))
                return true;
        return false;
    }

    #endregion

    #region Copia de las mallas

    void Copiar(Renderer fuente)
    {
        Mesh malla;
        GameObject copia = new GameObject(fuente.name);
        copia.layer = fuente.gameObject.layer;
        copia.transform.SetParent(Raiz, false);

        if (fuente is SkinnedMeshRenderer piel)
        {
            // Sin compensar escala: los vertices quedan ya escalados, se coloca sin escala.
            malla = new Mesh { name = fuente.name + " (muerte)" };
            piel.BakeMesh(malla, false);
            mallasPropias.Add(malla);
            copia.transform.SetPositionAndRotation(piel.transform.position, piel.transform.rotation);
        }
        else
        {
            malla = fuente.GetComponent<MeshFilter>().sharedMesh;
            copia.transform.SetPositionAndRotation(fuente.transform.position, fuente.transform.rotation);
            copia.transform.localScale = fuente.transform.lossyScale;
        }

        copia.AddComponent<MeshFilter>().sharedMesh = malla;
        MeshRenderer render = copia.AddComponent<MeshRenderer>();

        Material[] originalesMateriales = fuente.sharedMaterials;
        Material[] propios = new Material[originalesMateriales.Length];
        for (int i = 0; i < originalesMateriales.Length; i++)
            propios[i] = MaterialPara(originalesMateriales[i]);

        render.sharedMaterials = propios;
        render.shadowCastingMode = fuente.shadowCastingMode;
        render.receiveShadows = fuente.receiveShadows;
        render.renderingLayerMask = fuente.renderingLayerMask;
        render.lightProbeUsage = fuente.lightProbeUsage;
        render.reflectionProbeUsage = fuente.reflectionProbeUsage;

        CapasDeLuz = fuente.renderingLayerMask;
        Capa = fuente.gameObject.layer;

        originales.Add(fuente);
        copias.Add(render);
        RegistrarTriangulos(malla, copia.transform, originalesMateriales);
    }

    Material MaterialPara(Material original)
    {
        if (original != null && instancias.TryGetValue(original, out Material existente))
            return existente;

        Material material = RecursosMuerte.NuevoMaterialCuerpo();
        if (material == null)
            return original;

        material.name = (original != null ? original.name : "Cuerpo") + " (muerte)";
        if (original != null)
        {
            Texture textura = RecursosMuerte.TexturaDe(original);
            Vector4 escalaDesplazamiento = RecursosMuerte.EscalaDesplazamientoDe(original);
            material.SetTexture(IdBaseMap, textura);
            material.SetTextureScale(IdBaseMap, new Vector2(escalaDesplazamiento.x, escalaDesplazamiento.y));
            material.SetTextureOffset(IdBaseMap, new Vector2(escalaDesplazamiento.z, escalaDesplazamiento.w));
            material.SetColor(IdBaseColor, RecursosMuerte.TinteDe(original));

            if (original.HasProperty(IdMetallic))
                material.SetFloat(IdMetallic, original.GetFloat(IdMetallic));
            if (original.HasProperty(IdSmoothness))
                material.SetFloat(IdSmoothness, original.GetFloat(IdSmoothness));
            else if (original.HasProperty(IdGlossiness))
                material.SetFloat(IdSmoothness, original.GetFloat(IdGlossiness));
            if (original.HasProperty(IdCull))
                material.SetFloat(IdCull, original.GetFloat(IdCull));

            instancias[original] = material;
        }

        material.SetFloat(IdProgreso, -1f);
        materiales.Add(material);
        return material;
    }

    void RegistrarTriangulos(Mesh malla, Transform transformacion, Material[] materialesOriginales)
    {
        if (!malla.isReadable)
        {
            Debug.LogWarning($"EfectoMuerte: la malla '{malla.name}' no tiene Read/Write activo; no saldran particulas de ella.", malla);
            return;
        }

        Vector3[] vertices = malla.vertices;
        Vector3[] normales = malla.normals;
        Vector2[] uvs = malla.uv;
        Matrix4x4 aMundo = transformacion.localToWorldMatrix;

        Vector3[] posiciones = new Vector3[vertices.Length];
        Vector3[] direcciones = new Vector3[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            posiciones[i] = aMundo.MultiplyPoint3x4(vertices[i]);
            direcciones[i] = normales.Length == vertices.Length
                ? aMundo.MultiplyVector(normales[i]).normalized
                : Vector3.up;
        }

        int indiceMalla = verticesMundo.Count;
        verticesMundo.Add(posiciones);
        normalesMundo.Add(direcciones);
        uvsMalla.Add(uvs.Length == vertices.Length ? uvs : null);

        float acumulado = areaAcumulada.Count > 0 ? areaAcumulada[areaAcumulada.Count - 1] : 0f;
        for (int sub = 0; sub < malla.subMeshCount; sub++)
        {
            Material material = materialesOriginales.Length > 0
                ? materialesOriginales[Mathf.Min(sub, materialesOriginales.Length - 1)]
                : null;
            int[] indices = malla.GetTriangles(sub);

            for (int i = 0; i + 2 < indices.Length; i += 3)
            {
                Vector3 a = posiciones[indices[i]];
                Vector3 b = posiciones[indices[i + 1]];
                Vector3 c = posiciones[indices[i + 2]];
                float area = Vector3.Cross(b - a, c - a).magnitude * 0.5f;
                if (area <= 0f)
                    continue;

                acumulado += area;
                triangulos.Add(new Triangulo { malla = indiceMalla, a = indices[i], b = indices[i + 1], c = indices[i + 2], material = material });
                areaAcumulada.Add(acumulado);
            }
        }
    }

    #endregion

    #region Muestreo de la superficie

    void Muestrear(int cantidad)
    {
        Bounds limites = new Bounds(copias[0].transform.position, Vector3.zero);
        bool hayLimites = false;
        for (int m = 0; m < verticesMundo.Count; m++)
        {
            Vector3[] posiciones = verticesMundo[m];
            for (int i = 0; i < posiciones.Length; i++)
            {
                if (!hayLimites)
                {
                    limites = new Bounds(posiciones[i], Vector3.zero);
                    hayLimites = true;
                }
                limites.Encapsulate(posiciones[i]);
            }
        }

        if (!hayLimites)
        {
            for (int i = 0; i < copias.Count; i++)
            {
                if (i == 0)
                    limites = copias[i].bounds;
                else
                    limites.Encapsulate(copias[i].bounds);
            }
        }

        Limites = limites;

        if (triangulos.Count == 0 || cantidad <= 0)
        {
            Muestras = new Muestra[0];
            return;
        }

        float areaTotal = areaAcumulada[areaAcumulada.Count - 1];
        Muestra[] muestras = new Muestra[cantidad];
        Dictionary<Material, float> areaPorMaterial = new Dictionary<Material, float>();
        Color suma = Color.clear;

        for (int k = 0; k < cantidad; k++)
        {
            int indice = BuscarTriangulo(Random.value * areaTotal);
            Triangulo t = triangulos[indice];

            float u = Random.value;
            float v = Random.value;
            if (u + v > 1f)
            {
                u = 1f - u;
                v = 1f - v;
            }
            float w = 1f - u - v;

            Vector3[] posiciones = verticesMundo[t.malla];
            Vector3[] normales = normalesMundo[t.malla];
            Vector2[] uvs = uvsMalla[t.malla];

            Color color = RecursosMuerte.TinteDe(t.material);
            Texture2D colores = RecursosMuerte.CopiaLegible(RecursosMuerte.TexturaDe(t.material));
            if (colores != null && uvs != null)
            {
                Vector4 st = RecursosMuerte.EscalaDesplazamientoDe(t.material);
                Vector2 uv = uvs[t.a] * w + uvs[t.b] * u + uvs[t.c] * v;
                color *= colores.GetPixelBilinear(uv.x * st.x + st.z, uv.y * st.y + st.w);
            }
            color.a = 1f;

            muestras[k] = new Muestra
            {
                posicion = posiciones[t.a] * w + posiciones[t.b] * u + posiciones[t.c] * v,
                normal = (normales[t.a] * w + normales[t.b] * u + normales[t.c] * v).normalized,
                color = color
            };

            suma += color;
            if (t.material != null)
            {
                areaPorMaterial.TryGetValue(t.material, out float area);
                areaPorMaterial[t.material] = area + 1f;
            }
        }

        Muestras = muestras;
        ColorPromedio = suma / cantidad;

        float mayor = -1f;
        foreach (KeyValuePair<Material, float> par in areaPorMaterial)
        {
            if (par.Value > mayor)
            {
                mayor = par.Value;
                MaterialPrincipal = par.Key;
            }
        }
    }

    int BuscarTriangulo(float valor)
    {
        int bajo = 0;
        int alto = areaAcumulada.Count - 1;
        while (bajo < alto)
        {
            int medio = (bajo + alto) / 2;
            if (areaAcumulada[medio] < valor)
                bajo = medio + 1;
            else
                alto = medio;
        }
        return bajo;
    }

    #endregion

    #region Disolucion

    /// <summary>
    /// Configura el campo de disolucion en el shader y calcula el mismo campo en cada punto de
    /// la superficie. Las muestras quedan ordenadas por el momento en que desaparecen.
    /// </summary>
    public void ConfigurarDisolucion(float peso, bool deArribaAbajo, float escala, float anchoBorde, Color colorBorde)
    {
        pesoAltura = peso;
        sentidoAltura = deArribaAbajo ? 1f : 0f;
        escalaRuido = escala;

        Fijar(IdPesoAltura, pesoAltura);
        Fijar(IdSentidoAltura, sentidoAltura);
        Fijar(IdEscalaRuido, escalaRuido);
        Fijar(IdAlturaMin, Limites.min.y);
        Fijar(IdAlturaMax, Limites.max.y);
        Fijar(IdAnchoBorde, anchoBorde);
        Fijar(IdColorBorde, colorBorde);

        Muestra[] muestras = Muestras;
        float minimo = float.MaxValue;
        float maximo = float.MinValue;
        for (int i = 0; i < muestras.Length; i++)
        {
            muestras[i].campo = Campo(muestras[i].posicion);
            minimo = Mathf.Min(minimo, muestras[i].campo);
            maximo = Mathf.Max(maximo, muestras[i].campo);
        }

        System.Array.Sort(muestras, (x, y) => x.campo.CompareTo(y.campo));

        CampoMinimo = muestras.Length > 0 ? minimo : 0f;
        CampoMaximo = muestras.Length > 0 ? maximo : 1f;
    }

    public float Campo(Vector3 posicionMundo)
    {
        Vector3 p = posicionMundo - origenRuido;
        float altura = Mathf.Clamp01((p.y - Limites.min.y) / Mathf.Max(0.001f, Limites.size.y));
        float campoAltura = Mathf.LerpUnclamped(altura, 1f - altura, sentidoAltura);
        return Mathf.LerpUnclamped(RuidoDisolucion(p * escalaRuido), campoAltura, pesoAltura);
    }

    /// <summary>Mueve las copias sin que el ruido de la disolucion o las grietas resbale.</summary>
    public void Desplazar(Vector3 desplazamiento)
    {
        Raiz.localPosition = desplazamiento;
        Fijar(IdOrigenRuido, (Vector4)desplazamiento);
    }

    static float Frac(float x) => x - Mathf.Floor(x);

    static float Hash13(Vector3 p)
    {
        p = new Vector3(Frac(p.x * 0.1031f), Frac(p.y * 0.1031f), Frac(p.z * 0.1031f));
        float d = p.x * (p.z + 31.32f) + p.y * (p.y + 31.32f) + p.z * (p.x + 31.32f);
        p.x += d;
        p.y += d;
        p.z += d;
        return Frac((p.x + p.y) * p.z);
    }

    static float RuidoValor(Vector3 p)
    {
        Vector3 i = new Vector3(Mathf.Floor(p.x), Mathf.Floor(p.y), Mathf.Floor(p.z));
        Vector3 f = p - i;
        Vector3 u = new Vector3(f.x * f.x * (3f - 2f * f.x), f.y * f.y * (3f - 2f * f.y), f.z * f.z * (3f - 2f * f.z));

        float x00 = Mathf.LerpUnclamped(Hash13(i), Hash13(i + new Vector3(1, 0, 0)), u.x);
        float x10 = Mathf.LerpUnclamped(Hash13(i + new Vector3(0, 1, 0)), Hash13(i + new Vector3(1, 1, 0)), u.x);
        float x01 = Mathf.LerpUnclamped(Hash13(i + new Vector3(0, 0, 1)), Hash13(i + new Vector3(1, 0, 1)), u.x);
        float x11 = Mathf.LerpUnclamped(Hash13(i + new Vector3(0, 1, 1)), Hash13(i + new Vector3(1, 1, 1)), u.x);
        return Mathf.LerpUnclamped(Mathf.LerpUnclamped(x00, x10, u.y), Mathf.LerpUnclamped(x01, x11, u.y), u.z);
    }

    static float RuidoDisolucion(Vector3 p)
    {
        float n = RuidoValor(p) * 0.65f + RuidoValor(p * 2.17f + new Vector3(19.1f, 19.1f, 19.1f)) * 0.35f;
        return Mathf.Clamp01((n - 0.5f) * 2.2f + 0.5f);
    }

    #endregion

    #region Control de las copias

    public void Fijar(int id, float valor)
    {
        for (int i = 0; i < materiales.Count; i++)
            materiales[i].SetFloat(id, valor);
    }

    public void Fijar(int id, Color valor)
    {
        for (int i = 0; i < materiales.Count; i++)
            materiales[i].SetColor(id, valor);
    }

    public void Fijar(int id, Vector4 valor)
    {
        for (int i = 0; i < materiales.Count; i++)
            materiales[i].SetVector(id, valor);
    }

    public void Mostrar(bool visible)
    {
        for (int i = 0; i < copias.Count; i++)
            if (copias[i] != null)
                copias[i].enabled = visible;
    }

    /// <summary>Apaga el modelo original; la copia ocupa su lugar en este mismo frame.</summary>
    public void OcultarOriginal()
    {
        for (int i = 0; i < originales.Count; i++)
            if (originales[i] != null)
                originales[i].forceRenderingOff = true;
    }

    public void Liberar()
    {
        for (int i = 0; i < materiales.Count; i++)
            if (materiales[i] != null)
                Object.Destroy(materiales[i]);

        for (int i = 0; i < mallasPropias.Count; i++)
            if (mallasPropias[i] != null)
                Object.Destroy(mallasPropias[i]);

        materiales.Clear();
        mallasPropias.Clear();
        instancias.Clear();
    }

    #endregion
}
