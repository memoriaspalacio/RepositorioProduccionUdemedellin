using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace UdeM.Portafolio
{
    /// <summary>
    /// Prueba las vitrinas en Play sin nadie al teclado: abre cada escena, da Play, ejecuta sus
    /// acciones una por una (EjecutarAccion, como si se pulsara la tecla), guarda capturas con la
    /// interfaz incluida y apunta todo error o excepcion de la consola en un informe.
    /// Al entrar en Play el proyecto recarga el dominio, asi que el avance vive en SessionState y
    /// se retoma desde el constructor estatico.
    /// Desde consola: -executeMethod UdeM.Portafolio.VerificadorVitrinas.VerificarDesdeConsola
    /// [-capturas carpeta] [-escenas "a.unity;b.unity"] [-segundos 16] (sin -quit: sale solo).
    /// </summary>
    [InitializeOnLoad]
    public static class VerificadorVitrinas
    {
        const string Clave = "VerificadorVitrinas.";
        const string Carpeta = "Assets/_Juego Actual/Portafolio/Escenas";

        // Teclas que no se prueban: ocultan lo que se quiere ver, cambian la escala de tiempo o de nivel.
        static readonly Key[] Omitidas = { Key.H, Key.N, Key.T, Key.L, Key.M };

        static VerificadorVitrinas()
        {
            if (!SessionState.GetBool(Clave + "Activo", false))
                return;

            EditorApplication.update -= Paso;
            EditorApplication.update += Paso;
            Application.logMessageReceived -= AlRegistrar;
            Application.logMessageReceived += AlRegistrar;
        }

        [MenuItem("Portafolio/Probar todas las vitrinas en Play", priority = 60)]
        static void Menu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            Empezar(null, null, 16f, false);
        }

        public static void VerificarDesdeConsola()
        {
            string segundos = Argumento("-segundos");
            Empezar(Argumento("-escenas"), Argumento("-capturas"), segundos != null ? float.Parse(segundos) : 16f, false);
        }

        /// <summary>
        /// Para -executeMethod: una captura por vitrina en su momento mas vistoso (AccionesMiniatura),
        /// en la carpeta de -capturas con el nombre de la escena. Son las miniaturas del menu.
        /// </summary>
        public static void MiniaturasDesdeConsola()
        {
            Empezar(Argumento("-escenas"), Argumento("-capturas"), 12f, true);
        }

        /// <summary>
        /// Para -executeMethod: juega Crisantia, pulsa el boton de la pantalla de victoria como un
        /// jugador y anota que pasa con la cinematica final (y si se llega a la escena Victoria).
        /// </summary>
        public static void ProbarVictoriaDesdeConsola()
        {
            Empezar("Assets/_Juego Actual/Scenes/_ProperGameScenes/crisantia.unity", Argumento("-capturas"), 30f, false);
            SessionState.SetBool(Clave + "Victoria", true);
        }

        static void Empezar(string escenas, string capturas, float segundos, bool miniaturas)
        {
            SessionState.SetBool(Clave + "Victoria", false);
            if (string.IsNullOrEmpty(escenas))
            {
                escenas = string.Join(";", Directory.GetFiles(Carpeta, "*.unity")
                    .Select(r => r.Replace('\\', '/'))
                    .OrderBy(r => r));
            }

            string carpeta = capturas ?? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Capturas Portafolio", "Prueba"));
            Directory.CreateDirectory(carpeta);
            string informe = Path.Combine(carpeta, "informe.txt");
            File.WriteAllText(informe, "Prueba de vitrinas " + DateTime.Now + "\n");

            SessionState.SetBool(Clave + "Activo", true);
            SessionState.SetString(Clave + "Escenas", escenas);
            SessionState.SetString(Clave + "Carpeta", carpeta);
            SessionState.SetString(Clave + "Informe", informe);
            SessionState.SetFloat(Clave + "Segundos", segundos);
            SessionState.SetBool(Clave + "Miniaturas", miniaturas);
            SessionState.SetInt(Clave + "Indice", 0);
            SessionState.SetInt(Clave + "Errores", 0);
            SessionState.SetString(Clave + "Fase", "abrir");
            SessionState.SetFloat(Clave + "Arranque", (float)EditorApplication.timeSinceStartup);

            EditorApplication.update -= Paso;
            EditorApplication.update += Paso;
            Application.logMessageReceived -= AlRegistrar;
            Application.logMessageReceived += AlRegistrar;
        }

        static void Paso()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            string[] escenas = SessionState.GetString(Clave + "Escenas", "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            int indice = SessionState.GetInt(Clave + "Indice", 0);
            string fase = SessionState.GetString(Clave + "Fase", "abrir");
            float ahora = (float)EditorApplication.timeSinceStartup;

            // Por si algo se queda colgado: nunca mas de dos minutos por escena.
            if (ahora - SessionState.GetFloat(Clave + "Arranque", ahora) > 120f * Mathf.Max(1, escenas.Length) + 60f)
            {
                Anotar("TIEMPO AGOTADO: la prueba no termino.");
                Terminar(2);
                return;
            }

            switch (fase)
            {
                case "abrir":
                    if (indice >= escenas.Length)
                    {
                        int errores = SessionState.GetInt(Clave + "Errores", 0);
                        Anotar($"\nFIN: {escenas.Length} escenas, {errores} errores.");
                        Terminar(errores > 0 ? 1 : 0);
                        return;
                    }
                    Anotar($"\n== {escenas[indice]}");
                    EditorSceneManager.OpenScene(escenas[indice], OpenSceneMode.Single);
                    SessionState.SetString(Clave + "Fase", "entrar");
                    EditorApplication.isPlaying = true;
                    break;

                case "entrar":
                    if (!EditorApplication.isPlaying)
                        return;
                    SessionState.SetFloat(Clave + "Inicio", ahora);
                    SessionState.SetInt(Clave + "Accion", 0);
                    SessionState.SetInt(Clave + "Captura", 0);
                    SessionState.SetInt(Clave + "Segundo", 0);
                    SessionState.SetString(Clave + "Fase", "jugando");
                    break;

                case "jugando":
                    Jugar(escenas[indice], ahora - SessionState.GetFloat(Clave + "Inicio", ahora));
                    break;

                case "salir":
                    if (EditorApplication.isPlaying)
                        return;
                    SessionState.SetInt(Clave + "Indice", indice + 1);
                    SessionState.SetString(Clave + "Fase", "abrir");
                    break;
            }
        }

        static void Jugar(string escena, float t)
        {
            float duracion = SessionState.GetFloat(Clave + "Segundos", 16f);
            VitrinaPortafolio vitrina = Object.FindFirstObjectByType<VitrinaPortafolio>();

            if (SessionState.GetBool(Clave + "Miniaturas", false))
            {
                Miniatura(escena, vitrina, t);
                return;
            }

            if (SessionState.GetBool(Clave + "Victoria", false))
            {
                Victoria(t, duracion);
                return;
            }

            // El menu no es una vitrina: se recorren sus tarjetas sin abrir ninguna.
            MenuPortafolio menu = Object.FindFirstObjectByType<MenuPortafolio>();
            if (vitrina == null && menu != null)
            {
                int paso = SessionState.GetInt(Clave + "Accion", 0);
                if (t >= 2f + paso * 0.8f && paso < menu.Cantidad + 1)
                {
                    Anotar($"  t={t:0.0}s menu: elegir {paso + 1}");
                    menu.Elegir(paso + 1, false);
                    SessionState.SetInt(Clave + "Accion", paso + 1);
                }
                vitrina = null;
                SessionState.SetInt(Clave + "Accion", Mathf.Max(1, SessionState.GetInt(Clave + "Accion", 0)));
            }

            // Una accion cada 1.2 s a partir del segundo 2.5, en el orden del panel.
            int accion = SessionState.GetInt(Clave + "Accion", 0);
            if (vitrina != null && t >= 2.5f + accion * 1.2f)
            {
                List<Key> teclas = vitrina.SecuenciaDePrueba.Where(k => !Omitidas.Contains(k)).ToList();
                if (accion < teclas.Count)
                {
                    Anotar($"  t={t:0.0}s accion {teclas[accion]}");
                    try
                    {
                        vitrina.EjecutarAccion(teclas[accion]);
                    }
                    catch (Exception error)
                    {
                        Error($"excepcion en {teclas[accion]}: {error}");
                    }
                }
                SessionState.SetInt(Clave + "Accion", accion + 1);
            }
            else if (vitrina == null && t > 2f && SessionState.GetInt(Clave + "Accion", 0) == 0)
            {
                Error("la escena no tiene VitrinaPortafolio");
                SessionState.SetInt(Clave + "Accion", 1);
            }

            // Cuatro capturas repartidas en la prueba.
            int captura = SessionState.GetInt(Clave + "Captura", 0);
            if (captura < 4 && t >= duracion * (0.2f + 0.22f * captura))
            {
                string archivo = Path.Combine(SessionState.GetString(Clave + "Carpeta", ""), $"{Path.GetFileNameWithoutExtension(escena)}_{captura + 1}.png");
                try
                {
                    Capturar(archivo);
                }
                catch (Exception error)
                {
                    Anotar("  (no se pudo capturar: " + error.Message + ")");
                }
                SessionState.SetInt(Clave + "Captura", captura + 1);
            }

            if (t >= duracion)
            {
                SessionState.SetString(Clave + "Fase", "salir");
                EditorApplication.isPlaying = false;
            }
        }

        static void Victoria(float t, float duracion)
        {
            int paso = SessionState.GetInt(Clave + "Accion", 0);
            if (paso == 0 && t >= 4f)
            {
                // El mismo boton que pulsa el jugador: el que tiene TiempoGanar en su onClick.
                UnityEngine.UI.Button boton = Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .FirstOrDefault(b => Enumerable.Range(0, b.onClick.GetPersistentEventCount()).Any(i => b.onClick.GetPersistentMethodName(i) == "TiempoGanar"));
                Anotar($"  t={t:0.0}s boton de victoria: {(boton != null ? boton.name + (boton.gameObject.activeInHierarchy ? " (visible)" : " (oculto)") : "NO ENCONTRADO")}, reloj {(ControladorContrarreloj.Instancia != null ? "ok" : "NULL")}");
                // Antes de invocar: si el clic lanza una excepcion no se repite en cada frame.
                SessionState.SetInt(Clave + "Accion", 1);
                if (boton != null)
                    boton.onClick.Invoke();
            }

            // Estado cada segundo despues del clic.
            int segundo = Mathf.FloorToInt(t);
            if (paso >= 1 && segundo > SessionState.GetInt(Clave + "Segundo", 0))
            {
                SessionState.SetInt(Clave + "Segundo", segundo);
                GameObject cinematica = GameObject.Find("CinematicaFinal");
                UnityEngine.Video.VideoPlayer video = cinematica != null ? cinematica.GetComponent<UnityEngine.Video.VideoPlayer>() : null;
                string estado = cinematica == null ? "sin CinematicaFinal"
                    : video == null ? "CinematicaFinal sin VideoPlayer (aun en la transicion)"
                    : $"video preparado {video.isPrepared}, reproduciendo {video.isPlaying}, tiempo {video.time:0.0}/{(video.clip != null ? video.clip.length : 0):0.0}";
                int cuantas = Object.FindObjectsByType<CinematicaFinal>(FindObjectsSortMode.None).Length;
                Anotar($"  t={t:0.0}s escena {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}: {estado}, cinematicas {cuantas}, timeScale {Time.timeScale}");
                if (segundo % 4 == 0)
                {
                    try { Capturar(Path.Combine(SessionState.GetString(Clave + "Carpeta", ""), $"victoria_{segundo:00}.png")); }
                    catch (Exception error) { Anotar("  (sin captura: " + error.Message + ")"); }
                }
            }

            if (t >= duracion)
            {
                SessionState.SetString(Clave + "Fase", "salir");
                EditorApplication.isPlaying = false;
            }
        }

        static void Miniatura(string escena, VitrinaPortafolio vitrina, float t)
        {
            int paso = SessionState.GetInt(Clave + "Accion", 0);
            if (paso == 0 && t >= 2.5f)
            {
                if (vitrina != null)
                {
                    foreach (Key tecla in vitrina.AccionesMiniatura)
                        vitrina.EjecutarAccion(tecla);
                }
                SessionState.SetFloat(Clave + "Momento", t + (vitrina != null ? vitrina.EsperaMiniatura : 0f));
                SessionState.SetInt(Clave + "Accion", 1);
            }
            else if (paso == 1 && t >= SessionState.GetFloat(Clave + "Momento", 0f))
            {
                // Sin rotulo: el menu ya pone el titulo, y de fondo el texto de la miniatura estorba.
                RotuloVitrina rotulo = Object.FindFirstObjectByType<RotuloVitrina>();
                if (rotulo != null)
                    rotulo.Alternar();

                string archivo = Path.Combine(SessionState.GetString(Clave + "Carpeta", ""), Path.GetFileNameWithoutExtension(escena) + ".png");
                try
                {
                    Capturar(archivo);
                    Anotar("  miniatura " + archivo);
                }
                catch (Exception error)
                {
                    Error("no se pudo capturar la miniatura: " + error.Message);
                }
                SessionState.SetInt(Clave + "Accion", 2);
                SessionState.SetString(Clave + "Fase", "salir");
                EditorApplication.isPlaying = false;
            }
        }

        // Camera.Render no dibuja los Canvas en Screen Space Overlay: se pasan un momento a la camara.
        static void Capturar(string archivo)
        {
            Camera camara = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
            if (camara == null)
                return;

            const int ancho = 1600;
            const int alto = 900;
            List<Canvas> cambiados = new List<Canvas>();
            foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (canvas.isRootCanvas && canvas.renderMode == RenderMode.ScreenSpaceOverlay && canvas.isActiveAndEnabled)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camara;
                    canvas.planeDistance = camara.nearClipPlane + 0.05f;
                    cambiados.Add(canvas);
                }
            }

            RenderTexture destino = new RenderTexture(ancho, alto, 24);
            RenderTexture anteriorActiva = RenderTexture.active;
            RenderTexture anteriorDestino = camara.targetTexture;
            try
            {
                Canvas.ForceUpdateCanvases();
                camara.targetTexture = destino;
                camara.Render();
                RenderTexture.active = destino;
                Texture2D imagen = new Texture2D(ancho, alto, TextureFormat.RGB24, false);
                imagen.ReadPixels(new Rect(0f, 0f, ancho, alto), 0, 0);
                imagen.Apply();
                File.WriteAllBytes(archivo, imagen.EncodeToPNG());
                Object.DestroyImmediate(imagen);
            }
            finally
            {
                camara.targetTexture = anteriorDestino;
                RenderTexture.active = anteriorActiva;
                Object.DestroyImmediate(destino);
                foreach (Canvas canvas in cambiados)
                {
                    if (canvas != null)
                        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                }
            }
        }

        static void AlRegistrar(string mensaje, string pila, LogType tipo)
        {
            if (!SessionState.GetBool(Clave + "Activo", false))
                return;
            if (tipo == LogType.Error || tipo == LogType.Exception || tipo == LogType.Assert)
                Error(mensaje + "\n" + pila);
        }

        static void Error(string texto)
        {
            SessionState.SetInt(Clave + "Errores", SessionState.GetInt(Clave + "Errores", 0) + 1);
            Anotar("  ERROR: " + texto.Trim());
        }

        static void Anotar(string texto)
        {
            string informe = SessionState.GetString(Clave + "Informe", "");
            if (!string.IsNullOrEmpty(informe))
                File.AppendAllText(informe, texto + "\n");
        }

        static void Terminar(int codigo)
        {
            SessionState.SetBool(Clave + "Activo", false);
            EditorApplication.update -= Paso;
            Application.logMessageReceived -= AlRegistrar;

            string informe = SessionState.GetString(Clave + "Informe", "");
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(codigo);
            }
            else
            {
                Debug.Log("Prueba de vitrinas terminada. Informe: " + informe);
                EditorUtility.RevealInFinder(informe);
            }
        }

        static string Argumento(string nombre)
        {
            string[] argumentos = Environment.GetCommandLineArgs();
            int indice = Array.IndexOf(argumentos, nombre);
            return indice >= 0 && indice + 1 < argumentos.Length ? argumentos[indice + 1] : null;
        }
    }
}
