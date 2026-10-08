using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class SetupIglesiaLighting
{
    const string ScenePath =
        "Assets/_Juego Actual/Scenes/_ProperGameScenes/iglesia.unity";

    const string MaterialPath =
        "Assets/_Juego Actual/Arte/Materials/Ventanas.mat";

    const string SessionKey = "IglesiaLightingDimmed";

    static SetupIglesiaLighting()
    {
        EditorApplication.delayCall += TrySetup;
    }

    static void TrySetup()
    {
        if (SessionState.GetBool(SessionKey, false))
            return;

        Scene active = SceneManager.GetActiveScene();
        if (active.path != ScenePath)
            return;

        SessionState.SetBool(SessionKey, true);
        Apply();
    }

    public static void Apply()
    {
        EnableWindowEmission();
        DisableSmallLights();
        CreateBakedLights();
        DimExistingLights();
        CreateProbeGrid();

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();

        Debug.Log("Iluminacion de iglesia lista. Hornea la escena para verla fija.");
    }

    static void EnableWindowEmission()
    {
        Material windows = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (windows == null)
            return;

        windows.EnableKeyword("_EMISSION");
        windows.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        windows.SetColor("_EmissionColor", new Color(0.08f, 0.16f, 0.2f));
        EditorUtility.SetDirty(windows);
    }

    static void DisableSmallLights()
    {
        Light[] lights = Object.FindObjectsByType<Light>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < lights.Length; i++)
        {
            Light light = lights[i];
            if (light.type != LightType.Point && light.type != LightType.Spot)
                continue;

            if (light.gameObject.name.StartsWith("Luz "))
                continue;

            light.gameObject.SetActive(false);
        }

        for (int i = 0; i < lights.Length; i++)
        {
            Light light = lights[i];
            if (light.type != LightType.Directional)
                continue;

            light.lightmapBakeType = LightmapBakeType.Baked;
            light.intensity = 7f;
        }
    }

    static void CreateBakedLights()
    {
        Bounds windows = BoundsOf("Ventanal");
        Bounds altar = BoundsOf("Mesa");
        Bounds candles = BoundsOf("Vela");

        if (GameObject.Find("Luz Nave") == null && windows.size.sqrMagnitude > 0.01f)
        {
            GameObject nave = new GameObject("Luz Nave");
            Light light = nave.AddComponent<Light>();
            light.type = LightType.Rectangle;
            light.color = new Color(0.55f, 0.75f, 0.78f);
            light.intensity = 8f;
            light.bounceIntensity = 0.3f;
            light.shadows = LightShadows.None;
            light.lightmapBakeType = LightmapBakeType.Baked;
            light.areaSize = new Vector2(
                Mathf.Max(6f, windows.size.x * 0.7f),
                Mathf.Max(6f, windows.size.z * 0.7f));

            nave.transform.position = windows.center + Vector3.up * (windows.extents.y + 0.4f);
            nave.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        CreateWarmLight("Luz Altar", altar, 35f);
        CreateWarmLight("Luz Velas", candles, 20f);
    }

    static void DimExistingLights()
    {
        SetLight("Luz Nave", 8f, 0.3f);
        SetLight("Luz Altar", 35f, 0.3f);
        SetLight("Luz Velas", 20f, 0.3f);
    }

    static void SetLight(string name, float intensity, float bounce)
    {
        GameObject go = GameObject.Find(name);
        if (go == null)
            return;

        Light light = go.GetComponent<Light>();
        if (light == null)
            return;

        light.intensity = intensity;
        light.bounceIntensity = bounce;
    }

    static void CreateWarmLight(string name, Bounds bounds, float intensity)
    {
        if (bounds.size.sqrMagnitude <= 0.01f)
            return;

        if (GameObject.Find(name) != null)
            return;

        GameObject go = new GameObject(name);
        Light light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.55f, 0.22f);
        light.intensity = intensity;
        light.range = 6f;
        light.bounceIntensity = 0.3f;
        light.shadows = LightShadows.None;
        light.lightmapBakeType = LightmapBakeType.Baked;
        go.transform.position = bounds.center + Vector3.up * 1.2f;
    }

    static void CreateProbeGrid()
    {
        if (GameObject.Find("Probes Iglesia") != null)
            return;

        Bounds volume = BoundsOf("Ventanal");
        if (volume.size.sqrMagnitude <= 0.01f)
            volume = BoundsOf("Iglesia");

        if (volume.size.sqrMagnitude <= 0.01f)
            return;

        GameObject probes = new GameObject("Probes Iglesia");
        probes.transform.position = volume.center;
        LightProbeGroup group = probes.AddComponent<LightProbeGroup>();

        List<Vector3> positions = new List<Vector3>();
        Vector3 min = volume.min;
        Vector3 size = volume.size;

        for (int x = 0; x < 4; x++)
        {
            for (int y = 0; y < 3; y++)
            {
                for (int z = 0; z < 4; z++)
                {
                    Vector3 world = new Vector3(
                        min.x + size.x * x / 3f,
                        min.y + size.y * y / 2f,
                        min.z + size.z * z / 3f);

                    positions.Add(probes.transform.InverseTransformPoint(world));
                }
            }
        }

        group.probePositions = positions.ToArray();
    }

    static Bounds BoundsOf(string nameStartsWith)
    {
        bool found = false;
        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);

        Renderer[] renderers = Object.FindObjectsByType<Renderer>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (!renderer.gameObject.name.StartsWith(nameStartsWith))
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

        return bounds;
    }
}
