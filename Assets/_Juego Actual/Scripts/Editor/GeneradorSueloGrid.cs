using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Cambia el suelo modelado de cada escena por un plano con SueloGrid, cuyas juntas caen sobre las celdas del MeshGrid.
public static class GeneradorSueloGrid
{
    const string ShaderSuelo = "UdeM/Suelo/SueloGrid";
    const string ShaderCaptura = "Hidden/UdeM/CapturaLuzSuelo";
    const string NombreSuelo = "Suelo Grid";
    const string CarpetaEscenas = "Assets/_Juego Actual/Scenes/_ProperGameScenes/";
    const string CarpetaTexturas = "Assets/_Juego Actual/Arte/Textures/";
    const string CarpetaCementerio = AplicadorAmbientacion.CarpetaRaiz + "/Cementerio/";
    const int TexelesPorUnidad = 8;

    class Preset
    {
        public string escena;
        public string carpeta;
        public string textura;  // ruta completa
        public Action<Material> ajustar;
    }

    // Cada preset imita el suelo que estaba modelado en esa escena.
    static readonly Preset[] Presets =
    {
        new Preset
        {
            escena = "mazmorra", carpeta = "Mazmorra", textura = CarpetaTexturas + "piedra 1.png",
            ajustar = m =>
            {
                Valores(m, ("_Patron", 0), ("_Divisiones", 2), ("_Junta", 0.05f), ("_Bisel", 0.035f), ("_Relieve", 1f),
                    ("_RadioEsquina", 0.04f), ("_Astillas", 0.3f), ("_Grietas", 0.18f), ("_EscalaTextura", 2.5f),
                    ("_VariacionColor", 0.35f), ("_Oclusion", 0.65f), ("_Suavidad", 0.2f),
                    ("_MezclaGrid", 1), ("_EstiloGrid", 0), ("_IntensidadGrid", 1f));
                m.SetColor("_ColorJunta", new Color(0.09f, 0.08f, 0.075f));
                m.SetColor("_GridColor", new Color(1.6f, 0.5f, 0.12f));
            }
        },
        new Preset
        {
            escena = "crisantia", carpeta = "Crisantia", textura = CarpetaTexturas + "pisosacristia.jpg",
            ajustar = m =>
            {
                Valores(m, ("_Patron", 0), ("_Divisiones", 1), ("_Junta", 0.07f), ("_Bisel", 0.1f), ("_Relieve", 1.2f),
                    ("_RadioEsquina", 0.02f), ("_EscalaTextura", 7f), ("_VariacionColor", 0.15f), ("_Oclusion", 0.5f), ("_Suavidad", 0.3f),
                    ("_MezclaGrid", 2), ("_EstiloGrid", 3), ("_LineThickness", 0.025f), ("_LargoMarca", 0.18f), ("_MargenGrid", 0.1f));
                m.SetColor("_ColorJunta", new Color(0.32f, 0.28f, 0.24f));
                m.SetColor("_GridColor", new Color(1.5f, 1.17f, 0.52f));
            }
        },
        new Preset
        {
            escena = "iglesia", carpeta = "Iglesia", textura = CarpetaTexturas + "Piedra.png",
            ajustar = m =>
            {
                Valores(m, ("_Patron", 1), ("_FilasLadrillo", 4), ("_LadrillosFila", 2), ("_Junta", 0.04f), ("_Bisel", 0.06f),
                    ("_Relieve", 1f), ("_RadioEsquina", 0.02f), ("_EscalaTextura", 1.5f), ("_MezclaTextura", 0.6f),
                    ("_VariacionColor", 0.18f), ("_Oclusion", 0.5f), ("_Suavidad", 0.35f),
                    ("_MezclaGrid", 3), ("_EstiloGrid", 0), ("_LineThickness", 0.035f), ("_DesgasteGrid", 0.35f), ("_ReaccionBeat", 0.6f));
                m.SetColor("_BaseColor", new Color(0.82f, 0.8f, 0.78f));
                m.SetColor("_ColorJunta", new Color(0.35f, 0.34f, 0.33f));
                m.SetColor("_GridColor", new Color(0.95f, 0.92f, 0.85f));
            }
        },
        new Preset
        {
            // Parcelas de cesped separadas por senderos de tierra, con un mojon en cada cruce.
            escena = "cementerio", carpeta = "Cementerio", textura = CarpetaCementerio + "T_Cesped_Cementerio.png",
            ajustar = m =>
            {
                Valores(m, ("_Patron", 4), ("_EscalaTextura", 6f), ("_VariacionParcela", 0.65f), ("_EscalaSenda", 1.5f),
                    ("_AnchoSenda", 0.34f), ("_IrregularSenda", 0.6f), ("_Bisel", 0.08f), ("_Relieve", 1f), ("_Oclusion", 0.55f),
                    ("_Suavidad", 0.15f), ("_TamanoMojon", 0.32f), ("_MezclaGrid", 1), ("_IntensidadGrid", 0.2f));
                m.SetTexture("_TexturaSenda", AssetDatabase.LoadAssetAtPath<Texture2D>(CarpetaCementerio + "T_Tierra_Cementerio.png"));
                m.SetTexture("_TexturaPiedra", AssetDatabase.LoadAssetAtPath<Texture2D>(CarpetaTexturas + "Piedra.png"));
                m.SetColor("_ColorSeco", new Color(0.62f, 0.55f, 0.32f));
                m.SetColor("_ColorPiedra", new Color(0.55f, 0.56f, 0.6f));
                m.SetColor("_GridColor", new Color(0.3f, 0.85f, 0.65f));
            }
        },
    };

    [MenuItem("Herramientas/Suelo Grid/Crear o actualizar en la escena abierta", priority = 30)]
    static void CrearEnEscenaAbierta()
    {
        Scene escena = SceneManager.GetActiveScene();
        if (Instalar(escena) != null)
            EditorSceneManager.SaveScene(escena);
    }

    [MenuItem("Herramientas/Suelo Grid/Crear en las cuatro escenas de juego", priority = 31)]
    static void CrearEnTodasMenu()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            CrearEnTodas();
    }

    [MenuItem("Herramientas/Suelo Grid/Restablecer el material de la escena abierta", priority = 33)]
    static void RestablecerMenu()
    {
        Material material = RestablecerMaterial(SceneManager.GetActiveScene().name);
        if (material != null)
            Debug.Log("Material " + material.name + " vuelto a su preset.");
    }

    // Devuelve el material del suelo de la escena a los valores de su preset, sin cambiar su GUID.
    public static Material RestablecerMaterial(string escena)
    {
        Preset preset = Buscar(escena);
        Material material = AssetDatabase.LoadAssetAtPath<Material>(RutaMaterial(preset));
        if (material == null)
        {
            Debug.LogWarning("La escena " + escena + " no tiene material de suelo grid todavia.");
            return null;
        }

        Undo.RecordObject(material, "Restablecer material de suelo");
        Material limpio = new Material(Shader.Find(ShaderSuelo));
        material.shader = limpio.shader;
        material.CopyPropertiesFromMaterial(limpio);
        Object.DestroyImmediate(limpio);
        AplicarPreset(material, preset, null);
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssetIfDirty(material);
        return material;
    }

    [MenuItem("Herramientas/Suelo Grid/Recapturar luz horneada (despues de hornear)", priority = 32)]
    static void RecapturarMenu()
    {
        SueloGrid suelo = Object.FindFirstObjectByType<SueloGrid>();
        if (suelo == null)
        {
            Debug.LogWarning("No hay un SueloGrid en la escena abierta.");
            return;
        }

        CapturarLuz(suelo, Buscar(suelo.gameObject.scene.name).carpeta);
        EditorSceneManager.MarkSceneDirty(suelo.gameObject.scene);
    }

    public static void CrearEnTodas()
    {
        string actual = SceneManager.GetActiveScene().path;
        foreach (Preset preset in Presets)
        {
            string ruta = CarpetaEscenas + preset.escena + ".unity";
            if (!File.Exists(ruta))
                continue;

            Scene escena = EditorSceneManager.OpenScene(ruta, OpenSceneMode.Single);
            if (Instalar(escena) != null)
                EditorSceneManager.SaveScene(escena);
        }

        if (!string.IsNullOrEmpty(actual) && File.Exists(actual))
            EditorSceneManager.OpenScene(actual, OpenSceneMode.Single);
    }

    public static SueloGrid Instalar(Scene escena)
    {
        MeshGrid grid = Object.FindFirstObjectByType<MeshGrid>();
        if (grid == null)
        {
            Debug.LogWarning("La escena " + escena.name + " no tiene MeshGrid; no hay suelo que alinear.");
            return null;
        }

        Preset preset = Buscar(escena.name);
        SueloGrid suelo = Object.FindFirstObjectByType<SueloGrid>(FindObjectsInactive.Include);
        bool nuevo = suelo == null;
        if (nuevo)
            suelo = Crear(grid);

        suelo.meshGrid = grid;
        Renderer original = suelo.SueloOriginal();
        if (original == null)
        {
            Debug.LogWarning("El MeshGrid de " + escena.name + " no tiene Renderer de suelo.");
            return null;
        }

        MeshRenderer render = suelo.GetComponent<MeshRenderer>();
        if (render.sharedMaterial == null || render.sharedMaterial.shader.name != ShaderSuelo)
            render.sharedMaterial = MaterialSuelo(preset, original);

        suelo.Ajustar();
        // Si el suelo modelado tiene partes mas altas que el plano (escalones, altar), se deja visible para que asomen.
        if (nuevo && original.bounds.max.y - suelo.transform.position.y > 0.06f)
        {
            suelo.ocultarSueloOriginal = false;
            suelo.altura = 0.01f;
            suelo.Ajustar();
        }

        CapturarLuz(suelo, preset.carpeta);
        EditorUtility.SetDirty(suelo);
        EditorSceneManager.MarkSceneDirty(escena);
        Debug.Log("Suelo grid listo en " + escena.name + " (" + render.sharedMaterial.name + ").");
        return suelo;
    }

    static Preset Buscar(string escena)
    {
        foreach (Preset preset in Presets)
        {
            if (string.Equals(preset.escena, escena, StringComparison.OrdinalIgnoreCase))
                return preset;
        }

        // Escena sin preset: losas de una celda con la textura del suelo original.
        return new Preset
        {
            escena = escena, carpeta = char.ToUpper(escena[0]) + escena.Substring(1),
            ajustar = m => Valores(m, ("_Patron", 0), ("_Divisiones", 1))
        };
    }

    static SueloGrid Crear(MeshGrid grid)
    {
        GameObject objeto = new GameObject(NombreSuelo);
        Undo.RegisterCreatedObjectUndo(objeto, "Crear suelo grid");
        objeto.layer = grid.gameObject.layer;

        // Al lado del suelo modelado, nunca dentro: MeshGrid toma el primer Renderer de sus hijos.
        Transform padre = grid.transform.parent;
        if (padre != null && padre.rotation == Quaternion.identity)
            objeto.transform.SetParent(padre, false);
        objeto.transform.SetSiblingIndex(grid.transform.GetSiblingIndex() + 1);

        objeto.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        MeshRenderer render = objeto.AddComponent<MeshRenderer>();
        render.shadowCastingMode = ShadowCastingMode.Off;
        render.lightProbeUsage = LightProbeUsage.BlendProbes;
        render.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;

        SueloGrid suelo = objeto.AddComponent<SueloGrid>();
        suelo.meshGrid = grid;

        GridBeatVisualizer pulso = objeto.AddComponent<GridBeatVisualizer>();
        pulso.meshGrid = grid;
        pulso.minPulse = 0.35f;
        pulso.maxPulse = 1f;
        return suelo;
    }

    static string RutaMaterial(Preset preset)
    {
        return AplicadorAmbientacion.CarpetaRaiz + "/" + preset.carpeta + "/M_Suelo_" + preset.carpeta + ".mat";
    }

    static Material MaterialSuelo(Preset preset, Renderer original)
    {
        string ruta = RutaMaterial(preset);
        Material material = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (material != null)
            return material;

        if (!AssetDatabase.IsValidFolder(AplicadorAmbientacion.CarpetaRaiz + "/" + preset.carpeta))
            AssetDatabase.CreateFolder(AplicadorAmbientacion.CarpetaRaiz, preset.carpeta);

        material = new Material(Shader.Find(ShaderSuelo));
        AplicarPreset(material, preset, original);
        AssetDatabase.CreateAsset(material, ruta);
        return material;
    }

    static void AplicarPreset(Material material, Preset preset, Renderer original)
    {
        Texture textura = preset.textura != null
            ? AssetDatabase.LoadAssetAtPath<Texture2D>(preset.textura)
            : original != null && original.sharedMaterial != null && original.sharedMaterial.HasTexture("_BaseMap") ? original.sharedMaterial.GetTexture("_BaseMap") : null;
        if (textura != null)
            material.SetTexture("_BaseMap", textura);
        preset.ajustar(material);
    }

    static void Valores(Material material, params (string nombre, float valor)[] valores)
    {
        foreach ((string nombre, float valor) in valores)
            material.SetFloat(nombre, valor);
    }

    // Dibuja el suelo modelado desde arriba leyendo su lightmap y lo guarda como textura en coordenadas de mundo.
    static void CapturarLuz(SueloGrid suelo, string carpeta)
    {
        Renderer original = suelo.SueloOriginal();
        MeshFilter filtro = original != null ? original.GetComponent<MeshFilter>() : null;
        LightmapData[] lightmaps = LightmapSettings.lightmaps;
        if (filtro == null || filtro.sharedMesh == null || original.lightmapIndex < 0 || original.lightmapIndex >= lightmaps.Length
            || lightmaps[original.lightmapIndex].lightmapColor == null)
        {
            Debug.LogWarning("El suelo original de " + suelo.gameObject.scene.name + " no tiene lightmap; el plano usara las sondas de luz.");
            suelo.luzHorneada = null;
            suelo.Ajustar();
            return;
        }

        Bounds limites = original.bounds;
        Rect rect = new Rect(limites.min.x, limites.min.z, limites.size.x, limites.size.z);
        int ancho = Mathf.Clamp(Mathf.CeilToInt(rect.width * TexelesPorUnidad), 8, 2048);
        int alto = Mathf.Clamp(Mathf.CeilToInt(rect.height * TexelesPorUnidad), 8, 2048);
        Mesh malla = filtro.sharedMesh;

        Material captura = new Material(Shader.Find(ShaderCaptura));
        captura.SetTexture("_Lightmap", lightmaps[original.lightmapIndex].lightmapColor);
        captura.SetVector("_LightmapST", original.lightmapScaleOffset);
        captura.SetVector("_Rect", new Vector4(rect.xMin, rect.yMin, 1f / rect.width, 1f / rect.height));
        captura.SetMatrix("_Matriz", original.localToWorldMatrix);
        captura.SetMatrix("_MatrizNormal", original.localToWorldMatrix.inverse.transpose);
        captura.SetFloat("_VoltearY", SystemInfo.graphicsUVStartsAtTop ? 1f : 0f);
        captura.SetFloat("_UsarUV0", malla.HasVertexAttribute(VertexAttribute.TexCoord1) ? 0f : 1f);

        RenderTexture destino = RenderTexture.GetTemporary(ancho, alto, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        RenderTexture anterior = RenderTexture.active;
        RenderTexture.active = destino;
        GL.Clear(true, true, Color.clear);
        for (int i = 0; i < malla.subMeshCount; i++)
        {
            captura.SetPass(0);
            Graphics.DrawMeshNow(malla, Matrix4x4.identity, i);
        }

        Texture2D luz = new Texture2D(ancho, alto, TextureFormat.RGBAHalf, false, true);
        luz.ReadPixels(new Rect(0, 0, ancho, alto), 0, 0);
        luz.Apply();
        RenderTexture.active = anterior;
        RenderTexture.ReleaseTemporary(destino);
        Object.DestroyImmediate(captura);

        Color[] pixeles = luz.GetPixels();
        if (!RellenarHuecos(pixeles, ancho, alto))
        {
            Object.DestroyImmediate(luz);
            Debug.LogWarning("No se pudo leer el lightmap del suelo de " + suelo.gameObject.scene.name + ".");
            return;
        }
        // Borra el rastro de las juntas viejas para que no se vean debajo de las nuevas.
        Suavizar(pixeles, ancho, alto, 2);
        luz.SetPixels(pixeles);
        luz.Apply();

        string ruta = AplicadorAmbientacion.CarpetaRaiz + "/" + carpeta + "/T_LuzSuelo_" + carpeta + ".exr";
        File.WriteAllBytes(ruta, luz.EncodeToEXR(Texture2D.EXRFlags.CompressZIP));
        Object.DestroyImmediate(luz);

        AssetDatabase.ImportAsset(ruta, ImportAssetOptions.ForceUpdate);
        TextureImporter importador = (TextureImporter)AssetImporter.GetAtPath(ruta);
        importador.sRGBTexture = false;
        importador.mipmapEnabled = false;
        importador.wrapMode = TextureWrapMode.Clamp;
        importador.npotScale = TextureImporterNPOTScale.None;
        importador.filterMode = FilterMode.Bilinear;
        importador.textureCompression = TextureImporterCompression.Uncompressed;
        importador.SaveAndReimport();

        suelo.luzHorneada = AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
        suelo.rectLuz = rect;
        suelo.Ajustar();
        EditorUtility.SetDirty(suelo);
    }

    // Los pixeles sin suelo (biseles, bordes) toman el promedio de sus vecinos ya llenos.
    static bool RellenarHuecos(Color[] pixeles, int ancho, int alto)
    {
        bool[] lleno = new bool[pixeles.Length];
        int faltan = 0;
        for (int i = 0; i < pixeles.Length; i++)
        {
            lleno[i] = pixeles[i].a > 0.5f;
            if (!lleno[i])
                faltan++;
        }

        if (faltan == pixeles.Length)
            return false;

        bool[] llenoNuevo = (bool[])lleno.Clone();
        while (faltan > 0)
        {
            for (int y = 0; y < alto; y++)
            {
                for (int x = 0; x < ancho; x++)
                {
                    int i = y * ancho + x;
                    if (lleno[i])
                        continue;

                    Color suma = Color.clear;
                    int cuenta = 0;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int vx = x + dx;
                            int vy = y + dy;
                            if (vx < 0 || vy < 0 || vx >= ancho || vy >= alto || !lleno[vy * ancho + vx])
                                continue;
                            suma += pixeles[vy * ancho + vx];
                            cuenta++;
                        }
                    }

                    if (cuenta == 0)
                        continue;
                    pixeles[i] = suma / cuenta;
                    pixeles[i].a = 1f;
                    llenoNuevo[i] = true;
                    faltan--;
                }
            }
            Array.Copy(llenoNuevo, lleno, lleno.Length);
        }
        return true;
    }

    static void Suavizar(Color[] pixeles, int ancho, int alto, int pasadas)
    {
        Color[] temporal = new Color[pixeles.Length];
        for (int pasada = 0; pasada < pasadas; pasada++)
        {
            for (int y = 0; y < alto; y++)
                for (int x = 0; x < ancho; x++)
                    temporal[y * ancho + x] = (pixeles[y * ancho + Mathf.Max(x - 1, 0)] + pixeles[y * ancho + x] + pixeles[y * ancho + Mathf.Min(x + 1, ancho - 1)]) / 3f;
            for (int y = 0; y < alto; y++)
                for (int x = 0; x < ancho; x++)
                    pixeles[y * ancho + x] = (temporal[Mathf.Max(y - 1, 0) * ancho + x] + temporal[y * ancho + x] + temporal[Mathf.Min(y + 1, alto - 1) * ancho + x]) / 3f;
        }
    }
}
