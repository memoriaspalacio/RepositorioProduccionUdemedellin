using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Herramientas del menu de graficos:
/// - Crea los niveles Medio, Bajo y Muy bajo en Quality Settings (copias del nivel PC con su
///   propio asset de URP). Se ejecuta solo al abrir el proyecto si no existe ninguno, por
///   ejemplo si un Quality Settings viejo los pisa al fusionar ramas.
/// - Limita las texturas 3D a 1024 en Android e iOS, para builds moviles mas livianas.
/// - Atajos para probar el menu en Play y para borrar los ajustes guardados.
/// </summary>
[InitializeOnLoad]
public static class ConfiguradorGraficos
{
    const string RutaQuality = "ProjectSettings/QualitySettings.asset";
    const string ClaveAjustes = "Graficos.Ajustes";
    const int TamanoMovil = 1024;

    /// <summary>Lo que cada nivel cambia respecto al nivel PC. Debe coincidir con los assets de URP.</summary>
    class Nivel
    {
        public string nombre;
        public string asset;
        public int sombras = 2;
        public int cascadas = 2;
        public float distanciaSombras;
        public int modoShadowmask;
        public int huesos;
        public int mipmaps;
        public int anisotropico;
        public float sesgoLod;
        public bool fundidoLod;
        public int rayosParticulas;
    }

    static readonly Nivel[] Niveles =
    {
        new Nivel { nombre = "Medio", asset = "Assets/Settings/Graficos_Medio_RPAsset.asset", distanciaSombras = 30f, modoShadowmask = 1, huesos = 4, mipmaps = 0, anisotropico = 1, sesgoLod = 1.5f, fundidoLod = true, rayosParticulas = 128 },
        new Nivel { nombre = "Bajo", asset = "Assets/Settings/Graficos_Bajo_RPAsset.asset", cascadas = 1, distanciaSombras = 20f, modoShadowmask = 0, huesos = 2, mipmaps = 1, anisotropico = 0, sesgoLod = 1f, fundidoLod = false, rayosParticulas = 64 },
        new Nivel { nombre = "Muy bajo", asset = "Assets/Settings/Graficos_MuyBajo_RPAsset.asset", sombras = 0, cascadas = 1, distanciaSombras = 20f, modoShadowmask = 0, huesos = 2, mipmaps = 2, anisotropico = 0, sesgoLod = 0.7f, fundidoLod = false, rayosParticulas = 16 },
    };

    static ConfiguradorGraficos()
    {
        EditorApplication.delayCall += RevisarAlAbrir;
    }

    static void RevisarAlAbrir()
    {
        List<string> nombres = new List<string>(QualitySettings.names);
        int existentes = 0;
        for (int i = 0; i < Niveles.Length; i++)
        {
            if (nombres.Contains(Niveles[i].nombre))
                existentes++;
        }

        if (existentes == 0)
            ConfigurarNiveles(false);
        else if (existentes < Niveles.Length)
            Debug.LogWarning("Graficos: faltan niveles de calidad. Herramientas > Graficos > Configurar niveles de calidad los vuelve a crear.");
    }

    #region Niveles

    [MenuItem("Herramientas/Graficos/Configurar niveles de calidad", priority = 30)]
    static void ConfigurarNivelesMenu()
    {
        string resultado = ConfigurarNiveles(true);
        EditorUtility.DisplayDialog("Niveles de calidad", resultado, "OK");
    }

    /// <summary>Crea los niveles que falten y deja el nivel PC disponible tambien en moviles.</summary>
    static string ConfigurarNiveles(bool desdeMenu)
    {
        Object[] objetos = AssetDatabase.LoadAllAssetsAtPath(RutaQuality);
        if (objetos == null || objetos.Length == 0)
            return "No se pudo abrir " + RutaQuality;

        SerializedObject quality = new SerializedObject(objetos[0]);
        SerializedProperty lista = quality.FindProperty("m_QualitySettings");

        int indicePc = BuscarNivel(lista, "PC");
        if (indicePc < 0)
            return "No existe el nivel PC, del que se copian los demas.";

        List<string> creados = new List<string>();

        // Alto es el nivel PC: tambien debe poder elegirse en moviles potentes
        SerializedProperty excluidas = lista.GetArrayElementAtIndex(indicePc).FindPropertyRelative("excludedTargetPlatforms");
        bool pcEnMoviles = excluidas.arraySize == 0;
        excluidas.ClearArray();

        for (int i = 0; i < Niveles.Length; i++)
        {
            Nivel nivel = Niveles[i];
            RenderPipelineAsset pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(nivel.asset);
            if (pipeline == null)
                return "No se encontro " + nivel.asset;

            int indice = BuscarNivel(lista, nivel.nombre);
            if (indice < 0)
            {
                // Copia del nivel PC al final de la lista (no cambia los indices de los demas)
                lista.InsertArrayElementAtIndex(indicePc);
                lista.MoveArrayElement(indicePc + 1, lista.arraySize - 1);
                indice = lista.arraySize - 1;
                Copiar(lista.GetArrayElementAtIndex(indice), nivel);
                creados.Add(nivel.nombre);
            }

            lista.GetArrayElementAtIndex(indice).FindPropertyRelative("customRenderPipeline").objectReferenceValue = pipeline;
        }

        quality.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();

        string mensaje = creados.Count > 0
            ? "Graficos: creados los niveles de calidad " + string.Join(", ", creados) + "."
            : "Graficos: los niveles de calidad ya estaban configurados.";
        if (!pcEnMoviles)
            mensaje += " El nivel PC (Alto) ahora tambien esta disponible en Android e iOS.";

        if (creados.Count > 0 || !pcEnMoviles || desdeMenu)
            Debug.Log(mensaje);

        return mensaje;
    }

    static void Copiar(SerializedProperty destino, Nivel nivel)
    {
        destino.FindPropertyRelative("name").stringValue = nivel.nombre;
        destino.FindPropertyRelative("shadows").intValue = nivel.sombras;
        destino.FindPropertyRelative("shadowCascades").intValue = nivel.cascadas;
        destino.FindPropertyRelative("shadowDistance").floatValue = nivel.distanciaSombras;
        destino.FindPropertyRelative("shadowmaskMode").intValue = nivel.modoShadowmask;
        destino.FindPropertyRelative("skinWeights").intValue = nivel.huesos;
        destino.FindPropertyRelative("globalTextureMipmapLimit").intValue = nivel.mipmaps;
        destino.FindPropertyRelative("anisotropicTextures").intValue = nivel.anisotropico;
        destino.FindPropertyRelative("lodBias").floatValue = nivel.sesgoLod;
        destino.FindPropertyRelative("enableLODCrossFade").boolValue = nivel.fundidoLod;
        destino.FindPropertyRelative("particleRaycastBudget").intValue = nivel.rayosParticulas;
        destino.FindPropertyRelative("excludedTargetPlatforms").ClearArray();
    }

    static int BuscarNivel(SerializedProperty lista, string nombre)
    {
        for (int i = 0; i < lista.arraySize; i++)
        {
            if (lista.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == nombre)
                return i;
        }

        return -1;
    }

    #endregion

    #region Texturas moviles

    [MenuItem("Herramientas/Graficos/Texturas 3D: limitar a 1024 en moviles", priority = 50)]
    static void LimitarTexturasMoviles()
    {
        List<TextureImporter> texturas = TexturasMoviles(true);
        if (texturas.Count == 0)
        {
            EditorUtility.DisplayDialog("Texturas moviles", "No hay texturas 3D por encima de " + TamanoMovil + " sin limitar.", "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog("Texturas moviles",
                texturas.Count + " texturas de escenarios y personajes pasaran a un maximo de " + TamanoMovil + " px en Android e iOS.\n\n" +
                "PC no cambia. La UI, los sprites y los lightmaps no se tocan.", "Limitar", "Cancelar"))
            return;

        AplicarLimite(texturas, true);
    }

    [MenuItem("Herramientas/Graficos/Texturas 3D: quitar el limite en moviles", priority = 51)]
    static void QuitarLimiteTexturasMoviles()
    {
        List<TextureImporter> texturas = TexturasMoviles(false);
        if (texturas.Count == 0)
        {
            EditorUtility.DisplayDialog("Texturas moviles", "Ninguna textura tiene el limite para moviles.", "OK");
            return;
        }

        AplicarLimite(texturas, false);
    }

    /// <summary>
    /// Texturas 3D (tipo Default, con mipmaps) del juego actual de mas de 1024 px. Con sinLimitar
    /// devuelve las que aun no tienen el limite; si no, las que ya lo tienen.
    /// </summary>
    static List<TextureImporter> TexturasMoviles(bool sinLimitar)
    {
        List<TextureImporter> resultado = new List<TextureImporter>();
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/_Juego Actual/Arte" });

        for (int i = 0; i < guids.Length; i++)
        {
            TextureImporter importador = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guids[i])) as TextureImporter;
            if (importador == null || importador.textureType != TextureImporterType.Default || !importador.mipmapEnabled)
                continue;

            if (importador.maxTextureSize <= TamanoMovil)
                continue;

            TextureImporterPlatformSettings android = importador.GetPlatformTextureSettings("Android");
            bool limitada = android.overridden && android.maxTextureSize <= TamanoMovil;
            if (limitada != sinLimitar)
                resultado.Add(importador);
        }

        return resultado;
    }

    static void AplicarLimite(List<TextureImporter> texturas, bool limitar)
    {
        try
        {
            AssetDatabase.StartAssetEditing();
            for (int i = 0; i < texturas.Count; i++)
            {
                TextureImporter importador = texturas[i];
                EditorUtility.DisplayProgressBar("Texturas moviles", importador.assetPath, i / (float)texturas.Count);

                foreach (string plataforma in new[] { "Android", "iPhone" })
                {
                    TextureImporterPlatformSettings ajustes = importador.GetPlatformTextureSettings(plataforma);
                    ajustes.overridden = limitar;
                    ajustes.maxTextureSize = limitar ? TamanoMovil : importador.maxTextureSize;
                    ajustes.format = TextureImporterFormat.Automatic;
                    importador.SetPlatformTextureSettings(ajustes);
                }

                importador.SaveAndReimport();
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            EditorUtility.ClearProgressBar();
        }

        Debug.Log("Graficos: " + texturas.Count + (limitar ? " texturas limitadas a " + TamanoMovil + " en moviles." : " texturas sin limite en moviles."));
    }

    #endregion

    #region Pruebas

    [MenuItem("Herramientas/Graficos/Abrir menu de graficos (Play)", priority = 10)]
    static void AbrirMenu()
    {
        MenuGraficos.Alternar();
    }

    [MenuItem("Herramientas/Graficos/Abrir menu de graficos (Play)", true)]
    static bool AbrirMenuValido()
    {
        return Application.isPlaying;
    }

    [MenuItem("Herramientas/Graficos/Volver a calidad Alta (borrar ajustes guardados)", priority = 11)]
    static void BorrarAjustes()
    {
        // Los ajustes del menu quedan en PlayerPrefs entre sesiones de Play
        PlayerPrefs.DeleteKey(ClaveAjustes);
        PlayerPrefs.Save();

        if (Application.isPlaying)
            GestorGraficos.UsarNivel(NivelGrafico.Alto);

        Debug.Log("Graficos: ajustes borrados; el editor vuelve a la calidad Alta (el aspecto original).");
    }

    #endregion
}
