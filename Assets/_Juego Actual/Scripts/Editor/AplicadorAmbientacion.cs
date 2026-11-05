using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class AplicadorAmbientacion
{
    public const string CarpetaRaiz = "Assets/_Juego Actual/Arte/Ambientacion";

    const string ShaderSkybox = "UdeM/Ambientacion/SkyboxNoche";
    const string ShaderNiebla = "UdeM/Ambientacion/NieblaParticula";
    const string ShaderHaz = "UdeM/Ambientacion/HazDeLuz";
    const string ShaderOjos = "UdeM/Ambientacion/BrilloOjos";
    const string NombreCapaPersonajes = "Personajes";
    const string RutaTexturaNiebla = CarpetaRaiz + "/T_NieblaPuff.png";
    const string RutaTexturaBrillo = CarpetaRaiz + "/T_Brillo.png";

    const string NombreRaiz = "== Ambientacion ==";
    const string NombreParticulas = "Particulas";
    const string NombreVolumenes = "Volumenes";
    const string NombreAcentos = "Luces Acento";
    const string NombreHaces = "Haces de Luz";

    public static bool Aplicar(PerfilAmbientacion perfil)
    {
        if (perfil == null)
            return false;

        Scene escena = SceneManager.GetActiveScene();
        if (!string.IsNullOrEmpty(perfil.rutaEscena) && escena.path != perfil.rutaEscena)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return false;

            escena = EditorSceneManager.OpenScene(perfil.rutaEscena, OpenSceneMode.Single);
        }

        string carpeta = Path.GetDirectoryName(AssetDatabase.GetAssetPath(perfil)).Replace('\\', '/');
        AsegurarUVsLightmap(perfil);
        AsegurarAssets(perfil, carpeta);
        MarcarEstaticos(perfil);

        Transform raiz = BuscarOCrear(NombreRaiz, null);
        Bounds limites = Limites(perfil, raiz);
        float suelo = AlturaSuelo(limites);

        DesactivarObjetos(perfil);
        Light luna = ConfigurarLuna(perfil);
        ConfigurarEntorno(perfil, luna);
        ConfigurarEmisivos(perfil);
        ConfigurarAcentos(perfil, raiz, carpeta, limites);
        GameObject haces = ConfigurarHaces(perfil, raiz, carpeta, limites, suelo, luna);
        GameObject particulas = ConfigurarParticulas(perfil, raiz, limites, suelo);
        Volume volumen = ConfigurarVolumen(perfil, raiz);
        ConfigurarCamaras();
        ConfigurarSondas(perfil, raiz, limites, suelo);
        ConfigurarHorneado(perfil);
        ConfigurarAtmosfera(perfil, raiz, particulas, volumen, haces);
        ConfigurarLuzPersonajes(perfil, raiz);
        ConfigurarOjos(perfil, raiz, limites, carpeta);

        EditorUtility.SetDirty(perfil);
        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        AssetDatabase.SaveAssets();

        Debug.Log("Ambientacion '" + perfil.name + "' aplicada en " + escena.name + ". Hornea para fijar la luz.");
        return true;
    }

    public static void AplicarSoloLuzPersonajes()
    {
        Scene escena = SceneManager.GetActiveScene();
        PerfilAmbientacion perfil = null;
        string[] guids = AssetDatabase.FindAssets("t:PerfilAmbientacion");
        for (int i = 0; i < guids.Length && perfil == null; i++)
        {
            PerfilAmbientacion candidato = AssetDatabase.LoadAssetAtPath<PerfilAmbientacion>(AssetDatabase.GUIDToAssetPath(guids[i]));
            if (candidato != null && candidato.rutaEscena == escena.path)
                perfil = candidato;
        }

        if (perfil == null)
            perfil = ScriptableObject.CreateInstance<PerfilAmbientacion>();

        ConfigurarLuzPersonajes(perfil, BuscarOCrear(NombreRaiz, null));
        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        Debug.Log("Luz de personajes agregada en " + escena.name + ".");
    }

    public static void Hornear()
    {
        if (Lightmapping.isRunning)
        {
            Debug.LogWarning("Ya hay un horneado en curso.");
            return;
        }

        Lightmapping.bakeCompleted -= AlTerminarHorneado;
        Lightmapping.bakeCompleted += AlTerminarHorneado;

        if (!Lightmapping.BakeAsync())
        {
            Lightmapping.bakeCompleted -= AlTerminarHorneado;
            Debug.LogError("No se pudo iniciar el horneado.");
        }
    }

    static void AlTerminarHorneado()
    {
        Lightmapping.bakeCompleted -= AlTerminarHorneado;
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log("Horneado terminado y escena guardada.");
    }

    public static void CrearCarpeta(string ruta)
    {
        if (AssetDatabase.IsValidFolder(ruta))
            return;

        string padre = Path.GetDirectoryName(ruta).Replace('\\', '/');
        CrearCarpeta(padre);
        AssetDatabase.CreateFolder(padre, Path.GetFileName(ruta));
    }

    static void AsegurarAssets(PerfilAmbientacion perfil, string carpeta)
    {
        CrearCarpeta(carpeta);
        string nombre = perfil.name.Replace("Perfil_", "");

        if (perfil.skybox == null)
        {
            Material skybox = new Material(Shader.Find(ShaderSkybox));
            AssetDatabase.CreateAsset(skybox, carpeta + "/Skybox_" + nombre + ".mat");
            perfil.skybox = skybox;
        }

        if (perfil.materialNiebla == null)
        {
            Material niebla = new Material(Shader.Find(ShaderNiebla));
            niebla.SetTexture("_BaseMap", TexturaNiebla());
            niebla.renderQueue = (int)RenderQueue.Transparent;
            AssetDatabase.CreateAsset(niebla, carpeta + "/M_Niebla_" + nombre + ".mat");
            perfil.materialNiebla = niebla;
        }

        if (perfil.particulasPolvo > 0 && perfil.materialPolvo == null)
        {
            Material polvo = new Material(Shader.Find(ShaderNiebla));
            polvo.SetTexture("_BaseMap", TexturaNiebla());
            polvo.SetFloat("_SuavizadoProfundidad", 0.2f);
            polvo.SetFloat("_CercaInicio", 0.5f);
            polvo.SetFloat("_CercaFin", 2f);
            polvo.renderQueue = (int)RenderQueue.Transparent;
            AssetDatabase.CreateAsset(polvo, carpeta + "/M_Polvo_" + nombre + ".mat");
            perfil.materialPolvo = polvo;
        }

        if (TieneLuciernagas(perfil) && perfil.materialLuciernagas == null)
        {
            Material luciernagas = new Material(Shader.Find(ShaderNiebla));
            luciernagas.SetTexture("_BaseMap", TexturaBrillo());
            luciernagas.SetColor("_BaseColor", new Color(5f, 3.6f, 1.6f));
            luciernagas.SetFloat("_SuavizadoProfundidad", 0.1f);
            luciernagas.SetFloat("_CercaInicio", 0.3f);
            luciernagas.SetFloat("_CercaFin", 1.2f);
            luciernagas.renderQueue = (int)RenderQueue.Transparent;
            AssetDatabase.CreateAsset(luciernagas, carpeta + "/M_Luciernagas_" + nombre + ".mat");
            perfil.materialLuciernagas = luciernagas;
        }

        if (perfil.datosOjos != null && perfil.materialOjos == null)
        {
            Material ojos = new Material(Shader.Find(ShaderOjos));
            ojos.SetTexture("_BaseMap", TexturaBrillo());
            ojos.renderQueue = (int)RenderQueue.Transparent + 2;
            AssetDatabase.CreateAsset(ojos, carpeta + "/M_Ojos_" + nombre + ".mat");
            perfil.materialOjos = ojos;
        }

        if (perfil.haces.activo && perfil.haces.material == null)
        {
            Material haz = new Material(Shader.Find(ShaderHaz));
            haz.renderQueue = (int)RenderQueue.Transparent + 1;
            AssetDatabase.CreateAsset(haz, carpeta + "/M_Haz_" + nombre + ".mat");
            perfil.haces.material = haz;
        }

        if (perfil.perfilVolumen == null)
            perfil.perfilVolumen = CrearPerfilVolumen(carpeta + "/Volumen_" + nombre + ".asset");

        if (perfil.ajustesHorneado == null)
        {
            LightingSettings ajustes = new LightingSettings();
            ajustes.name = "Iluminacion_" + nombre;
            AssetDatabase.CreateAsset(ajustes, carpeta + "/Iluminacion_" + nombre + ".lighting");
            perfil.ajustesHorneado = ajustes;
        }
    }

    static bool TieneLuciernagas(PerfilAmbientacion perfil)
    {
        for (int i = 0; i < perfil.zonasVivas.Length; i++)
        {
            if (perfil.zonasVivas[i].luciernagas > 0)
                return true;
        }

        return false;
    }

    static Texture2D TexturaBrillo()
    {
        Texture2D existente = AssetDatabase.LoadAssetAtPath<Texture2D>(RutaTexturaBrillo);
        if (existente != null)
            return existente;

        const int lado = 64;
        Texture2D textura = new Texture2D(lado, lado, TextureFormat.RGBA32, false);
        Color32[] pixeles = new Color32[lado * lado];

        for (int y = 0; y < lado; y++)
        {
            for (int x = 0; x < lado; x++)
            {
                float u = (x + 0.5f) / lado - 0.5f;
                float v = (y + 0.5f) / lado - 0.5f;
                float r = Mathf.Sqrt(u * u + v * v) * 2f;
                float nucleo = Mathf.Exp(-r * r * 18f);
                float halo = Mathf.Exp(-r * r * 4f) * 0.45f;
                float alfa = Mathf.Clamp01((nucleo + halo) * Mathf.Clamp01(1f - r));
                pixeles[y * lado + x] = new Color32(255, 255, 255, (byte)(alfa * 255f));
            }
        }

        textura.SetPixels32(pixeles);
        textura.Apply();
        File.WriteAllBytes(RutaTexturaBrillo, textura.EncodeToPNG());
        Object.DestroyImmediate(textura);

        AssetDatabase.ImportAsset(RutaTexturaBrillo);
        TextureImporter importador = (TextureImporter)AssetImporter.GetAtPath(RutaTexturaBrillo);
        importador.textureType = TextureImporterType.Default;
        importador.alphaSource = TextureImporterAlphaSource.FromInput;
        importador.alphaIsTransparency = true;
        importador.wrapMode = TextureWrapMode.Clamp;
        importador.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Texture2D>(RutaTexturaBrillo);
    }

    static Texture2D TexturaNiebla()
    {
        Texture2D existente = AssetDatabase.LoadAssetAtPath<Texture2D>(RutaTexturaNiebla);
        if (existente != null)
            return existente;

        CrearCarpeta(CarpetaRaiz);

        const int lado = 128;
        Texture2D textura = new Texture2D(lado, lado, TextureFormat.RGBA32, false);
        Color32[] pixeles = new Color32[lado * lado];

        for (int y = 0; y < lado; y++)
        {
            for (int x = 0; x < lado; x++)
            {
                float u = (x + 0.5f) / lado - 0.5f;
                float v = (y + 0.5f) / lado - 0.5f;
                float radio = Mathf.Sqrt(u * u + v * v) * 2f;
                float caida = Mathf.Clamp01(1f - radio);
                caida = caida * caida * (3f - 2f * caida);

                float ruido = 0f;
                float amplitud = 0.5f;
                float frecuencia = 3f;
                for (int k = 0; k < 4; k++)
                {
                    ruido += amplitud * Mathf.PerlinNoise(
                        x / (float)lado * frecuencia + 11.3f * k,
                        y / (float)lado * frecuencia + 7.1f * k);
                    amplitud *= 0.5f;
                    frecuencia *= 2f;
                }
                ruido /= 0.9375f;

                float alfa = Mathf.Pow(Mathf.Clamp01(caida * (0.35f + 0.9f * ruido)), 1.3f);
                pixeles[y * lado + x] = new Color32(255, 255, 255, (byte)(alfa * 255f));
            }
        }

        textura.SetPixels32(pixeles);
        textura.Apply();
        File.WriteAllBytes(RutaTexturaNiebla, textura.EncodeToPNG());
        Object.DestroyImmediate(textura);

        AssetDatabase.ImportAsset(RutaTexturaNiebla);
        TextureImporter importador = (TextureImporter)AssetImporter.GetAtPath(RutaTexturaNiebla);
        importador.textureType = TextureImporterType.Default;
        importador.alphaSource = TextureImporterAlphaSource.FromInput;
        importador.alphaIsTransparency = true;
        importador.wrapMode = TextureWrapMode.Clamp;
        importador.mipmapEnabled = true;
        importador.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Texture2D>(RutaTexturaNiebla);
    }

    static void AsegurarUVsLightmap(PerfilAmbientacion perfil)
    {
        for (int i = 0; i < perfil.modelosLightmap.Length; i++)
        {
            ModelImporter importador = AssetImporter.GetAtPath(perfil.modelosLightmap[i]) as ModelImporter;
            if (importador == null || importador.generateSecondaryUV)
                continue;

            importador.generateSecondaryUV = true;
            importador.SaveAndReimport();
        }
    }

    static void MarcarEstaticos(PerfilAmbientacion perfil)
    {
        const StaticEditorFlags banderas = StaticEditorFlags.ContributeGI
            | StaticEditorFlags.ReflectionProbeStatic
            | StaticEditorFlags.OccluderStatic
            | StaticEditorFlags.OccludeeStatic;

        if (perfil.objetosEstaticos.Length == 0)
            return;

        Transform[] todos = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < todos.Length; i++)
        {
            if (System.Array.IndexOf(perfil.objetosEstaticos, todos[i].name) < 0)
                continue;

            Renderer[] renderers = todos[i].GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                GameObject objeto = renderers[r].gameObject;
                StaticEditorFlags actuales = GameObjectUtility.GetStaticEditorFlags(objeto);
                if ((actuales & banderas) != banderas)
                    GameObjectUtility.SetStaticEditorFlags(objeto, actuales | banderas);
            }
        }
    }

    public static Material CrearSkybox(string ruta)
    {
        Material skybox = new Material(Shader.Find(ShaderSkybox));
        AssetDatabase.CreateAsset(skybox, ruta);
        return skybox;
    }

    static VolumeProfile CrearPerfilVolumen(string ruta)
    {
        return CrearPerfilVolumen(ruta, new AjustesVolumen());
    }

    public static VolumeProfile CrearPerfilVolumen(string ruta, AjustesVolumen ajustes)
    {
        VolumeProfile perfil = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(perfil, ruta);

        Tonemapping tonemapping = perfil.Add<Tonemapping>(true);
        tonemapping.mode.Override(TonemappingMode.Neutral);

        Bloom bloom = perfil.Add<Bloom>(true);
        bloom.threshold.Override(ajustes.umbralBloom);
        bloom.intensity.Override(ajustes.intensidadBloom);
        bloom.scatter.Override(0.65f);
        bloom.tint.Override(ajustes.tinteBloom);

        ColorAdjustments color = perfil.Add<ColorAdjustments>(true);
        color.postExposure.Override(ajustes.exposicion);
        color.contrast.Override(ajustes.contraste);
        color.saturation.Override(ajustes.saturacion);
        color.colorFilter.Override(ajustes.filtro);

        SplitToning tonos = perfil.Add<SplitToning>(true);
        tonos.shadows.Override(ajustes.sombras);
        tonos.highlights.Override(ajustes.luces);
        tonos.balance.Override(ajustes.balance);

        Vignette vineta = perfil.Add<Vignette>(true);
        vineta.color.Override(ajustes.colorVineta);
        vineta.intensity.Override(ajustes.intensidadVineta);
        vineta.smoothness.Override(0.45f);

        if (ajustes.grano > 0f)
        {
            FilmGrain grano = perfil.Add<FilmGrain>(true);
            grano.type.Override(FilmGrainLookup.Medium1);
            grano.intensity.Override(ajustes.grano);
        }

        for (int i = 0; i < perfil.components.Count; i++)
        {
            VolumeComponent componente = perfil.components[i];
            componente.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(componente, perfil);
        }

        EditorUtility.SetDirty(perfil);
        return perfil;
    }

    static Light ConfigurarLuna(PerfilAmbientacion perfil)
    {
        Light luna = null;
        Light[] luces = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < luces.Length; i++)
        {
            if (luces[i].type == LightType.Directional)
            {
                luna = luces[i];
                break;
            }
        }

        if (luna == null)
        {
            luna = new GameObject("Luna").AddComponent<Light>();
            luna.type = LightType.Directional;
        }

        float elevacion = perfil.elevacionLuna * Mathf.Deg2Rad;
        float azimut = perfil.azimutLuna * Mathf.Deg2Rad;
        Vector3 haciaLuna = new Vector3(
            Mathf.Sin(azimut) * Mathf.Cos(elevacion),
            Mathf.Sin(elevacion),
            Mathf.Cos(azimut) * Mathf.Cos(elevacion));

        luna.transform.rotation = Quaternion.LookRotation(-haciaLuna);
        luna.color = perfil.colorLuna;
        luna.intensity = perfil.intensidadLuna;
        luna.bounceIntensity = 1f;
        luna.shadows = LightShadows.Soft;
        luna.shadowStrength = perfil.fuerzaSombras;
        luna.shadowAngle = 1.5f;
        luna.lightmapBakeType = perfil.modoLuna;
        EditorUtility.SetDirty(luna);
        EditorUtility.SetDirty(luna.transform);

        return luna;
    }

    static void ConfigurarEntorno(PerfilAmbientacion perfil, Light luna)
    {
        Material skybox = perfil.skybox;
        if (skybox.HasProperty("_DireccionLuna"))
            skybox.SetVector("_DireccionLuna", -luna.transform.forward);
        if (skybox.HasProperty("_ColorBruma"))
            skybox.SetColor("_ColorBruma", perfil.colorNiebla);
        EditorUtility.SetDirty(skybox);

        RenderSettings.skybox = skybox;
        RenderSettings.sun = luna;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = perfil.ambienteCielo;
        RenderSettings.ambientEquatorColor = perfil.ambienteHorizonte;
        RenderSettings.ambientGroundColor = perfil.ambienteSuelo;
        RenderSettings.ambientIntensity = 1f;
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
        RenderSettings.reflectionIntensity = perfil.intensidadReflejos;

        RenderSettings.fog = perfil.niebla;
        RenderSettings.fogMode = perfil.modoNiebla;
        RenderSettings.fogColor = perfil.colorNiebla;
        RenderSettings.fogDensity = perfil.densidadNiebla;

        DynamicGI.UpdateEnvironment();
    }

    static void DesactivarObjetos(PerfilAmbientacion perfil)
    {
        if (perfil.desactivarObjetos == null || perfil.desactivarObjetos.Length == 0)
            return;

        Transform[] todos = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < todos.Length; i++)
        {
            GameObject objeto = todos[i].gameObject;
            if (objeto.activeSelf && System.Array.IndexOf(perfil.desactivarObjetos, objeto.name) >= 0)
            {
                objeto.SetActive(false);
                EditorUtility.SetDirty(objeto);
            }
        }
    }

    static void ConfigurarEmisivos(PerfilAmbientacion perfil)
    {
        for (int i = 0; i < perfil.materialesEmisivos.Length; i++)
        {
            MaterialEmisivo emisivo = perfil.materialesEmisivos[i];
            Material material = emisivo.material;
            if (material == null)
                continue;

            Texture baseMap = material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : material.mainTexture;
            material.EnableKeyword("_EMISSION");
            material.SetTexture("_EmissionMap", emisivo.usarTextura ? baseMap : null);
            material.SetColor("_EmissionColor", emisivo.color);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            EditorUtility.SetDirty(material);
        }
    }

    static GameObject ConfigurarHaces(PerfilAmbientacion perfil, Transform raiz, string carpeta, Bounds limites, float suelo, Light luna)
    {
        HacesDeLuz haces = perfil.haces;
        Transform contenedor = raiz.Find(NombreHaces);

        bool modoCielo = haces.modo == ModoHaces.Cielo;
        if (!haces.activo || (!modoCielo && haces.materialVentanas == null))
        {
            if (contenedor != null)
                contenedor.gameObject.SetActive(false);
            return contenedor != null ? contenedor.gameObject : null;
        }

        contenedor = BuscarOCrear(NombreHaces, raiz);
        contenedor.gameObject.SetActive(true);
        Transform luces = BuscarOCrear(NombreHaces + " (luces)", raiz);
        luces.gameObject.SetActive(haces.crearLuces && !modoCielo);

        Material material = haces.material;
        material.SetColor("_Color", haces.color);
        material.SetFloat("_Intensidad", haces.opacidad);
        EditorUtility.SetDirty(material);

        Mesh malla = MallaHaz(carpeta + "/Malla_Haz_" + perfil.name.Replace("Perfil_", "") + ".asset", haces.apertura);
        if (modoCielo)
        {
            HacesDelCielo(perfil, raiz, contenedor, malla, material, limites, suelo, luna);
            for (int i = luces.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(luces.GetChild(i).gameObject);
            return contenedor.gameObject;
        }

        List<Bounds> ventanas = Ventanas(haces.materialVentanas, raiz);

        for (int i = 0; i < ventanas.Count; i++)
        {
            Bounds ventana = ventanas[i];
            bool normalEnX = ventana.size.x < ventana.size.z;
            Vector3 normal = normalEnX ? Vector3.right : Vector3.forward;
            Vector3 haciaCentro = limites.center - ventana.center;
            if (Vector3.Dot(normal, haciaCentro) < 0f)
                normal = -normal;

            Vector3 direccion = Quaternion.AngleAxis(haces.inclinacion, Vector3.Cross(Vector3.up, normal)) * normal;
            Quaternion rotacion = Quaternion.LookRotation(direccion, Vector3.up);
            float ancho = Mathf.Max(0.3f, normalEnX ? ventana.size.z : ventana.size.x);
            float alto = Mathf.Max(0.3f, ventana.size.y);

            Transform haz = BuscarOCrear("Haz " + (i + 1), contenedor);
            haz.SetPositionAndRotation(ventana.center + normal * 0.05f, rotacion);
            haz.localScale = new Vector3(ancho, alto, haces.largo);

            ConfigurarRenderHaz(haz, malla, material);

            Transform luzVentana = BuscarOCrear("Luz Ventana " + (i + 1), luces);
            luzVentana.SetPositionAndRotation(ventana.center + normal * 0.3f, rotacion);
            Light luz = luzVentana.GetComponent<Light>();
            if (luz == null)
                luz = luzVentana.gameObject.AddComponent<Light>();
            luz.type = LightType.Spot;
            luz.color = haces.color;
            luz.intensity = haces.intensidadLuz;
            luz.range = haces.rangoLuz;
            luz.spotAngle = haces.anguloLuz;
            luz.innerSpotAngle = haces.anguloLuz * 0.45f;
            luz.bounceIntensity = 1f;
            luz.lightmapBakeType = LightmapBakeType.Baked;
            luz.shadows = LightShadows.Soft;
            luz.shadowRadius = 0.3f;
            EditorUtility.SetDirty(luz);
        }

        for (int i = contenedor.childCount - 1; i >= ventanas.Count; i--)
            Object.DestroyImmediate(contenedor.GetChild(i).gameObject);
        for (int i = luces.childCount - 1; i >= ventanas.Count; i--)
            Object.DestroyImmediate(luces.GetChild(i).gameObject);

        return contenedor.gameObject;
    }

    static void HacesDelCielo(PerfilAmbientacion perfil, Transform raiz, Transform contenedor, Mesh malla, Material material, Bounds limites, float suelo, Light luna)
    {
        HacesDeLuz haces = perfil.haces;
        Vector3 direccion = luna.transform.forward;
        float seno = Mathf.Max(0.2f, -direccion.y);
        float largo = haces.alturaCielo / seno;
        Random.State estado = Random.state;
        Random.InitState(haces.semilla);

        for (int i = 0; i < haces.cantidadCielo; i++)
        {
            Vector3 llegada = new Vector3(
                Mathf.Lerp(limites.min.x + 1.5f, limites.max.x - 1.5f, Random.value),
                suelo,
                Mathf.Lerp(limites.min.z + 1.5f, limites.max.z - 1.5f, Random.value));
            float ancho = Mathf.Lerp(haces.anchoCielo.x, haces.anchoCielo.y, Random.value);

            Transform haz = BuscarOCrear("Haz " + (i + 1), contenedor);
            haz.SetPositionAndRotation(
                llegada - direccion * largo,
                Quaternion.LookRotation(direccion, Vector3.up) * Quaternion.Euler(0f, 0f, Random.Range(0f, 90f)));
            haz.localScale = new Vector3(ancho, ancho, largo);
            ConfigurarRenderHaz(haz, malla, material);
        }

        Random.state = estado;

        int total = haces.cantidadCielo;
        for (int z = 0; z < perfil.zonasVivas.Length; z++)
        {
            ZonaViva zona = perfil.zonasVivas[z];
            if (!zona.hazDelCielo || !BoundsDe(zona.prefijoObjeto, raiz, out Bounds objeto))
                continue;

            Vector3 haciaCentro = limites.center - objeto.center;
            haciaCentro.y = 0f;
            Vector3 llegada = objeto.ClosestPoint(new Vector3(limites.center.x, objeto.center.y, limites.center.z))
                + haciaCentro.normalized * zona.anchoHaz * 0.5f;
            llegada.y = suelo;

            Transform haz = BuscarOCrear("Haz " + (total + 1), contenedor);
            haz.SetPositionAndRotation(llegada - direccion * largo, Quaternion.LookRotation(direccion, Vector3.up));
            haz.localScale = new Vector3(zona.anchoHaz, zona.anchoHaz, largo);
            ConfigurarRenderHaz(haz, malla, material);
            total++;
        }

        for (int i = contenedor.childCount - 1; i >= total; i--)
            Object.DestroyImmediate(contenedor.GetChild(i).gameObject);
    }

    static void ConfigurarRenderHaz(Transform haz, Mesh malla, Material material)
    {
        MeshFilter filtro = haz.GetComponent<MeshFilter>();
        if (filtro == null)
            filtro = haz.gameObject.AddComponent<MeshFilter>();
        filtro.sharedMesh = malla;

        MeshRenderer render = haz.GetComponent<MeshRenderer>();
        if (render == null)
            render = haz.gameObject.AddComponent<MeshRenderer>();
        render.sharedMaterial = material;
        render.shadowCastingMode = ShadowCastingMode.Off;
        render.receiveShadows = false;
        render.lightProbeUsage = LightProbeUsage.Off;
        render.reflectionProbeUsage = ReflectionProbeUsage.Off;
        GameObjectUtility.SetStaticEditorFlags(haz.gameObject, 0);
        EditorUtility.SetDirty(render);
    }

    static List<Bounds> Ventanas(Material materialVentanas, Transform raiz)
    {
        List<Bounds> ventanas = new List<Bounds>();
        Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer.transform.IsChildOf(raiz) || System.Array.IndexOf(renderer.sharedMaterials, materialVentanas) < 0)
                continue;

            Bounds b = renderer.bounds;
            int existente = ventanas.FindIndex(v => (v.center - b.center).sqrMagnitude < 0.25f);
            if (existente >= 0)
            {
                Bounds unida = ventanas[existente];
                unida.Encapsulate(b);
                ventanas[existente] = unida;
            }
            else
            {
                ventanas.Add(b);
            }
        }

        ventanas.Sort((a, b) =>
        {
            int x = a.center.x.CompareTo(b.center.x);
            return x != 0 ? x : a.center.z.CompareTo(b.center.z);
        });
        return ventanas;
    }

    static Mesh MallaHaz(string ruta, float apertura)
    {
        Mesh malla = AssetDatabase.LoadAssetAtPath<Mesh>(ruta);
        bool nueva = malla == null;
        if (nueva)
            malla = new Mesh { name = "Haz" };
        malla.Clear();

        Vector3[] inicio =
        {
            new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f)
        };

        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> normales = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangulos = new List<int>();

        for (int cara = 0; cara < 4; cara++)
        {
            Vector3 a0 = inicio[cara];
            Vector3 b0 = inicio[(cara + 1) % 4];
            Vector3 a1 = new Vector3(a0.x * apertura, a0.y * apertura, 1f);
            Vector3 b1 = new Vector3(b0.x * apertura, b0.y * apertura, 1f);
            Vector3 normal = Vector3.Cross(b0 - a0, a1 - a0).normalized;
            if (Vector3.Dot(normal, (a0 + b0) * 0.5f) < 0f)
                normal = -normal;

            int baseIndice = vertices.Count;
            vertices.AddRange(new[] { a0, b0, b1, a1 });
            normales.AddRange(new[] { normal, normal, normal, normal });
            uvs.AddRange(new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) });
            triangulos.AddRange(new[] { baseIndice, baseIndice + 1, baseIndice + 2, baseIndice, baseIndice + 2, baseIndice + 3 });
        }

        malla.SetVertices(vertices);
        malla.SetNormals(normales);
        malla.SetUVs(0, uvs);
        malla.SetTriangles(triangulos, 0);
        malla.RecalculateBounds();

        if (nueva)
            AssetDatabase.CreateAsset(malla, ruta);
        else
            EditorUtility.SetDirty(malla);

        return malla;
    }

    static void ConfigurarAcentos(PerfilAmbientacion perfil, Transform raiz, string carpeta, Bounds limites)
    {
        Transform contenedor = BuscarOCrear(NombreAcentos, raiz);
        Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        for (int a = 0; a < perfil.lucesAcento.Length; a++)
        {
            LuzAcento acento = perfil.lucesAcento[a];
            if (string.IsNullOrEmpty(acento.prefijoObjeto))
                continue;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer.transform.IsChildOf(raiz))
                    continue;

                if (!renderer.gameObject.name.StartsWith(acento.prefijoObjeto, System.StringComparison.OrdinalIgnoreCase))
                    continue;

                if (CoincidePrefijo(renderer.gameObject.name, acento.excluirPrefijos))
                    continue;

                Light[] hijas = renderer.GetComponentsInChildren<Light>(true);
                Light luz = null;
                if (hijas.Length == 0)
                {
                    Transform existente = contenedor.Find("Luz " + renderer.name);
                    luz = existente != null ? existente.GetComponent<Light>() : null;
                }

                if (hijas.Length == 0 && luz == null)
                {
                    luz = new GameObject("Luz " + renderer.name).AddComponent<Light>();
                    luz.transform.SetParent(contenedor, false);
                    Bounds b = renderer.bounds;
                    Vector3 posicion = b.center + Vector3.up * b.extents.y * acento.alturaRelativa;
                    Vector3 haciaCentro = limites.center - posicion;
                    haciaCentro.y = 0f;
                    if (haciaCentro.sqrMagnitude > 0.0001f)
                        posicion += haciaCentro.normalized * acento.separacionInterior;
                    luz.transform.position = posicion;
                }

                Light[] luces = hijas.Length > 0 ? hijas : new[] { luz };
                for (int l = 0; l < luces.Length; l++)
                {
                    Light actual = luces[l];
                    actual.type = LightType.Point;
                    actual.color = acento.color;
                    actual.intensity = acento.intensidad;
                    actual.range = acento.rango;
                    actual.bounceIntensity = 1f;
                    actual.lightmapBakeType = acento.modo;
                    actual.shadows = acento.modo == LightmapBakeType.Baked ? LightShadows.Soft : LightShadows.None;
                    actual.shadowRadius = 0.15f;
                    EditorUtility.SetDirty(actual);
                }

                if (acento.emision > 0f)
                    AplicarEmision(renderer, acento, carpeta);
            }
        }
    }

    static void AplicarEmision(Renderer renderer, LuzAcento acento, string carpeta)
    {
        Material[] materiales = renderer.sharedMaterials;
        for (int i = 0; i < materiales.Length; i++)
        {
            Material original = materiales[i];
            if (original == null)
                continue;

            Material emisivo = original;
            if (!original.name.EndsWith("_Emisivo"))
            {
                string ruta = carpeta + "/M_" + original.name + "_" + acento.prefijoObjeto + "_Emisivo.mat";
                emisivo = AssetDatabase.LoadAssetAtPath<Material>(ruta);
                if (emisivo == null)
                {
                    emisivo = new Material(original);
                    AssetDatabase.CreateAsset(emisivo, ruta);
                }
            }

            Texture baseMap = original.HasProperty("_BaseMap") ? original.GetTexture("_BaseMap") : original.mainTexture;
            emisivo.EnableKeyword("_EMISSION");
            emisivo.SetTexture("_EmissionMap", baseMap);
            emisivo.SetColor("_EmissionColor", acento.color * acento.emision);
            emisivo.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(emisivo);
            materiales[i] = emisivo;
        }

        renderer.sharedMaterials = materiales;
        EditorUtility.SetDirty(renderer);
    }

    static void ConfigurarZonasVivas(PerfilAmbientacion perfil, Transform contenedor, Transform raiz)
    {
        string carpeta = Path.GetDirectoryName(AssetDatabase.GetAssetPath(perfil)).Replace('\\', '/');
        if (perfil.materialLuciernagas != null)
        {
            perfil.materialLuciernagas.SetColor("_BaseColor", perfil.colorLuciernagas);
            EditorUtility.SetDirty(perfil.materialLuciernagas);
        }

        HashSet<string> usadas = new HashSet<string>();

        for (int z = 0; z < perfil.zonasVivas.Length; z++)
        {
            ZonaViva zona = perfil.zonasVivas[z];
            if (zona.luciernagas <= 0 || perfil.materialLuciernagas == null || !BoundsDe(zona.prefijoObjeto, raiz, out Bounds objeto))
                continue;

            string nombre = "Vida " + zona.prefijoObjeto;
            usadas.Add(nombre);
            ParticleSystem sistema = ObtenerSistema(contenedor, nombre);
            sistema.gameObject.SetActive(true);
            sistema.transform.position = objeto.center;
            ConfigurarLuciernagas(sistema, zona, objeto, perfil.materialLuciernagas);
            if (zona.emitirDesdeLuces)
                EmitirDesdeLuces(sistema, zona, raiz, carpeta);
            sistema.Play();
        }

        for (int i = contenedor.childCount - 1; i >= 0; i--)
        {
            Transform hijo = contenedor.GetChild(i);
            if (hijo.name.StartsWith("Vida ") && !usadas.Contains(hijo.name))
                Object.DestroyImmediate(hijo.gameObject);
        }
    }

    static void ConfigurarLuciernagas(ParticleSystem sistema, ZonaViva zona, Bounds objeto, Material material)
    {
        const float vida = 7f;
        int cantidad = Mathf.Max(1, zona.luciernagas);

        sistema.useAutoRandomSeed = false;
        sistema.randomSeed = 404;

        ParticleSystem.MainModule principal = sistema.main;
        principal.duration = vida;
        principal.loop = true;
        principal.prewarm = true;
        principal.playOnAwake = true;
        principal.startLifetime = new ParticleSystem.MinMaxCurve(vida * 0.7f, vida * 1.3f);
        principal.startSpeed = 0f;
        principal.startSize = new ParticleSystem.MinMaxCurve(zona.tamano.x, zona.tamano.y);
        principal.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 0.7f), Color.white);
        principal.maxParticles = Mathf.CeilToInt(cantidad * 1.3f);
        principal.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emision = sistema.emission;
        emision.enabled = true;
        emision.rateOverTime = cantidad / vida;

        ParticleSystem.ShapeModule forma = sistema.shape;
        forma.shapeType = ParticleSystemShapeType.Box;
        forma.scale = objeto.size + new Vector3(zona.radio * 2f, zona.radio, zona.radio * 2f);
        forma.rotation = Vector3.zero;

        ParticleSystem.VelocityOverLifetimeModule velocidad = sistema.velocityOverLifetime;
        velocidad.enabled = true;
        velocidad.space = ParticleSystemSimulationSpace.World;
        velocidad.x = new ParticleSystem.MinMaxCurve(-0.02f, 0.02f);
        velocidad.y = new ParticleSystem.MinMaxCurve(zona.velocidadSubida * 0.3f, zona.velocidadSubida);
        velocidad.z = new ParticleSystem.MinMaxCurve(-0.02f, 0.02f);

        ParticleSystem.NoiseModule ruido = sistema.noise;
        ruido.enabled = true;
        ruido.strength = 0.6f;
        ruido.frequency = 0.5f;
        ruido.scrollSpeed = 0.35f;
        ruido.damping = true;
        ruido.quality = ParticleSystemNoiseQuality.Medium;
        ruido.sizeAmount = new ParticleSystem.MinMaxCurve(0.6f);

        ParticleSystem.ColorOverLifetimeModule color = sistema.colorOverLifetime;
        color.enabled = true;
        Gradient titileo = new Gradient();
        titileo.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.15f),
                new GradientAlphaKey(0.45f, 0.3f),
                new GradientAlphaKey(1f, 0.45f),
                new GradientAlphaKey(0.55f, 0.6f),
                new GradientAlphaKey(1f, 0.78f),
                new GradientAlphaKey(0f, 1f)
            });
        color.color = titileo;

        Transform plantillaTransform = BuscarOCrear("Plantilla Luz", sistema.transform);
        plantillaTransform.gameObject.SetActive(false);
        Light plantilla = plantillaTransform.GetComponent<Light>();
        if (plantilla == null)
            plantilla = plantillaTransform.gameObject.AddComponent<Light>();
        plantilla.type = LightType.Point;
        plantilla.color = zona.colorLuz;
        plantilla.intensity = zona.intensidadLuz;
        plantilla.range = zona.rangoLuz;
        plantilla.shadows = LightShadows.None;
        plantilla.lightmapBakeType = LightmapBakeType.Realtime;
        EditorUtility.SetDirty(plantilla);

        ParticleSystem.LightsModule luces = sistema.lights;
        luces.enabled = zona.lucesReales > 0;
        luces.light = plantilla;
        luces.ratio = 1f;
        luces.maxLights = zona.lucesReales;
        luces.useRandomDistribution = true;
        luces.useParticleColor = false;
        luces.sizeAffectsRange = false;
        luces.alphaAffectsIntensity = true;
        luces.intensityMultiplier = 1f;
        luces.rangeMultiplier = 1f;

        ConfigurarRender(sistema, material, ParticleSystemRenderMode.Billboard);
    }

    static void EmitirDesdeLuces(ParticleSystem sistema, ZonaViva zona, Transform raiz, string carpeta)
    {
        List<Vector3> puntos = new List<Vector3>();
        Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer.transform.IsChildOf(raiz) || !renderer.gameObject.name.StartsWith(zona.prefijoObjeto, System.StringComparison.OrdinalIgnoreCase))
                continue;

            Light[] luces = renderer.GetComponentsInChildren<Light>(true);
            for (int l = 0; l < luces.Length; l++)
                puntos.Add(sistema.transform.InverseTransformPoint(luces[l].transform.position));
        }

        if (puntos.Count == 0)
            return;

        string ruta = carpeta + "/Puntos_" + zona.prefijoObjeto + ".asset";
        Mesh malla = AssetDatabase.LoadAssetAtPath<Mesh>(ruta);
        bool nueva = malla == null;
        if (nueva)
            malla = new Mesh { name = "Puntos " + zona.prefijoObjeto };
        malla.Clear();
        // Triangulos y no puntos: en una build el modulo Shape rechaza las mallas de puntos ("Mesh
        // used in Particle System Shape Module is not valid") aunque sean legibles. Como emite desde
        // los vertices (Vertex), como se unan da igual; se repite el ultimo hasta un multiplo de 3.
        while (puntos.Count % 3 != 0)
            puntos.Add(puntos[puntos.Count - 1]);
        malla.SetVertices(puntos);
        int[] indices = new int[puntos.Count];
        for (int i = 0; i < indices.Length; i++)
            indices[i] = i;
        malla.SetIndices(indices, MeshTopology.Triangles, 0);
        malla.RecalculateBounds();
        if (nueva)
            AssetDatabase.CreateAsset(malla, ruta);
        else
            EditorUtility.SetDirty(malla);

        ParticleSystem.ShapeModule forma = sistema.shape;
        forma.shapeType = ParticleSystemShapeType.Mesh;
        forma.meshShapeType = ParticleSystemMeshShapeType.Vertex;
        forma.mesh = malla;
        forma.scale = Vector3.one;
        forma.rotation = Vector3.zero;
        forma.position = Vector3.zero;
        forma.randomPositionAmount = zona.radio * 0.2f;
    }

    static bool BoundsDe(string prefijo, Transform raiz, out Bounds limites)
    {
        limites = new Bounds();
        bool encontrado = false;
        if (string.IsNullOrEmpty(prefijo))
            return false;

        Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer.transform.IsChildOf(raiz) || renderer is ParticleSystemRenderer)
                continue;
            if (!renderer.gameObject.name.StartsWith(prefijo, System.StringComparison.OrdinalIgnoreCase))
                continue;

            if (!encontrado)
            {
                limites = renderer.bounds;
                encontrado = true;
            }
            else
            {
                limites.Encapsulate(renderer.bounds);
            }
        }

        return encontrado;
    }

    static GameObject ConfigurarParticulas(PerfilAmbientacion perfil, Transform raiz, Bounds limites, float suelo)
    {
        Transform contenedor = BuscarOCrear(NombreParticulas, raiz);

        Material material = perfil.materialNiebla;
        material.SetColor("_BaseColor", perfil.colorParticulas);
        EditorUtility.SetDirty(material);

        ParticleSystem interior = ObtenerSistema(contenedor, "Niebla Suelo");
        interior.transform.position = new Vector3(limites.center.x, suelo + perfil.alturaNieblaSuelo * 0.5f, limites.center.z);
        ConfigurarNiebla(interior, perfil, perfil.particulasSuelo, perfil.tamanoSuelo, perfil.opacidadSuelo, 101);
        ParticleSystem.ShapeModule formaInterior = interior.shape;
        formaInterior.shapeType = ParticleSystemShapeType.Box;
        formaInterior.scale = new Vector3(limites.size.x, perfil.alturaNieblaSuelo, limites.size.z);
        formaInterior.rotation = Vector3.zero;
        ConfigurarRender(interior, material, ParticleSystemRenderMode.HorizontalBillboard);

        ParticleSystem exterior = ObtenerSistema(contenedor, "Niebla Exterior");
        exterior.transform.position = new Vector3(limites.center.x, suelo + perfil.alturaExterior, limites.center.z);
        ConfigurarNiebla(exterior, perfil, perfil.particulasExterior, perfil.tamanoExterior, perfil.opacidadExterior, 202);
        ParticleSystem.ShapeModule formaExterior = exterior.shape;
        formaExterior.shapeType = ParticleSystemShapeType.Circle;
        formaExterior.rotation = new Vector3(90f, 0f, 0f);
        formaExterior.scale = Vector3.one;
        formaExterior.radius = Mathf.Max(limites.extents.x, limites.extents.z) * perfil.radioExterior;
        formaExterior.radiusThickness = 0.45f;
        formaExterior.arc = 360f;
        ConfigurarRender(exterior, material, ParticleSystemRenderMode.Billboard);

        ParticleSystem polvo = ObtenerSistema(contenedor, "Polvo");
        polvo.transform.position = new Vector3(limites.center.x, suelo + perfil.alturaPolvo * 0.5f, limites.center.z);
        ConfigurarPolvo(polvo, perfil, limites);

        interior.gameObject.SetActive(perfil.particulasSuelo > 0);
        polvo.gameObject.SetActive(perfil.particulasPolvo > 0 && perfil.materialPolvo != null);
        exterior.gameObject.SetActive(perfil.particulasExterior > 0);
        contenedor.gameObject.SetActive(perfil.nieblaParticulas);
        interior.Play();
        exterior.Play();
        polvo.Play();

        ConfigurarZonasVivas(perfil, contenedor, raiz);

        return contenedor.gameObject;
    }

    static void ConfigurarPolvo(ParticleSystem sistema, PerfilAmbientacion perfil, Bounds limites)
    {
        const float vida = 11f;
        int cantidad = Mathf.Max(1, perfil.particulasPolvo);

        sistema.useAutoRandomSeed = false;
        sistema.randomSeed = 303;

        ParticleSystem.MainModule principal = sistema.main;
        principal.duration = vida;
        principal.loop = true;
        principal.prewarm = true;
        principal.playOnAwake = true;
        principal.startLifetime = new ParticleSystem.MinMaxCurve(vida * 0.7f, vida * 1.3f);
        principal.startSpeed = 0f;
        principal.startSize = new ParticleSystem.MinMaxCurve(perfil.tamanoPolvo.x, perfil.tamanoPolvo.y);
        principal.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        principal.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 0.5f), Color.white);
        principal.maxParticles = Mathf.CeilToInt(cantidad * 1.3f);
        principal.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emision = sistema.emission;
        emision.enabled = true;
        emision.rateOverTime = cantidad / vida;

        ParticleSystem.ShapeModule forma = sistema.shape;
        forma.shapeType = ParticleSystemShapeType.Box;
        forma.scale = new Vector3(limites.size.x, perfil.alturaPolvo, limites.size.z);
        forma.rotation = Vector3.zero;

        ParticleSystem.VelocityOverLifetimeModule velocidad = sistema.velocityOverLifetime;
        velocidad.enabled = true;
        velocidad.space = ParticleSystemSimulationSpace.World;
        velocidad.x = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
        velocidad.y = new ParticleSystem.MinMaxCurve(perfil.subidaPolvo.x, perfil.subidaPolvo.y);
        velocidad.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);

        ParticleSystem.NoiseModule ruido = sistema.noise;
        ruido.enabled = true;
        ruido.strength = 0.3f;
        ruido.frequency = 0.35f;
        ruido.scrollSpeed = 0.2f;
        ruido.quality = ParticleSystemNoiseQuality.Low;

        ParticleSystem.ColorOverLifetimeModule color = sistema.colorOverLifetime;
        color.enabled = true;
        Gradient degradado = new Gradient();
        degradado.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.2f),
                new GradientAlphaKey(1f, 0.75f),
                new GradientAlphaKey(0f, 1f)
            });
        color.color = degradado;

        if (perfil.materialPolvo != null)
        {
            perfil.materialPolvo.SetColor("_BaseColor", perfil.colorPolvo);
            EditorUtility.SetDirty(perfil.materialPolvo);
            ConfigurarRender(sistema, perfil.materialPolvo, ParticleSystemRenderMode.Billboard);
        }
    }

    static ParticleSystem ObtenerSistema(Transform padre, string nombre)
    {
        Transform hijo = BuscarOCrear(nombre, padre);
        ParticleSystem sistema = hijo.GetComponent<ParticleSystem>();
        if (sistema == null)
            sistema = hijo.gameObject.AddComponent<ParticleSystem>();

        sistema.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return sistema;
    }

    static void ConfigurarNiebla(ParticleSystem sistema, PerfilAmbientacion perfil, int cantidad, Vector2 tamano, float opacidad, uint semilla)
    {
        const float vida = 20f;
        cantidad = Mathf.Max(1, cantidad);

        sistema.useAutoRandomSeed = false;
        sistema.randomSeed = semilla;

        ParticleSystem.MainModule principal = sistema.main;
        principal.duration = vida;
        principal.loop = true;
        principal.prewarm = true;
        principal.playOnAwake = true;
        principal.startLifetime = new ParticleSystem.MinMaxCurve(vida * 0.8f, vida * 1.2f);
        principal.startSpeed = 0f;
        principal.startSize = new ParticleSystem.MinMaxCurve(tamano.x, tamano.y);
        principal.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        principal.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 1f, 1f, opacidad * 0.6f),
            new Color(1f, 1f, 1f, opacidad));
        principal.maxParticles = Mathf.CeilToInt(cantidad * 1.3f);
        principal.simulationSpace = ParticleSystemSimulationSpace.World;
        principal.scalingMode = ParticleSystemScalingMode.Hierarchy;

        ParticleSystem.EmissionModule emision = sistema.emission;
        emision.enabled = true;
        emision.rateOverTime = cantidad / vida;

        ParticleSystem.VelocityOverLifetimeModule velocidad = sistema.velocityOverLifetime;
        velocidad.enabled = true;
        velocidad.space = ParticleSystemSimulationSpace.World;
        velocidad.x = new ParticleSystem.MinMaxCurve(perfil.viento.x * 0.5f, perfil.viento.x * 1.5f);
        velocidad.y = new ParticleSystem.MinMaxCurve(-0.01f, 0.01f);
        velocidad.z = new ParticleSystem.MinMaxCurve(perfil.viento.y * 0.5f, perfil.viento.y * 1.5f);

        ParticleSystem.ColorOverLifetimeModule color = sistema.colorOverLifetime;
        color.enabled = true;
        Gradient degradado = new Gradient();
        degradado.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.3f),
                new GradientAlphaKey(1f, 0.7f),
                new GradientAlphaKey(0f, 1f)
            });
        color.color = degradado;

        ParticleSystem.RotationOverLifetimeModule rotacion = sistema.rotationOverLifetime;
        rotacion.enabled = true;
        rotacion.z = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);

        ParticleSystem.SizeOverLifetimeModule crecimiento = sistema.sizeOverLifetime;
        crecimiento.enabled = true;
        crecimiento.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.8f, 1f, 1.2f));
    }

    static void ConfigurarRender(ParticleSystem sistema, Material material, ParticleSystemRenderMode modo)
    {
        ParticleSystemRenderer render = sistema.GetComponent<ParticleSystemRenderer>();
        render.renderMode = modo;
        render.sharedMaterial = material;
        render.sortMode = ParticleSystemSortMode.Distance;
        render.shadowCastingMode = ShadowCastingMode.Off;
        render.receiveShadows = false;
        render.lightProbeUsage = LightProbeUsage.Off;
        render.reflectionProbeUsage = ReflectionProbeUsage.Off;
        render.minParticleSize = 0f;
        render.maxParticleSize = 4f;
        EditorUtility.SetDirty(render);
    }

    static Volume ConfigurarVolumen(PerfilAmbientacion perfil, Transform raiz)
    {
        Transform contenedor = BuscarOCrear(NombreVolumenes, raiz);
        Transform global = BuscarOCrear("Volumen Global", contenedor);

        Volume volumen = global.GetComponent<Volume>();
        if (volumen == null)
            volumen = global.gameObject.AddComponent<Volume>();

        volumen.isGlobal = true;
        volumen.priority = 1f;
        volumen.weight = 1f;
        volumen.sharedProfile = perfil.perfilVolumen;
        volumen.enabled = perfil.postProceso;
        EditorUtility.SetDirty(volumen);

        return volumen;
    }

    static void ConfigurarCamaras()
    {
        Camera[] camaras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < camaras.Length; i++)
        {
            UniversalAdditionalCameraData datos = camaras[i].GetUniversalAdditionalCameraData();
            datos.renderPostProcessing = true;
            EditorUtility.SetDirty(datos);
        }
    }

    static void ConfigurarSondas(PerfilAmbientacion perfil, Transform raiz, Bounds limites, float suelo)
    {
        Transform sondas = BuscarOCrear("Sondas de Luz", raiz);
        sondas.position = Vector3.zero;
        sondas.rotation = Quaternion.identity;

        LightProbeGroup grupo = sondas.GetComponent<LightProbeGroup>();
        if (grupo == null)
            grupo = sondas.gameObject.AddComponent<LightProbeGroup>();

        float[] alturas = perfil.alturasSondas != null && perfil.alturasSondas.Length > 0
            ? perfil.alturasSondas
            : new[] { 0.3f, 1.5f, 3f };

        float paso = Mathf.Max(1f, perfil.separacionSondas);
        int nx = Mathf.Max(2, Mathf.CeilToInt(limites.size.x / paso) + 1);
        int nz = Mathf.Max(2, Mathf.CeilToInt(limites.size.z / paso) + 1);

        List<Vector3> posiciones = new List<Vector3>();
        for (int x = 0; x < nx; x++)
        {
            for (int z = 0; z < nz; z++)
            {
                for (int h = 0; h < alturas.Length; h++)
                {
                    posiciones.Add(new Vector3(
                        Mathf.Lerp(limites.min.x, limites.max.x, x / (float)(nx - 1)),
                        suelo + alturas[h],
                        Mathf.Lerp(limites.min.z, limites.max.z, z / (float)(nz - 1))));
                }
            }
        }

        grupo.probePositions = posiciones.ToArray();
        EditorUtility.SetDirty(grupo);

        Transform reflejos = BuscarOCrear("Sonda de Reflejos", raiz);
        reflejos.position = new Vector3(limites.center.x, suelo + 2f, limites.center.z);

        ReflectionProbe sonda = reflejos.GetComponent<ReflectionProbe>();
        if (sonda == null)
            sonda = reflejos.gameObject.AddComponent<ReflectionProbe>();

        sonda.mode = ReflectionProbeMode.Baked;
        sonda.size = new Vector3(limites.size.x + 4f, Mathf.Max(8f, limites.size.y + 4f), limites.size.z + 4f);
        sonda.center = Vector3.zero;
        sonda.resolution = 128;
        sonda.hdr = true;
        sonda.boxProjection = false;
        EditorUtility.SetDirty(sonda);
    }

    static void ConfigurarHorneado(PerfilAmbientacion perfil)
    {
        LightingSettings ajustes = perfil.ajustesHorneado;
        ajustes.bakedGI = true;
        ajustes.realtimeGI = false;
        ajustes.mixedBakeMode = MixedLightingMode.Shadowmask;
        ajustes.lightmapper = LightingSettings.Lightmapper.ProgressiveGPU;
        ajustes.lightmapResolution = perfil.texelesPorUnidad;
        ajustes.lightmapMaxSize = perfil.tamanoAtlas;
        ajustes.lightmapPadding = 2;
        ajustes.directionalityMode = LightmapsMode.CombinedDirectional;
        ajustes.lightmapCompression = LightmapCompression.NormalQuality;
        ajustes.ao = perfil.oclusionAmbiental;
        ajustes.aoMaxDistance = 1f;
        ajustes.aoExponentIndirect = 1f;
        ajustes.aoExponentDirect = 0f;
        ajustes.directSampleCount = 32;
        ajustes.indirectSampleCount = 512;
        ajustes.environmentSampleCount = 256;
        ajustes.maxBounces = perfil.rebotes;
        ajustes.filteringMode = LightingSettings.FilterMode.Auto;
        EditorUtility.SetDirty(ajustes);

        Lightmapping.lightingSettings = ajustes;
    }

    static void ConfigurarAtmosfera(PerfilAmbientacion perfil, Transform raiz, GameObject particulas, Volume volumen, GameObject haces)
    {
        AtmosferaEscena atmosfera = raiz.GetComponent<AtmosferaEscena>();
        if (atmosfera == null)
            atmosfera = raiz.gameObject.AddComponent<AtmosferaEscena>();

        atmosfera.particulas = perfil.nieblaParticulas;
        atmosfera.postProceso = perfil.postProceso;
        atmosfera.nieblaUnity = perfil.niebla;
        atmosfera.calidadesSinParticulas = (int[])perfil.calidadesSinParticulas.Clone();
        atmosfera.calidadesSinPostProceso = (int[])perfil.calidadesSinPostProceso.Clone();
        atmosfera.volumetricos = perfil.haces.activo;
        atmosfera.calidadesSinVolumetricos = (int[])perfil.calidadesSinVolumetricos.Clone();
        atmosfera.objetosParticulas = new[] { particulas };
        atmosfera.volumenes = new[] { volumen };
        atmosfera.objetosVolumetricos = haces != null ? new[] { haces } : new GameObject[0];
        atmosfera.Aplicar();
        EditorUtility.SetDirty(atmosfera);
    }

    static void ConfigurarLuzPersonajes(PerfilAmbientacion perfil, Transform raiz)
    {
        NombrarCapaPersonajes();

        Transform rig = BuscarOCrear("Luz Personajes", raiz);
        rig.gameObject.SetActive(perfil.luzPersonajes);

        LuzPersonajes componente = rig.GetComponent<LuzPersonajes>();
        if (componente == null)
            componente = rig.gameObject.AddComponent<LuzPersonajes>();

        componente.relleno = LuzDeRig(rig, "Relleno", perfil.colorRellenoPersonajes, perfil.intensidadRellenoPersonajes);
        componente.contorno = LuzDeRig(rig, "Contorno", perfil.colorContornoPersonajes, perfil.intensidadContornoPersonajes);
        componente.Buscar();
        EditorUtility.SetDirty(componente);
    }

    static Light LuzDeRig(Transform rig, string nombre, Color color, float intensidad)
    {
        Transform hijo = BuscarOCrear(nombre, rig);
        Light luz = hijo.GetComponent<Light>();
        if (luz == null)
            luz = hijo.gameObject.AddComponent<Light>();

        luz.lightmapBakeType = LightmapBakeType.Realtime;
        LuzPersonajes.ConfigurarLuz(luz, color, intensidad);
        EditorUtility.SetDirty(luz);
        return luz;
    }

    static void NombrarCapaPersonajes()
    {
        Object[] gestor = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (gestor == null || gestor.Length == 0)
            return;

        SerializedObject serializado = new SerializedObject(gestor[0]);
        SerializedProperty capas = serializado.FindProperty("m_RenderingLayers");
        if (capas == null || !capas.isArray || capas.arraySize <= LuzPersonajes.CapaPersonajes)
            return;

        SerializedProperty capa = capas.GetArrayElementAtIndex(LuzPersonajes.CapaPersonajes);
        if (capa.stringValue == NombreCapaPersonajes)
            return;

        capa.stringValue = NombreCapaPersonajes;
        serializado.ApplyModifiedProperties();
    }

#pragma warning disable 0649
    [System.Serializable]
    class DatosOjos
    {
        public ObjetoOjos[] objetos;
    }

    [System.Serializable]
    class ObjetoOjos
    {
        public string nombre;
        public Ojo[] ojos;
        public PilarOjos[] pilares;
    }

    [System.Serializable]
    class Ojo
    {
        public float[] p;
        public float[] n;
        public float t;
        public float f;
    }

    [System.Serializable]
    class PilarOjos
    {
        public float[] c;
        public float r;
        public float alto;
    }

#pragma warning restore 0649

    static void ConfigurarOjos(PerfilAmbientacion perfil, Transform raiz, Bounds limites, string carpeta)
    {
        Transform contenedor = raiz.Find("Ojos Calaveras");
        Transform luces = raiz.Find("Ojos Calaveras (luces)");
        if (perfil.datosOjos == null || perfil.materialOjos == null)
        {
            if (contenedor != null)
                contenedor.gameObject.SetActive(false);
            if (luces != null)
                luces.gameObject.SetActive(false);
            return;
        }

        perfil.materialOjos.SetColor("_Color", perfil.colorOjos);
        EditorUtility.SetDirty(perfil.materialOjos);

        DatosOjos datos = JsonUtility.FromJson<DatosOjos>(perfil.datosOjos.text);
        contenedor = BuscarOCrear("Ojos Calaveras", raiz);
        contenedor.gameObject.SetActive(true);
        contenedor.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        contenedor.localScale = Vector3.one;
        luces = BuscarOCrear("Ojos Calaveras (luces)", raiz);
        luces.gameObject.SetActive(perfil.lucesPilares);

        List<Vector3> vertices = new List<Vector3>();
        List<Color> colores = new List<Color>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangulos = new List<int>();
        int indiceLuz = 0;

        for (int o = 0; o < datos.objetos.Length; o++)
        {
            ObjetoOjos objeto = datos.objetos[o];
            Transform dueno = BuscarRenderer(objeto.nombre, raiz);
            if (dueno == null)
            {
                Debug.LogWarning("No se encontro '" + objeto.nombre + "' para los ojos de calaveras.");
                continue;
            }

            float escala = Mathf.Abs(dueno.lossyScale.x);
            for (int i = 0; i < objeto.ojos.Length; i++)
            {
                Ojo ojo = objeto.ojos[i];
                Vector3 centro = dueno.TransformPoint(new Vector3(ojo.p[0], ojo.p[1], ojo.p[2]));
                Vector3 normal = dueno.TransformDirection(new Vector3(ojo.n[0], ojo.n[1], ojo.n[2])).normalized;
                Vector3 arriba = Vector3.ProjectOnPlane(Vector3.up, normal);
                if (arriba.sqrMagnitude < 0.01f)
                    arriba = Vector3.ProjectOnPlane(Vector3.forward, normal);
                arriba.Normalize();
                Vector3 derecha = Vector3.Cross(arriba, -normal).normalized;
                float medio = ojo.t * escala * perfil.tamanoOjos * 0.5f;
                float intensidad = Mathf.Lerp(0.65f, 1f, Mathf.Repeat(ojo.f * 7.31f, 1f));
                Color color = new Color(intensidad, intensidad, intensidad, ojo.f);

                int baseIndice = vertices.Count;
                vertices.Add(centro - derecha * medio - arriba * medio);
                vertices.Add(centro - derecha * medio + arriba * medio);
                vertices.Add(centro + derecha * medio + arriba * medio);
                vertices.Add(centro + derecha * medio - arriba * medio);
                colores.Add(color);
                colores.Add(color);
                colores.Add(color);
                colores.Add(color);
                uvs.Add(new Vector2(0f, 0f));
                uvs.Add(new Vector2(0f, 1f));
                uvs.Add(new Vector2(1f, 1f));
                uvs.Add(new Vector2(1f, 0f));
                triangulos.AddRange(new[] { baseIndice, baseIndice + 1, baseIndice + 2, baseIndice, baseIndice + 2, baseIndice + 3 });
            }

            for (int i = 0; i < objeto.pilares.Length; i++)
            {
                PilarOjos pilar = objeto.pilares[i];
                Vector3 centro = dueno.TransformPoint(new Vector3(pilar.c[0], pilar.c[1], pilar.c[2]));
                Vector3 haciaCentro = limites.center - centro;
                haciaCentro.y = 0f;
                Vector3 posicion = centro + haciaCentro.normalized * (pilar.r * escala + 0.7f);

                indiceLuz++;
                Transform hijo = BuscarOCrear("Luz Pilar " + indiceLuz, luces);
                hijo.position = posicion;
                Light luz = hijo.GetComponent<Light>();
                if (luz == null)
                    luz = hijo.gameObject.AddComponent<Light>();
                luz.type = LightType.Point;
                luz.color = perfil.colorLucesPilares;
                luz.intensity = perfil.intensidadLucesPilares;
                luz.range = perfil.rangoLucesPilares;
                luz.bounceIntensity = 0.5f;
                luz.lightmapBakeType = LightmapBakeType.Baked;
                luz.shadows = LightShadows.Soft;
                luz.shadowRadius = 0.2f;
                EditorUtility.SetDirty(luz);
            }
        }

        for (int i = luces.childCount - 1; i >= indiceLuz; i--)
            Object.DestroyImmediate(luces.GetChild(i).gameObject);

        string ruta = carpeta + "/Malla_Ojos_" + perfil.name.Replace("Perfil_", "") + ".asset";
        Mesh malla = AssetDatabase.LoadAssetAtPath<Mesh>(ruta);
        bool nueva = malla == null;
        if (nueva)
            malla = new Mesh { name = "Ojos Calaveras" };
        malla.Clear();
        malla.indexFormat = vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        malla.SetVertices(vertices);
        malla.SetColors(colores);
        malla.SetUVs(0, uvs);
        malla.SetTriangles(triangulos, 0);
        malla.RecalculateBounds();
        if (nueva)
            AssetDatabase.CreateAsset(malla, ruta);
        else
            EditorUtility.SetDirty(malla);

        MeshFilter filtro = contenedor.GetComponent<MeshFilter>();
        if (filtro == null)
            filtro = contenedor.gameObject.AddComponent<MeshFilter>();
        filtro.sharedMesh = malla;

        MeshRenderer render = contenedor.GetComponent<MeshRenderer>();
        if (render == null)
            render = contenedor.gameObject.AddComponent<MeshRenderer>();
        render.sharedMaterial = perfil.materialOjos;
        render.shadowCastingMode = ShadowCastingMode.Off;
        render.receiveShadows = false;
        render.lightProbeUsage = LightProbeUsage.Off;
        render.reflectionProbeUsage = ReflectionProbeUsage.Off;
        GameObjectUtility.SetStaticEditorFlags(contenedor.gameObject, 0);
        EditorUtility.SetDirty(render);
    }

    static Transform BuscarRenderer(string nombre, Transform raiz)
    {
        Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i].name == nombre && !renderers[i].transform.IsChildOf(raiz))
                return renderers[i].transform;
        }

        return null;
    }

    static Bounds Limites(PerfilAmbientacion perfil, Transform raiz)
    {
        Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        bool encontrado = false;
        Bounds limites = new Bounds();

        for (int pasada = 0; pasada < 2 && !encontrado; pasada++)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer.transform.IsChildOf(raiz) || renderer is ParticleSystemRenderer)
                    continue;

                if (pasada == 0 && !CoincidePrefijo(renderer.gameObject.name, perfil.prefijosLimite))
                    continue;

                if (pasada == 1 && !GameObjectUtility.AreStaticEditorFlagsSet(renderer.gameObject, StaticEditorFlags.ContributeGI))
                    continue;

                if (!encontrado)
                {
                    limites = renderer.bounds;
                    encontrado = true;
                }
                else
                {
                    limites.Encapsulate(renderer.bounds);
                }
            }
        }

        if (!encontrado)
            limites = new Bounds(Vector3.zero, new Vector3(30f, 4f, 30f));

        return limites;
    }

    static float AlturaSuelo(Bounds limites)
    {
        Vector3 origen = new Vector3(limites.center.x, limites.max.y + 50f, limites.center.z);
        RaycastHit[] golpes = Physics.RaycastAll(origen, Vector3.down, 200f, ~0, QueryTriggerInteraction.Ignore);

        float suelo = float.MaxValue;
        for (int i = 0; i < golpes.Length; i++)
            suelo = Mathf.Min(suelo, golpes[i].point.y);

        return suelo == float.MaxValue ? limites.min.y : suelo;
    }

    static bool CoincidePrefijo(string nombre, string[] prefijos)
    {
        if (prefijos == null)
            return false;

        for (int i = 0; i < prefijos.Length; i++)
        {
            if (!string.IsNullOrEmpty(prefijos[i]) && nombre.StartsWith(prefijos[i], System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    static Transform BuscarOCrear(string nombre, Transform padre)
    {
        if (padre != null)
        {
            Transform hijo = padre.Find(nombre);
            if (hijo != null)
                return hijo;
        }
        else
        {
            GameObject[] raices = SceneManager.GetActiveScene().GetRootGameObjects();
            for (int i = 0; i < raices.Length; i++)
            {
                if (raices[i].name == nombre)
                    return raices[i].transform;
            }
        }

        GameObject nuevo = new GameObject(nombre);
        if (padre != null)
            nuevo.transform.SetParent(padre, false);

        return nuevo.transform;
    }
}

public class AjustesVolumen
{
    public float exposicion = 0.25f;
    public float contraste = 12f;
    public float saturacion = 6f;
    public Color filtro = new Color(0.93f, 0.96f, 1f);
    public float umbralBloom = 0.95f;
    public float intensidadBloom = 0.9f;
    public Color tinteBloom = Color.white;
    public Color sombras = new Color(0.32f, 0.38f, 0.6f);
    public Color luces = new Color(0.7f, 0.6f, 0.66f);
    public float balance = -15f;
    public Color colorVineta = new Color(0.01f, 0.01f, 0.04f);
    public float intensidadVineta = 0.3f;
    public float grano;
}
