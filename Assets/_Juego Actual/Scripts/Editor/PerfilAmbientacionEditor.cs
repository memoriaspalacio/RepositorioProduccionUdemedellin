using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PerfilAmbientacion))]
public class PerfilAmbientacionEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        PerfilAmbientacion perfil = (PerfilAmbientacion)target;
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Aplicar reconfigura luna, niebla, luces de acento, particulas, sondas y horneado con los valores de este perfil. " +
            "El skybox y el perfil de volumen se editan directamente en sus assets.",
            MessageType.Info);

        if (GUILayout.Button("Aplicar a la escena", GUILayout.Height(28)))
        {
            AplicadorAmbientacion.Aplicar(perfil);
            GUIUtility.ExitGUI();
        }

        using (new EditorGUI.DisabledScope(Lightmapping.isRunning))
        {
            if (GUILayout.Button("Aplicar y hornear luces", GUILayout.Height(28)))
            {
                if (AplicadorAmbientacion.Aplicar(perfil))
                    AplicadorAmbientacion.Hornear();
                GUIUtility.ExitGUI();
            }
        }
    }
}
