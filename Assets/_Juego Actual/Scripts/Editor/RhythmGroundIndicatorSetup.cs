using UnityEditor;
using UnityEngine;

/// <summary>
/// Menús y herramientas de ayuda en el editor de Unity para configurar el indicador de suelo rítmico.
/// </summary>
public static class RhythmGroundIndicatorSetup
{
    [MenuItem("Tools/Ritmo/Añadir Indicador de Suelo al Personaje", false, 1)]
    public static void AddIndicatorToPlayer()
    {
        // 1. Intentar obtener el personaje seleccionado o buscarlo en la escena
        GameObject target = Selection.activeGameObject;

        if (target == null || target.GetComponent<GridPlayerMovement>() == null)
        {
            GridPlayerMovement movement = Object.FindFirstObjectByType<GridPlayerMovement>();
            if (movement != null)
            {
                target = movement.gameObject;
            }
            else
            {
                GameObject playerByTag = GameObject.FindWithTag("Player");
                if (playerByTag != null)
                    target = playerByTag;
            }
        }

        if (target == null)
        {
            EditorUtility.DisplayDialog("Indicador Rítmico", 
                "No se encontró un personaje con 'GridPlayerMovement' o tag 'Player' en la escena activa. Selecciona el personaje en la jerarquía y vuelve a pulsar esta opción.", 
                "Entendido");
            return;
        }

        // 2. Si ya hay un indicador en la escena, reutilizarlo en vez de crear otro.
        // Dos indicadores dibujan sus anillos superpuestos y el resultado es ilegible.
        RhythmGroundIndicator indicator = target.GetComponent<RhythmGroundIndicator>();

        if (indicator == null)
        {
            RhythmGroundIndicator existing = Object.FindFirstObjectByType<RhythmGroundIndicator>();

            if (existing != null)
            {
                EditorUtility.DisplayDialog("Indicador Rítmico",
                    $"Ya existe un indicador en la escena, sobre '{existing.gameObject.name}'.\n\n" +
                    "Se seleccionará ese en vez de añadir uno nuevo: dos indicadores superponen " +
                    "sus anillos y el medidor se vuelve ilegible.",
                    "Entendido");

                Selection.activeGameObject = existing.gameObject;
                return;
            }

            indicator = Undo.AddComponent<RhythmGroundIndicator>(target);
        }

        // 3. Cargar el material preconfigurado
        Material mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Juego Actual/Arte/Materials/M_RhythmCircleIndicator.mat");
        if (mat != null)
        {
            SerializedObject so = new SerializedObject(indicator);
            SerializedProperty matProp = so.FindProperty("indicatorMaterial");
            if (matProp != null)
            {
                matProp.objectReferenceValue = mat;
                so.ApplyModifiedProperties();
            }
        }

        indicator.EnsureVisualQuad();
        EditorUtility.SetDirty(target);
        Selection.activeGameObject = target;

        Debug.Log($"<color=#38bdf8><b>[Ritmo]</b></color> Indicador de suelo rítmico configurado exitosamente en: <b>{target.name}</b>", target);
    }

    [MenuItem("GameObject/Ritmo/Crear Indicador de Suelo", false, 10)]
    public static void CreateStandaloneIndicator(MenuCommand menuCommand)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Juego Actual/Prefabs/RhythmGroundIndicator.prefab");
        GameObject instance;

        if (prefab != null)
        {
            instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(instance, "Crear RhythmGroundIndicator");
        }
        else
        {
            instance = new GameObject("RhythmGroundIndicator", typeof(RhythmGroundIndicator));
            Undo.RegisterCreatedObjectUndo(instance, "Crear RhythmGroundIndicator");
        }

        GameObjectUtility.SetParentAndAlign(instance, menuCommand.context as GameObject);
        Selection.activeObject = instance;
    }
}
