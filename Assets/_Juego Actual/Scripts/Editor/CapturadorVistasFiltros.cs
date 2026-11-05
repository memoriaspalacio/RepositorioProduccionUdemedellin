using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Guarda una captura de la camara principal de cada nivel, sin interfaz, en
/// Resources/VistasFiltros. El menu de filtros las usa como vista previa fuera de los niveles
/// (menu principal); dentro de un nivel usa la camara en vivo. Vuelve a ejecutarlo si cambia
/// la iluminacion de algun nivel.
/// </summary>
public static class CapturadorVistasFiltros
{
    const string Carpeta = "Assets/_Juego Actual/Resources/VistasFiltros";
    const int Ancho = 1280;
    const int Alto = 720;

    static readonly string[] Escenas =
    {
        "Assets/_Juego Actual/Scenes/_ProperGameScenes/cementerio.unity",
        "Assets/_Juego Actual/Scenes/_ProperGameScenes/iglesia.unity",
        "Assets/_Juego Actual/Scenes/_ProperGameScenes/crisantia.unity",
        "Assets/_Juego Actual/Scenes/_ProperGameScenes/mazmorra.unity"
    };

    [MenuItem("Herramientas/Graficos/Capturar vistas previas de filtros")]
    static void Capturar()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        string original = SceneManager.GetActiveScene().path;
        AplicadorAmbientacion.CrearCarpeta(Carpeta);

        int hechas = 0;
        for (int i = 0; i < Escenas.Length; i++)
        {
            EditorUtility.DisplayProgressBar("Vistas previas de filtros", Path.GetFileNameWithoutExtension(Escenas[i]), i / (float)Escenas.Length);
            EditorSceneManager.OpenScene(Escenas[i], OpenSceneMode.Single);

            Camera camara = Camera.main;
            if (camara == null)
            {
                Debug.LogWarning("Sin camara principal en " + Escenas[i]);
                continue;
            }

            RenderTexture destino = new RenderTexture(Ancho, Alto, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            destino.antiAliasing = 4;
            GestorFiltros.CapturarSinFiltro(camara, destino);

            RenderTexture resuelta = RenderTexture.GetTemporary(Ancho, Alto, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(destino, resuelta);

            RenderTexture anterior = RenderTexture.active;
            RenderTexture.active = resuelta;
            Texture2D imagen = new Texture2D(Ancho, Alto, TextureFormat.RGB24, false, false);
            imagen.ReadPixels(new Rect(0, 0, Ancho, Alto), 0, 0);
            imagen.Apply();
            RenderTexture.active = anterior;

            string ruta = Carpeta + "/" + Path.GetFileNameWithoutExtension(Escenas[i]) + ".png";
            File.WriteAllBytes(ruta, imagen.EncodeToPNG());

            Object.DestroyImmediate(imagen);
            RenderTexture.ReleaseTemporary(resuelta);
            destino.Release();
            Object.DestroyImmediate(destino);
            hechas++;
        }

        EditorUtility.ClearProgressBar();
        AssetDatabase.Refresh();

        if (!string.IsNullOrEmpty(original))
            EditorSceneManager.OpenScene(original, OpenSceneMode.Single);

        Debug.Log("Vistas previas de filtros: " + hechas + " capturas en " + Carpeta + ".");
    }
}
