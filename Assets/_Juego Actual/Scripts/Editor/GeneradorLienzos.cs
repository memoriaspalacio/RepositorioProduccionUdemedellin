using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class GeneradorLienzos
{
    const string ShaderLienzo = "UdeM/Ambientacion/Lienzo";
    const string NombreRaiz = "== Cuadros ==";

    const string RutaEscenaCrisantia = "Assets/_Juego Actual/Scenes/_ProperGameScenes/crisantia.unity";
    const string CarpetaImagenesCrisantia = "Assets/_Juego Actual/Arte/Textures/Cuadros";
    const string RutaMaterialCrisantia = AplicadorAmbientacion.CarpetaRaiz + "/Crisantia/M_Lienzo_Crisantia.mat";
    static readonly string[] PrefijosCrisantia = { "cuadro", "cuadors" };
    static readonly string[] PreferidasRedondas = { "gloria_03", "dolor_01", "gloria_05", "dolor_02", "gloria_02", "dolor_05" };

    struct Lienzo
    {
        public Vector3 centro;
        public Vector3 normal;
        public Vector3 derecha;
        public float ancho;
        public float alto;
        public bool redondo;
        public float area;
    }

    struct Componente
    {
        public Vector2 min;
        public Vector2 max;
        public float profundidad;
        public float area;
    }

    [MenuItem("Herramientas/Ambientacion/Crisantia/Rellenar cuadros con imagenes", priority = 16)]
    static void RellenarCrisantia()
    {
        Scene escena = SceneManager.GetActiveScene();
        if (escena.path != RutaEscenaCrisantia)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            escena = EditorSceneManager.OpenScene(RutaEscenaCrisantia, OpenSceneMode.Single);
        }

        Generar(PrefijosCrisantia, CarpetaImagenesCrisantia, MaterialLienzo(RutaMaterialCrisantia));
        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
    }

    public static void Generar(string[] prefijos, string carpetaImagenes, Material material)
    {
        List<Texture2D> imagenes = Imagenes(carpetaImagenes);
        if (imagenes.Count == 0)
        {
            Debug.LogError("No hay imagenes en " + carpetaImagenes);
            return;
        }

        Vector3 observador = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
        List<Lienzo> lienzos = new List<Lienzo>();

        Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < renderers.Length; i++)
        {
            MeshRenderer render = renderers[i] as MeshRenderer;
            if (render == null || !CoincidePrefijo(render.name, prefijos))
                continue;

            MeshFilter filtro = render.GetComponent<MeshFilter>();
            if (filtro == null || filtro.sharedMesh == null)
                continue;

            lienzos.AddRange(Detectar(filtro, observador));
        }

        if (lienzos.Count == 0)
        {
            Debug.LogError("No se detectaron lienzos en los objetos " + string.Join(", ", prefijos));
            return;
        }

        Transform raiz = Raiz();
        for (int i = raiz.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(raiz.GetChild(i).gameObject);

        lienzos.Sort((a, b) => b.area.CompareTo(a.area));
        Texture2D[] asignadas = Asignar(lienzos, imagenes);
        Mesh quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");

        for (int i = 0; i < lienzos.Count; i++)
        {
            Lienzo lienzo = lienzos[i];
            GameObject objeto = new GameObject("Lienzo " + (i + 1).ToString("00") + " - " + asignadas[i].name);
            objeto.transform.SetParent(raiz, false);
            objeto.transform.SetPositionAndRotation(
                lienzo.centro + lienzo.normal * 0.004f,
                Quaternion.LookRotation(-lienzo.normal, Vector3.Cross(-lienzo.normal, lienzo.derecha)));
            objeto.transform.localScale = new Vector3(lienzo.ancho, lienzo.alto, 1f);

            objeto.AddComponent<MeshFilter>().sharedMesh = quad;
            MeshRenderer render = objeto.AddComponent<MeshRenderer>();
            render.sharedMaterial = material;
            render.shadowCastingMode = ShadowCastingMode.Off;
            render.lightProbeUsage = LightProbeUsage.BlendProbes;
            render.reflectionProbeUsage = ReflectionProbeUsage.Off;

            LienzoCuadro componente = objeto.AddComponent<LienzoCuadro>();
            componente.imagen = asignadas[i];
            componente.redondo = lienzo.redondo;
            componente.Aplicar();
        }

        Debug.Log("Se rellenaron " + lienzos.Count + " lienzos con " + imagenes.Count + " imagenes.");
    }

    static List<Lienzo> Detectar(MeshFilter filtro, Vector3 observador)
    {
        Mesh malla = filtro.sharedMesh;
        Vector3[] vertices;
        int[] triangulos;
        try
        {
            vertices = malla.vertices;
            triangulos = malla.triangles;
        }
        catch (System.Exception)
        {
            Debug.LogError("No se pudo leer la malla de " + filtro.name + ". Activa Read/Write en su modelo.");
            return new List<Lienzo>();
        }

        Transform t = filtro.transform;
        Vector3[] mundo = new Vector3[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
            mundo[i] = t.TransformPoint(vertices[i]);

        int cantidad = triangulos.Length / 3;
        Vector3[] normales = new Vector3[cantidad];
        float[] areas = new float[cantidad];
        Dictionary<Vector3Int, float> histograma = new Dictionary<Vector3Int, float>();

        for (int i = 0; i < cantidad; i++)
        {
            Vector3 a = mundo[triangulos[i * 3]];
            Vector3 b = mundo[triangulos[i * 3 + 1]];
            Vector3 c = mundo[triangulos[i * 3 + 2]];
            Vector3 cruz = Vector3.Cross(b - a, c - a);
            areas[i] = cruz.magnitude * 0.5f;
            normales[i] = cruz.normalized;

            Vector3Int clave = Vector3Int.RoundToInt(normales[i] * 10f);
            histograma.TryGetValue(clave, out float acumulado);
            histograma[clave] = acumulado + areas[i];
        }

        Vector3 haciaObservador = observador - filtro.GetComponent<Renderer>().bounds.center;
        Vector3 frente = Vector3.zero;
        float mejor = 0f;
        foreach (KeyValuePair<Vector3Int, float> par in histograma)
        {
            Vector3 n = ((Vector3)par.Key).normalized;
            if (Vector3.Dot(n, haciaObservador) > 0f && par.Value > mejor)
            {
                mejor = par.Value;
                frente = n;
            }
        }

        if (frente == Vector3.zero)
            return new List<Lienzo>();

        Vector3 arriba = Vector3.ProjectOnPlane(Vector3.up, frente).normalized;
        if (arriba.sqrMagnitude < 0.01f)
            arriba = Vector3.ProjectOnPlane(Vector3.forward, frente).normalized;
        Vector3 derecha = Vector3.Cross(arriba, -frente).normalized;

        List<int> delante = new List<int>();
        for (int i = 0; i < cantidad; i++)
        {
            if (Vector3.Dot(normales[i], frente) > 0.95f)
                delante.Add(i);
        }

        int[] padre = new int[cantidad];
        for (int i = 0; i < cantidad; i++)
            padre[i] = i;

        Dictionary<Vector3Int, int> porPosicion = new Dictionary<Vector3Int, int>();
        for (int k = 0; k < delante.Count; k++)
        {
            int tri = delante[k];
            for (int v = 0; v < 3; v++)
            {
                Vector3Int clave = Vector3Int.RoundToInt(mundo[triangulos[tri * 3 + v]] * 2000f);
                if (porPosicion.TryGetValue(clave, out int otro))
                    Unir(padre, tri, otro);
                else
                    porPosicion[clave] = tri;
            }
        }

        Dictionary<int, Componente> componentes = new Dictionary<int, Componente>();
        Dictionary<int, int> conteo = new Dictionary<int, int>();
        for (int k = 0; k < delante.Count; k++)
        {
            int tri = delante[k];
            int raiz = Buscar(padre, tri);
            if (!componentes.TryGetValue(raiz, out Componente comp))
            {
                comp.min = new Vector2(float.MaxValue, float.MaxValue);
                comp.max = new Vector2(float.MinValue, float.MinValue);
            }

            for (int v = 0; v < 3; v++)
            {
                Vector3 p = mundo[triangulos[tri * 3 + v]];
                Vector2 plano = new Vector2(Vector3.Dot(p, derecha), Vector3.Dot(p, arriba));
                comp.min = Vector2.Min(comp.min, plano);
                comp.max = Vector2.Max(comp.max, plano);
                comp.profundidad += Vector3.Dot(p, frente);
            }

            comp.area += areas[tri];
            componentes[raiz] = comp;
            conteo.TryGetValue(raiz, out int n);
            conteo[raiz] = n + 3;
        }

        List<Componente> lista = new List<Componente>();
        foreach (KeyValuePair<int, Componente> par in componentes)
        {
            Componente comp = par.Value;
            comp.profundidad /= conteo[par.Key];
            lista.Add(comp);
        }

        List<Lienzo> resultado = new List<Lienzo>();
        for (int i = 0; i < lista.Count; i++)
        {
            Componente candidato = lista[i];
            bool tieneMarco = false;
            bool contieneOtro = false;

            for (int j = 0; j < lista.Count; j++)
            {
                if (i == j)
                    continue;

                if (Contiene(lista[j], candidato) && lista[j].profundidad > candidato.profundidad + 0.001f)
                    tieneMarco = true;
                if (Contiene(candidato, lista[j]))
                    contieneOtro = true;
            }

            if (!tieneMarco || contieneOtro)
                continue;

            Vector2 tamano = candidato.max - candidato.min;
            Vector2 centro = (candidato.max + candidato.min) * 0.5f;
            float relleno = candidato.area / Mathf.Max(0.0001f, tamano.x * tamano.y);

            resultado.Add(new Lienzo
            {
                centro = derecha * centro.x + arriba * centro.y + frente * candidato.profundidad,
                normal = frente,
                derecha = derecha,
                ancho = tamano.x,
                alto = tamano.y,
                redondo = relleno < 0.9f,
                area = candidato.area
            });
        }

        return resultado;
    }

    static Texture2D[] Asignar(List<Lienzo> lienzos, List<Texture2D> imagenes)
    {
        Texture2D[] asignadas = new Texture2D[lienzos.Count];
        Dictionary<Texture2D, int> usos = new Dictionary<Texture2D, int>();
        for (int i = 0; i < imagenes.Count; i++)
            usos[imagenes[i]] = 0;

        string estadoAnterior = "";
        for (int i = 0; i < lienzos.Count; i++)
        {
            Lienzo lienzo = lienzos[i];
            float aspecto = lienzo.ancho / Mathf.Max(0.0001f, lienzo.alto);
            Texture2D mejor = null;
            float mejorPuntaje = float.MaxValue;

            for (int k = 0; k < imagenes.Count; k++)
            {
                Texture2D imagen = imagenes[k];
                float aspectoImagen = imagen.width / (float)Mathf.Max(1, imagen.height);
                float puntaje = Mathf.Abs(Mathf.Log(aspectoImagen / aspecto)) + usos[imagen] * 0.9f;

                if (Estado(imagen) == estadoAnterior)
                    puntaje += 0.35f;

                if (lienzo.redondo)
                {
                    int preferencia = System.Array.IndexOf(PreferidasRedondas, imagen.name);
                    puntaje = preferencia >= 0 ? preferencia * 0.2f + usos[imagen] * 2f : 10f;
                }

                if (puntaje < mejorPuntaje)
                {
                    mejorPuntaje = puntaje;
                    mejor = imagen;
                }
            }

            asignadas[i] = mejor;
            usos[mejor]++;
            estadoAnterior = Estado(mejor);
        }

        return asignadas;
    }

    static string Estado(Texture2D imagen)
    {
        int guion = imagen.name.IndexOf('_');
        return guion > 0 ? imagen.name.Substring(0, guion) : imagen.name;
    }

    static List<Texture2D> Imagenes(string carpeta)
    {
        List<Texture2D> imagenes = new List<Texture2D>();
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { carpeta });

        for (int i = 0; i < guids.Length; i++)
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guids[i]);
            TextureImporter importador = AssetImporter.GetAtPath(ruta) as TextureImporter;
            if (importador != null && (importador.maxTextureSize != 1024 || importador.wrapMode != TextureWrapMode.Clamp || importador.anisoLevel != 4))
            {
                importador.textureType = TextureImporterType.Default;
                importador.sRGBTexture = true;
                importador.mipmapEnabled = true;
                importador.wrapMode = TextureWrapMode.Clamp;
                importador.anisoLevel = 4;
                importador.maxTextureSize = 1024;
                importador.textureCompression = TextureImporterCompression.CompressedHQ;
                importador.SaveAndReimport();
            }

            Texture2D imagen = AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
            if (imagen != null)
                imagenes.Add(imagen);
        }

        imagenes.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return imagenes;
    }

    static Material MaterialLienzo(string ruta)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (material != null)
            return material;

        AplicadorAmbientacion.CrearCarpeta(Path.GetDirectoryName(ruta).Replace('\\', '/'));
        material = new Material(Shader.Find(ShaderLienzo));
        material.enableInstancing = false;
        AssetDatabase.CreateAsset(material, ruta);
        return material;
    }

    static Transform Raiz()
    {
        GameObject[] raices = SceneManager.GetActiveScene().GetRootGameObjects();
        for (int i = 0; i < raices.Length; i++)
        {
            if (raices[i].name == NombreRaiz)
                return raices[i].transform;
        }

        return new GameObject(NombreRaiz).transform;
    }

    static bool Contiene(Componente exterior, Componente interior)
    {
        const float tolerancia = 0.001f;
        return exterior.min.x <= interior.min.x + tolerancia
            && exterior.min.y <= interior.min.y + tolerancia
            && exterior.max.x >= interior.max.x - tolerancia
            && exterior.max.y >= interior.max.y - tolerancia;
    }

    static bool CoincidePrefijo(string nombre, string[] prefijos)
    {
        for (int i = 0; i < prefijos.Length; i++)
        {
            if (nombre.StartsWith(prefijos[i], System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    static int Buscar(int[] padre, int i)
    {
        while (padre[i] != i)
        {
            padre[i] = padre[padre[i]];
            i = padre[i];
        }

        return i;
    }

    static void Unir(int[] padre, int a, int b)
    {
        padre[Buscar(padre, a)] = Buscar(padre, b);
    }
}
