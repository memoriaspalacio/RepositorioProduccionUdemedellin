using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Banco de pruebas de rendimiento: recorre los niveles del juego con cada calidad, mide los
/// tiempos de fotograma y guarda un CSV y una captura por combinacion. Solo se activa al abrir
/// una build con el argumento -bancoGraficos, por ejemplo:
///
///   Juego.exe -bancoGraficos "C:\Resultados" -screen-fullscreen 0 -screen-width 1600 -screen-height 900
///
/// Sirve para comparar equipos con datos en vez de impresiones. Mide sin limite de FPS y al
/// terminar devuelve los ajustes del jugador y cierra el juego.
/// </summary>
public class BancoGraficos : MonoBehaviour
{
    const string Argumento = "-bancoGraficos";
    const float Calentamiento = 1.5f;
    const float Medicion = 4f;

    static readonly string[] Escenas = { "cementerio", "iglesia", "mazmorra", "crisantia" };

    string carpeta;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Iniciar()
    {
        string[] argumentos = Environment.GetCommandLineArgs();
        int indice = Array.IndexOf(argumentos, Argumento);
        if (indice < 0)
            return;

        bool hayCarpeta = indice + 1 < argumentos.Length && !argumentos[indice + 1].StartsWith("-");
        GameObject objeto = new GameObject("Banco Graficos");
        DontDestroyOnLoad(objeto);
        BancoGraficos banco = objeto.AddComponent<BancoGraficos>();
        banco.carpeta = hayCarpeta ? argumentos[indice + 1] : Path.Combine(Application.persistentDataPath, "BancoGraficos");
        Directory.CreateDirectory(banco.carpeta);
    }

    IEnumerator Start()
    {
        AjustesGraficos delJugador = GestorGraficos.Ajustes;
        StringBuilder csv = new StringBuilder("escena,calidad,nivel_unity,fps_medio,ms_medio,ms_p50,ms_p95,ms_peor,resolucion_3d,gpu\n");
        Debug.Log($"BancoGraficos: {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType}), pantalla {Screen.width}x{Screen.height}");

        foreach (string escena in Escenas)
        {
            if (!Application.CanStreamedLevelBeLoaded(escena))
                continue;

            SceneManager.LoadScene(escena);
            yield return new WaitForSecondsRealtime(3f);

            foreach (NivelGrafico nivel in Enum.GetValues(typeof(NivelGrafico)))
            {
                AjustesGraficos ajustes = AjustesGraficos.DePreset(nivel);
                ajustes.fps = LimiteFps.SinLimite;
                ajustes.mostrarFps = true;
                GestorGraficos.Aplicar(ajustes);
                QualitySettings.vSyncCount = 0;

                yield return new WaitForSecondsRealtime(Calentamiento);

                List<float> tiempos = new List<float>();
                float total = 0f;
                while (total < Medicion)
                {
                    yield return null;
                    tiempos.Add(Time.unscaledDeltaTime * 1000f);
                    total += Time.unscaledDeltaTime;
                }

                string linea = Linea(escena, nivel, ajustes, tiempos);
                csv.AppendLine(linea);
                Debug.Log("BancoGraficos: " + linea);

                yield return Capturar(escena + "_" + nivel);
            }
        }

        File.WriteAllText(Path.Combine(carpeta, "resultados.csv"), csv.ToString());
        Debug.Log("BancoGraficos: resultados en " + carpeta);

        GestorGraficos.Aplicar(delJugador);
        Application.Quit();
    }

    static string Linea(string escena, NivelGrafico nivel, AjustesGraficos ajustes, List<float> tiempos)
    {
        tiempos.Sort();
        float suma = 0f;
        for (int i = 0; i < tiempos.Count; i++)
            suma += tiempos[i];
        float media = suma / tiempos.Count;

        int ancho = Mathf.RoundToInt(Screen.width * ajustes.FactorResolucion);
        int alto = Mathf.RoundToInt(Screen.height * ajustes.FactorResolucion);

        return string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3:0.0},{4:0.00},{5:0.00},{6:0.00},{7:0.00},{8}x{9},{10}",
            escena, nivel, GestorGraficos.NombreNivelActivo, 1000f / media, media,
            tiempos[tiempos.Count / 2], tiempos[(int)(tiempos.Count * 0.95f)], tiempos[tiempos.Count - 1],
            ancho, alto, SystemInfo.graphicsDeviceName.Replace(",", " "));
    }

    IEnumerator Capturar(string nombre)
    {
        yield return new WaitForEndOfFrame();
        Texture2D captura = ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(Path.Combine(carpeta, nombre + ".jpg"), captura.EncodeToJPG(85));
        Destroy(captura);
    }
}
