using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class SetupMazmorraAreaLight
{
    const string ScenePath =
        "Assets/_Juego Actual/Scenes/_ProperGameScenes/mazmorra.unity";

    const string LightName = "Luz Area Mazmorra";
    const string SessionKey = "MazmorraAreaLightPlaced";

    static readonly string[] EnvironmentNames =
    {
        "CrucesA", "CrucesB", "Entrada", "Entrada2",
        "Ladrillos1", "Ladrillos2", "Ladrillos3", "Ladrillos4", "Ladrillos5",
        "Piso", "Antorchas", "Antorchas2",
        "WallW1", "WallW2", "WallE1", "WallE2", "WallN", "WallS",
        "Arco", "Arco2", "Pilares"
    };

    static SetupMazmorraAreaLight()
    {
        EditorApplication.delayCall += TryPlace;
    }

    static void TryPlace()
    {
        if (SessionState.GetBool(SessionKey, false))
            return;

        Scene active = SceneManager.GetActiveScene();
        if (active.path != ScenePath)
            return;

        if (GameObject.Find(LightName) != null)
        {
            SessionState.SetBool(SessionKey, true);
            return;
        }

        Bounds volume = EnvironmentBounds();
        if (volume.size.sqrMagnitude <= 0.01f)
            return;

        SessionState.SetBool(SessionKey, true);

        Color fill = DirectionalColor();
        GameObject area = new GameObject(LightName);
        Light light = area.AddComponent<Light>();
        light.type = LightType.Rectangle;
        light.color = fill;
        light.intensity = 8f;
        light.bounceIntensity = 0.3f;
        light.shadows = LightShadows.None;
        light.lightmapBakeType = LightmapBakeType.Baked;
        light.areaSize = new Vector2(
            Mathf.Max(4f, volume.size.x * 0.85f),
            Mathf.Max(4f, volume.size.z * 0.85f));

        area.transform.position = new Vector3(
            volume.center.x,
            volume.max.y + 0.5f,
            volume.center.z);
        area.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        EditorSceneManager.MarkSceneDirty(active);
        EditorSceneManager.SaveScene(active);
        Debug.Log("Luz Area Mazmorra creada encima del mapa, en Baked, con el color de la Directional.");
    }

    static Color DirectionalColor()
    {
        Light[] lights = Object.FindObjectsByType<Light>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i].type == LightType.Directional)
                return lights[i].color;
        }

        return new Color(1f, 0.95686275f, 0.8392157f);
    }

    static Bounds EnvironmentBounds()
    {
        HashSet<string> names = new HashSet<string>(EnvironmentNames);
        bool found = false;
        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);

        Renderer[] renderers = Object.FindObjectsByType<Renderer>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (!names.Contains(renderer.gameObject.name))
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
