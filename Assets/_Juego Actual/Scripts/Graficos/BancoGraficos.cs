using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Banco de pruebas de una build: recorre los niveles con cada calidad, mide los tiempos de
/// fotograma y comprueba que nada falle. Solo se activa al abrir una build con -bancoGraficos:
///
///   Juego.exe -bancoGraficos "C:\Resultados" -screen-fullscreen 0 -screen-width 1600 -screen-height 900
///
/// Deja en la carpeta:
///   - resultados.csv: FPS y milisegundos de cada nivel con cada calidad (sin limite de FPS y con
///     el juego congelado, para que todas dibujen lo mismo), mas una captura de cada una.
///   - opciones.csv: cada valor de cada fila del menu de graficos aplicado uno a uno, con los
///     errores que haya soltado.
///   - errores.txt: todos los errores y excepciones del recorrido (vacio si no hubo).
///   - Capturas del menu de graficos abierto y del panel de opciones del menu principal.
/// Al terminar devuelve los ajustes del jugador y cierra el juego.
/// </summary>
public class BancoGraficos : MonoBehaviour
{
    const string Argumento = "-bancoGraficos";
    const float Calentamiento = 1.5f;
    const float Medicion = 4f;

    static readonly string[] Escenas = { "cementerio", "iglesia", "mazmorra", "crisantia" };
    const string EscenaMenu = "menu";

    string carpeta;
    readonly List<string> errores = new List<string>();

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

    void OnEnable()
    {
        Application.logMessageReceived += AlRegistrar;
    }

    void OnDisable()
    {
        Application.logMessageReceived -= AlRegistrar;
    }

    void AlRegistrar(string mensaje, string pila, LogType tipo)
    {
        if (tipo == LogType.Error || tipo == LogType.Exception || tipo == LogType.Assert)
            errores.Add($"[{SceneManager.GetActiveScene().name}] {tipo}: {mensaje}\n{pila}");
    }

    IEnumerator Start()
    {
        AjustesGraficos delJugador = GestorGraficos.Ajustes;
        Debug.Log($"BancoGraficos: {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType}), pantalla {Screen.width}x{Screen.height}");

        yield return ProbarMenuPrincipal();

        StringBuilder csv = new StringBuilder("escena,calidad,nivel_unity,fps_medio,ms_medio,ms_p50,ms_p95,ms_peor,resolucion_3d,gpu\n");
        StringBuilder csvOpciones = new StringBuilder("escena,opcion,valor,nivel_unity,errores\n");
        bool opcionesProbadas = false;

        foreach (string escena in Escenas)
        {
            if (!Application.CanStreamedLevelBeLoaded(escena))
            {
                errores.Add($"No se puede cargar la escena {escena}: no esta en la build.");
                continue;
            }

            yield return CargarCongelada(escena);

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

                    // La primera vez que se usa una variante de shader se compila y da un tiron
                    if (Time.unscaledDeltaTime > 0.2f)
                        continue;

                    tiempos.Add(Time.unscaledDeltaTime * 1000f);
                    total += Time.unscaledDeltaTime;
                }

                string linea = Linea(escena, nivel, ajustes, tiempos);
                csv.AppendLine(linea);
                Debug.Log("BancoGraficos: " + linea);

                yield return Capturar(escena + "_" + nivel);
            }

            // El menu de graficos abierto sobre el nivel
            GestorGraficos.UsarNivel(NivelGrafico.Alto);
            MenuGraficos.Abrir();
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Capturar(escena + "_menu_graficos");
            MenuGraficos.Cerrar();

            // Cada valor de cada fila, una vez (en el primer nivel que cargue)
            if (!opcionesProbadas)
            {
                opcionesProbadas = true;
                yield return ProbarOpciones(escena, csvOpciones);
            }
        }

        File.WriteAllText(Path.Combine(carpeta, "resultados.csv"), csv.ToString());
        File.WriteAllText(Path.Combine(carpeta, "opciones.csv"), csvOpciones.ToString());
        File.WriteAllText(Path.Combine(carpeta, "errores.txt"), string.Join("\n\n", errores));
        Debug.Log($"BancoGraficos: resultados en {carpeta} ({errores.Count} errores)");

        GestorGraficos.Aplicar(delJugador);
        Time.timeScale = 1f;
        Application.Quit();
    }

    IEnumerator CargarCongelada(string escena)
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(escena);
        yield return new WaitForSecondsRealtime(3f);

        // Con el juego congelado nadie muere y el cronometro (que corre en tiempo sin escalar
        // y sigue entre escenas) no acaba la partida a mitad de la medicion
        if (ControladorContrarreloj.Instancia != null)
            ControladorContrarreloj.Instancia.PausarContrarreloj();
        Time.timeScale = 0f;
    }

    /// <summary>La fila "Graficos" del panel de opciones del menu principal y el menu que abre.</summary>
    IEnumerator ProbarMenuPrincipal()
    {
        if (!Application.CanStreamedLevelBeLoaded(EscenaMenu))
            yield break;

        SceneManager.LoadScene(EscenaMenu);
        yield return new WaitForSecondsRealtime(2f);

        // La cinematica de inicio tapa el menu: se salta como si el jugador pulsara una tecla
        foreach (ControladorCinematica cinematica in FindObjectsByType<ControladorCinematica>(FindObjectsSortMode.None))
            cinematica.SendMessage("TerminarCinematica", SendMessageOptions.DontRequireReceiver);
        yield return new WaitForSecondsRealtime(0.5f);

        BotonAbrirGraficos[] botones = FindObjectsByType<BotonAbrirGraficos>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (botones.Length == 0)
        {
            errores.Add("[menu] No aparecio la fila Graficos en el panel de opciones.");
            yield break;
        }

        // Muestra el panel de opciones como si se hubiera pulsado "Opciones"
        Transform panel = botones[0].transform;
        while (panel.parent != null && panel.parent.GetComponent<Canvas>() == null)
            panel = panel.parent;
        foreach (Transform hermano in panel.parent)
            hermano.gameObject.SetActive(hermano == panel);
        panel.gameObject.SetActive(true);

        yield return new WaitForSecondsRealtime(0.5f);
        yield return Capturar("menu_opciones");

        botones[0].Abrir();
        yield return new WaitForSecondsRealtime(0.5f);
        yield return Capturar("menu_opciones_graficos");
        MenuGraficos.Cerrar();
    }

    /// <summary>Recorre cada valor de cada fila del menu de graficos y anota si algo fallo.</summary>
    IEnumerator ProbarOpciones(string escena, StringBuilder csv)
    {
        GestorGraficos.UsarNivel(NivelGrafico.Alto);

        foreach (OpcionMenu opcion in OpcionesGraficas.Crear())
        {
            if (opcion.EsSeccion || opcion.EsLeyenda)
                continue;

            for (int objetivo = 0; objetivo < opcion.valores.Length; objetivo++)
            {
                int erroresAntes = errores.Count;

                // Se llega al valor con las flechas, como haria el jugador
                for (int paso = 0; paso < opcion.valores.Length && opcion.Indice != objetivo; paso++)
                    opcion.Cambiar(objetivo > opcion.Indice ? 1 : -1);

                yield return null;
                yield return null;

                int nuevos = errores.Count - erroresAntes;
                csv.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3},{4}",
                    escena, opcion.nombre, opcion.Disponible ? opcion.valores[objetivo] : opcion.textoNoDisponible,
                    GestorGraficos.NombreNivelActivo, nuevos));
            }

            // Cada fila parte de Alto, para no probar combinaciones raras de la anterior
            GestorGraficos.UsarNivel(NivelGrafico.Alto);
            yield return null;
        }

        // Tambien los extremos de la calidad general con todo lo demas cambiado
        GestorGraficos.UsarNivel(NivelGrafico.MuyBajo);
        yield return new WaitForSecondsRealtime(0.5f);
        yield return Capturar(escena + "_opciones_muy_bajo");
        GestorGraficos.UsarNivel(NivelGrafico.Alto);
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
