using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace UdeM.Portafolio
{
    /// <summary>
    /// Builds de Windows del juego y del portafolio, con listas de escenas explicitas: no cambia
    /// Build Settings ni los ajustes del proyecto.
    ///   - Juego: las escenas activas de Build Settings, como siempre.
    ///   - Portafolio: el menu primero y luego todas las vitrinas de Portafolio/Escenas.
    /// Salen en la carpeta Builds junto al repositorio, cada una en su carpeta con la fecha.
    /// </summary>
    public static class ConstructorBuilds
    {
        const string CarpetaEscenas = "Assets/_Juego Actual/Portafolio/Escenas";

        static string CarpetaBuilds => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Builds"));
        static string Fecha => DateTime.Now.ToString("yyyy-MM-dd");

        [MenuItem("Portafolio/Builds/Build del juego (Windows)", priority = 80)]
        public static void BuildJuego()
        {
            string[] escenas = EditorBuildSettings.scenes.Where(e => e.enabled).Select(e => e.path).ToArray();
            // -carpeta Nombre para sacarla en otra carpeta (por ejemplo una version corregida del mismo dia).
            string carpeta = Argumento("-carpeta") ?? "BeforeTheDawn_PC_" + Fecha;
            Construir(escenas, Path.Combine(CarpetaBuilds, carpeta), "Before The Dawn.exe");
        }

        [MenuItem("Portafolio/Builds/Build del portafolio (Windows)", priority = 81)]
        public static void BuildPortafolio()
        {
            string[] vitrinas = Directory.GetFiles(CarpetaEscenas, "*.unity")
                .Select(r => r.Replace('\\', '/'))
                .Where(r => r != CargadorEscenas.RutaMenu)
                .OrderBy(r => r)
                .ToArray();
            string[] escenas = new[] { CargadorEscenas.RutaMenu }.Concat(vitrinas).ToArray();
            Construir(escenas, Path.Combine(CarpetaBuilds, "Portafolio_ArteTecnico_PC_" + Fecha), "Portafolio Arte Tecnico.exe");
        }

        static string Argumento(string nombre)
        {
            string[] argumentos = Environment.GetCommandLineArgs();
            int indice = Array.IndexOf(argumentos, nombre);
            return indice >= 0 && indice + 1 < argumentos.Length ? argumentos[indice + 1] : null;
        }

        static void Construir(string[] escenas, string carpeta, string ejecutable)
        {
            Directory.CreateDirectory(carpeta);
            BuildPlayerOptions opciones = new BuildPlayerOptions
            {
                scenes = escenas,
                locationPathName = Path.Combine(carpeta, ejecutable),
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None
            };

            Debug.Log($"ConstructorBuilds: {escenas.Length} escenas -> {opciones.locationPathName}\n  " + string.Join("\n  ", escenas));
            BuildReport reporte = BuildPipeline.BuildPlayer(opciones);
            BuildSummary resumen = reporte.summary;
            Debug.Log($"ConstructorBuilds: {resumen.result}, {resumen.totalSize / (1024f * 1024f):0} MB, {resumen.totalErrors} errores, {resumen.totalTime}.");

            if (Application.isBatchMode && resumen.result != BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }
    }
}
