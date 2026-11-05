using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace UdeM.Portafolio
{
    /// <summary>
    /// Arma las escenas de portafolio (menu Portafolio). Solo escribe dentro de Portafolio/: las
    /// escenas, los materiales del set y su volumen. No toca prefabs, escenas del juego ni Build
    /// Settings: los personajes entran como instancias de sus prefabs.
    /// Las vitrinas de iluminacion son copias de los niveles: siguen usando el LightingData del
    /// original (los lightmaps no se duplican) y a la copia se le quita la logica de juego.
    /// Si una escena ya existe pregunta antes de reemplazarla (desde consola, solo con -reemplazar).
    /// </summary>
    public static class GeneradorVitrinas
    {
        const string Carpeta = "Assets/_Juego Actual/Portafolio";
        const string CarpetaEscenas = Carpeta + "/Escenas";
        const string CarpetaSet = Carpeta + "/Set";

        const string Escena01 = CarpetaEscenas + "/01_VFX_Muertes.unity";
        const string Escena02 = CarpetaEscenas + "/02_VFX_Sacerdote.unity";
        const string Escena03 = CarpetaEscenas + "/03_Animaciones.unity";
        const string Escena05 = CarpetaEscenas + "/05_Interfaz_Feedback.unity";
        const string Escena06 = CarpetaEscenas + "/06_Interfaz_Aislada.unity";
        const string Escena07 = CarpetaEscenas + "/07_Arte_Equipo.unity";
        const string Escena08 = CarpetaEscenas + "/08_Sistema_Grid.unity";
        const string CarpetaMiniaturas = Carpeta + "/Menu/Miniaturas";
        const string RutaRijusans = "Assets/_Juego Actual/Arte/Tipografias/Rijusans TMP.asset";
        const string RutaChartCementerio = "Assets/_Juego Actual/Resources/Charts/cementerio.json";

        // Rijusans, la sans del juego: la Kingstone es decorativa y en titulos y etiquetas no se lee bien.
        const string RutaFuente = RutaRijusans;
        const string RutaArabella = "Assets/AnimationTest/Prefabs/Arabella.prefab";
        const string RutaEsqueleto = "Assets/AnimationTest/Prefabs/Esqueleto.prefab";
        const string RutaMonja = "Assets/AnimationTest/Prefabs/Monja.prefab";
        const string RutaSacerdote = "Assets/AnimationTest/Prefabs/Sacerdote.prefab";
        const string RutaAngel = "Assets/_Juego Actual/Prefabs/angelAnimated.prefab";
        const string RutaCruz = "Assets/_Juego Actual/Prefabs/Cross.prefab";
        const string RutaCeldaFuego = "Assets/_Juego Actual/Prefabs/SisterAttackCell.prefab";

        // Carpeta con los clips de cada personaje (los .anim que no estan en su Animator tambien cuentan).
        static readonly Dictionary<string, string> CarpetaClips = new Dictionary<string, string>
        {
            { RutaArabella, "Assets/AnimationTest/ArabellaAnimations/AnimacionesModificables" },
            { RutaEsqueleto, "Assets/AnimationTest/Esqueleto" },
            { RutaMonja, "Assets/AnimationTest/Monja" },
            { RutaSacerdote, "Assets/AnimationTest/Sacerdote" },
            { RutaAngel, "Assets/AnimationTest/Angel" }
        };

        const string CarpetaNiveles = "Assets/_Juego Actual/Scenes/_ProperGameScenes";
        static readonly string[] Niveles = { "Cementerio", "Iglesia", "Mazmorra", "Crisantia" };

        // Scripts del juego que son ambientacion y se quedan en las copias de los niveles.
        static readonly HashSet<string> ScriptsDeEntorno = new HashSet<string>
        {
            "AtmosferaEscena", "LuzPersonajes", "SueloGrid", "LienzoCuadro", "MeshGrid"
        };

        static readonly Color Fondo = new Color(0.13f, 0.125f, 0.17f, 1f);
        static readonly Color ColorAcento = new Color(1f, 0.62f, 0.25f, 1f);
        static readonly Color ColorFoco = new Color(1f, 0.93f, 0.82f, 1f);

        static string RutaNivel(string nivel) => CarpetaEscenas + "/04_Iluminacion_" + nivel + ".unity";
        static string OriginalNivel(string nivel) => CarpetaNiveles + "/" + nivel.ToLowerInvariant() + ".unity";

        static IEnumerable<string> TodasLasEscenas()
        {
            yield return Escena01;
            yield return Escena02;
            yield return Escena03;
            foreach (string nivel in Niveles)
                yield return RutaNivel(nivel);
            yield return Escena05;
            yield return Escena06;
            yield return Escena07;
            yield return Escena08;
            yield return CargadorEscenas.RutaMenu;
        }

        #region Menu

        [MenuItem("Portafolio/Generar todas las vitrinas", priority = 0)]
        static void MenuTodas()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            Generar01();
            Generar02();
            Generar03();
            Generar04();
            Generar05();
            Generar06();
            Generar07();
            Generar08();
            GenerarMenu();
        }

        [MenuItem("Portafolio/Generar 01 VFX Muertes", priority = 20)]
        static void Menu01()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                Generar01();
        }

        [MenuItem("Portafolio/Generar 02 VFX Sacerdote", priority = 21)]
        static void Menu02()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                Generar02();
        }

        [MenuItem("Portafolio/Generar 03 Animaciones", priority = 22)]
        static void Menu03()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                Generar03();
        }

        [MenuItem("Portafolio/Generar 04 Iluminacion (copias de los niveles)", priority = 23)]
        static void Menu04()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                Generar04();
        }

        [MenuItem("Portafolio/Generar 05 Interfaz y feedback (copia del cementerio)", priority = 24)]
        static void Menu05()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                Generar05();
        }

        [MenuItem("Portafolio/Generar 06 Interfaz aislada (copia del cementerio)", priority = 25)]
        static void Menu06()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                Generar06();
        }

        [MenuItem("Portafolio/Generar 07 Arte del equipo", priority = 26)]
        static void Menu07()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                Generar07();
        }

        [MenuItem("Portafolio/Generar 08 Sistema de grid", priority = 27)]
        static void Menu08()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                Generar08();
        }

        [MenuItem("Portafolio/Generar menu de escenas", priority = 30)]
        static void MenuMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                GenerarMenu();
        }

        [MenuItem("Portafolio/Abrir menu de escenas", priority = 1)]
        static void AbrirMenu()
        {
            if (File.Exists(CargadorEscenas.RutaMenu) && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(CargadorEscenas.RutaMenu, OpenSceneMode.Single);
        }

        [MenuItem("Portafolio/Capturar vista de la escena abierta", priority = 40)]
        static void MenuCapturar()
        {
            Camera camara = Camera.main;
            if (camara == null)
            {
                EditorUtility.DisplayDialog("Portafolio", "La escena no tiene una camara con la etiqueta MainCamera.", "Aceptar");
                return;
            }

            string carpeta = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Capturas Portafolio"));
            string archivo = Path.Combine(carpeta, SceneManager.GetActiveScene().name + ".png");
            Capturar(camara, archivo, 1920, 1080);
            EditorUtility.RevealInFinder(archivo);
        }

        /// <summary>
        /// Para -executeMethod en modo batch: genera las vitrinas que todavia no existan, o todas
        /// si se pasa -reemplazar.
        /// </summary>
        public static void GenerarDesdeConsola()
        {
            Generar01();
            Generar02();
            Generar03();
            Generar04();
            Generar05();
            Generar06();
            Generar07();
            Generar08();
            GenerarMenu();
        }

        /// <summary>Para -executeMethod: solo el menu (con -miniaturas carpeta para usar capturas en Play).</summary>
        public static void GenerarMenuDesdeConsola()
        {
            GenerarMenu();
        }

        /// <summary>
        /// Para -executeMethod: abre cada vitrina y guarda lo que ve su camara en la carpeta que
        /// sigue a -capturas (por defecto "Capturas Portafolio" junto al proyecto). Con -escenas
        /// "a.unity;b.unity" captura esas escenas en lugar de las vitrinas (no las guarda).
        /// </summary>
        public static void CapturarDesdeConsola()
        {
            string carpeta = Argumento("-capturas") ?? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Capturas Portafolio"));
            string lista = Argumento("-escenas");
            IEnumerable<string> escenas = lista != null ? lista.Split(';') : TodasLasEscenas();

            foreach (string ruta in escenas)
            {
                if (!File.Exists(ruta))
                    continue;

                EditorSceneManager.OpenScene(ruta, OpenSceneMode.Single);
                Camera camara = Camera.main;
                if (camara == null)
                    camara = Object.FindFirstObjectByType<Camera>();
                if (camara != null)
                    Capturar(camara, Path.Combine(carpeta, Path.GetFileNameWithoutExtension(ruta) + ".png"), 1600, 900);
            }
        }

        #endregion

        #region Vitrinas de personajes

        static void Generar01()
        {
            if (!PuedeEscribir(Escena01))
                return;

            Scene escena = NuevaEscena();
            CrearCamara(new Vector3(0f, 1.05f, 0f), 9.5f, 1.5f, 22f, 18f);
            Transform luces = CrearLuces();
            CrearSuelo();
            CrearVolumen();

            Vitrina01Muertes vitrina = CrearVitrina<Vitrina01Muertes>(
                "Tres formas de morir",
                "Cada enemigo muere segun de que esta hecho");

            Transform personajes = new GameObject("Personajes").transform;
            var lista = new (string ruta, string nombre, EfectoMuerte.Estilo estilo, float altura)[]
            {
                (RutaEsqueleto, "Esqueleto (desintegrarse)", EfectoMuerte.Estilo.Desintegrarse, 1.8f),
                (RutaMonja, "Monja (arder)", EfectoMuerte.Estilo.Arder, 1.75f),
                (RutaSacerdote, "Sacerdote (ceniza blanca)", EfectoMuerte.Estilo.ArderCenizaBlanca, 1.85f),
                (RutaAngel, "Angel de piedra (romperse)", EfectoMuerte.Estilo.Romperse, 2.1f)
            };

            for (int i = 0; i < lista.Length; i++)
            {
                float x = (i - (lista.Length - 1) * 0.5f) * 2.2f;
                float alto = CrearPedestal(new Vector3(x, 0f, 0f), 0.75f, personajes, luces);
                GameObject modelo = ColocarPersonaje(lista[i].ruta, new Vector3(x, alto, 0f), lista[i].altura, 180f, personajes);
                vitrina.pedestales.Add(new Vitrina01Muertes.Pedestal
                {
                    nombre = lista[i].nombre,
                    modelo = modelo,
                    estilo = lista[i].estilo
                });
            }

            Guardar(escena, Escena01);
        }

        static void Generar02()
        {
            if (!PuedeEscribir(Escena02))
                return;

            Scene escena = NuevaEscena();
            CrearCamara(new Vector3(0f, 1f, 0.6f), 10f, 2.4f, 20f, 20f);
            Transform luces = CrearLuces();
            CrearSuelo();
            CrearVolumen();

            Vitrina02Sacerdote vitrina = CrearVitrina<Vitrina02Sacerdote>(
                "El kit del sacerdote",
                "Cruces, escudo divino, infierno e invocacion");

            Transform personajes = new GameObject("Personajes").transform;
            vitrina.modeloSacerdote = ColocarPersonaje(RutaSacerdote, Vector3.zero, 1.85f, 180f, personajes);
            vitrina.prefabCruz = AssetDatabase.LoadAssetAtPath<GameObject>(RutaCruz);
            vitrina.prefabCeldaFuego = AssetDatabase.LoadAssetAtPath<GameObject>(RutaCeldaFuego);

            // Dos monjas detras del fuego, un poco giradas hacia el centro del ataque.
            for (int lado = -1; lado <= 1; lado += 2)
            {
                Vector3 pie = vitrina.centroFuego + new Vector3(lado * 1.3f, 0f, 2.2f);
                GameObject monja = ColocarPersonaje(RutaMonja, pie, 1.75f, 180f + lado * 20f, personajes);
                if (monja != null)
                    vitrina.modelosMonja.Add(monja);
            }

            // Marcas en el suelo de donde invoca la cruz suelta y donde arde el fuego de las monjas.
            CrearMarca("Marca invocacion", vitrina.puntoInvocacion, 0.6f, personajes);
            CrearMarca("Marca fuego", vitrina.centroFuego, 1.7f, personajes);

            CrearFoco("Foco sacerdote", Vector3.zero, 2.2f, luces);
            CrearFoco("Foco monjas", vitrina.centroFuego + new Vector3(0f, 0f, 1f), 2.6f, luces);
            CrearFoco("Foco invocacion", vitrina.puntoInvocacion, 1f, luces);

            Guardar(escena, Escena02);
        }

        static void Generar03()
        {
            if (!PuedeEscribir(Escena03))
                return;

            Scene escena = NuevaEscena();
            CrearCamara(new Vector3(0f, 1.05f, 0f), 11.5f, 1.6f, 20f, 20f);
            Transform luces = CrearLuces();
            CrearSuelo();
            CrearVolumen();

            Vitrina03Animaciones vitrina = CrearVitrina<Vitrina03Animaciones>(
                "Animaciones de personajes",
                "Ciclos, ataques, reacciones y muertes");
            vitrina.fuenteEtiquetas = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(RutaFuente);

            Transform personajes = new GameObject("Personajes").transform;
            var lista = new (string ruta, string nombre, float altura)[]
            {
                (RutaArabella, "Arabella", 1.7f),
                (RutaEsqueleto, "Esqueleto", 1.8f),
                (RutaMonja, "Monja", 1.75f),
                (RutaSacerdote, "Sacerdote", 1.85f),
                (RutaAngel, "Angel", 2.1f)
            };

            for (int i = 0; i < lista.Length; i++)
            {
                float x = (i - (lista.Length - 1) * 0.5f) * 2.3f;
                float alto = CrearPedestal(new Vector3(x, 0f, 0f), 0.75f, personajes, luces);
                GameObject modelo = ColocarPersonaje(lista[i].ruta, new Vector3(x, alto, 0f), lista[i].altura, 180f, personajes);
                List<AnimationClip> clips = modelo != null ? ClipsDe(modelo, CarpetaClips[lista[i].ruta]) : new List<AnimationClip>();
                Debug.Log($"GeneradorVitrinas: {lista[i].nombre} con {clips.Count} clips: {string.Join(", ", clips.Select(c => c.name))}");

                vitrina.personajes.Add(new Vitrina03Animaciones.Personaje
                {
                    nombre = lista[i].nombre,
                    modelo = modelo,
                    clips = clips
                });
            }

            Guardar(escena, Escena03);
        }

        /// <summary>
        /// Los clips del Animator del personaje y los .anim de su carpeta (los que no estan
        /// conectados tambien cuentan). Si no hay ninguno, los clips de los FBX de la carpeta.
        /// Idle primero, el resto por nombre.
        /// </summary>
        static List<AnimationClip> ClipsDe(GameObject modelo, string carpeta)
        {
            List<AnimationClip> clips = new List<AnimationClip>();
            Animator animador = modelo.GetComponentInChildren<Animator>();
            if (animador != null && animador.runtimeAnimatorController != null)
                clips.AddRange(animador.runtimeAnimatorController.animationClips);

            List<AnimationClip> deFbx = new List<AnimationClip>();
            foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { carpeta }))
            {
                string ruta = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetDirectoryName(ruta).Replace('\\', '/') != carpeta)
                    continue;

                foreach (AnimationClip clip in AssetDatabase.LoadAllAssetsAtPath(ruta).OfType<AnimationClip>())
                {
                    if (clip.name.StartsWith("__preview__"))
                        continue;
                    if (ruta.EndsWith(".anim"))
                        clips.Add(clip);
                    else
                        deFbx.Add(clip);
                }
            }

            if (clips.Count == 0)
                clips.AddRange(deFbx);

            return clips
                .Where(c => c != null)
                .Distinct()
                .OrderBy(c => c.name.IndexOf("idle", StringComparison.OrdinalIgnoreCase) >= 0 ? 0 : 1)
                .ThenBy(c => c.name)
                .ToList();
        }

        #endregion

        #region Vitrinas de iluminacion

        static void Generar04()
        {
            bool existe = Niveles.Any(n => File.Exists(RutaNivel(n)));
            if (existe && !PuedeEscribir(RutaNivel(Niveles[0]), "las escenas 04 de iluminacion"))
                return;

            string[] rutas = Niveles.Select(RutaNivel).ToArray();
            foreach (string nivel in Niveles)
                GenerarNivel(nivel, rutas);
        }

        static void GenerarNivel(string nivel, string[] rutas)
        {
            string original = OriginalNivel(nivel);
            string destino = RutaNivel(nivel);
            if (!File.Exists(original))
            {
                Debug.LogWarning("GeneradorVitrinas: no se encontro " + original);
                return;
            }

            CrearCarpeta(CarpetaEscenas);
            if (File.Exists(destino))
                AssetDatabase.DeleteAsset(destino);
            if (!AssetDatabase.CopyAsset(original, destino))
            {
                Debug.LogWarning("GeneradorVitrinas: no se pudo copiar " + original);
                return;
            }

            Scene escena = EditorSceneManager.OpenScene(destino, OpenSceneMode.Single);
            int quitados = QuitarJuego(escena);

            Camera camara = ElegirCamara(escena);
            VueloLibre vuelo = camara.GetComponent<VueloLibre>();
            if (vuelo == null)
                vuelo = camara.gameObject.AddComponent<VueloLibre>();
            vuelo.puntosRecorrido = CrearRecorrido(escena, camara.transform.position);

            Vitrina04Iluminacion vitrina = CrearVitrina<Vitrina04Iluminacion>(
                "Luz y bake: " + nivel,
                "Lightmaps, luces, niebla y postproceso por capas");
            vitrina.camaraLibre = vuelo;
            vitrina.niveles = rutas;

            EditorSceneManager.SaveScene(escena);
            Debug.Log($"GeneradorVitrinas: {destino} creada ({quitados} scripts de juego quitados en la copia).");
        }

        /// <summary>Interfaz y feedback: el HUD completo del juego funcionando, con un bot de ritmo.</summary>
        static void Generar05()
        {
            if (!PuedeEscribir(Escena05))
                return;

            Vitrina05Interfaz vitrina = CopiaJugable<Vitrina05Interfaz>(Escena05,
                "Interfaz y feedback en juego",
                "Ritmo, calificaciones, dano, recompensas y transiciones");
            if (vitrina != null)
                EditorSceneManager.SaveScene(vitrina.gameObject.scene);
        }

        /// <summary>Los elementos del HUD por separado, cada uno solo sobre un fondo neutro.</summary>
        static void Generar06()
        {
            if (!PuedeEscribir(Escena06))
                return;

            Vitrina06InterfazAislada vitrina = CopiaJugable<Vitrina06InterfazAislada>(Escena06,
                "Interfaz aislada", "");
            if (vitrina != null)
                EditorSceneManager.SaveScene(vitrina.gameObject.scene);
        }

        /// <summary>
        /// Copia del cementerio con su ritmo, su HUD, su reloj y el jugador funcionando. Solo se
        /// apagan los enemigos (uno queda de plantilla para las recompensas) y se quita el contador
        /// de enemigos, que es el que da la victoria. El juez recibe el chart del cementerio: con
        /// otro nombre de escena no lo encontraria y las notas no irian con la musica.
        /// </summary>
        static T CopiaJugable<T>(string destino, string titulo, string subtitulo) where T : VitrinaPortafolio
        {
            string original = OriginalNivel("Cementerio");
            CrearCarpeta(CarpetaEscenas);
            if (File.Exists(destino))
                AssetDatabase.DeleteAsset(destino);
            if (!AssetDatabase.CopyAsset(original, destino))
            {
                Debug.LogWarning("GeneradorVitrinas: no se pudo copiar " + original);
                return null;
            }

            Scene escena = EditorSceneManager.OpenScene(destino, OpenSceneMode.Single);
            List<GameObject> raices = escena.GetRootGameObjects().ToList();

            GameObject plantilla = null;
            int apagados = 0;
            foreach (EnemyHealth enemigo in raices.SelectMany(r => r.GetComponentsInChildren<EnemyHealth>(true)))
            {
                bool esqueleto = enemigo.GetComponents<MonoBehaviour>().Any(m => m != null && m.GetType().Name == "SkeletonBehaviour");
                if (plantilla == null || (esqueleto && plantilla.GetComponents<MonoBehaviour>().All(m => m == null || m.GetType().Name != "SkeletonBehaviour")))
                    plantilla = enemigo.gameObject;
                enemigo.gameObject.SetActive(false);
                apagados++;
            }

            // Sin enemigos su panel del HUD diria "0 / 0". Sus textos viven dentro del prefab del HUD,
            // asi que se sube solo mientras el bloque no contenga nada mas que el contador.
            foreach (ContadorEnemigos contador in raices.SelectMany(r => r.GetComponentsInChildren<ContadorEnemigos>(true)).ToList())
            {
                SerializedObject datos = new SerializedObject(contador);
                List<TMP_Text> textos = new[] { "totalEnemies", "defeatEnemies" }
                    .Select(campo => datos.FindProperty(campo)?.objectReferenceValue as TMP_Text)
                    .Where(t => t != null)
                    .ToList();

                foreach (TMP_Text texto in textos)
                {
                    Transform bloque = texto.transform;
                    while (bloque.parent != null && bloque.parent.GetComponent<Canvas>() == null && SoloContador(bloque.parent, textos))
                        bloque = bloque.parent;
                    bloque.gameObject.SetActive(false);
                }
                Object.DestroyImmediate(contador);
            }

            BeatActionJudge juez = raices.SelectMany(r => r.GetComponentsInChildren<BeatActionJudge>(true)).FirstOrDefault();
            TextAsset chart = AssetDatabase.LoadAssetAtPath<TextAsset>(RutaChartCementerio);
            if (juez != null && chart != null)
            {
                SerializedObject datosJuez = new SerializedObject(juez);
                SerializedProperty campoChart = datosJuez.FindProperty("noteChartFile");
                if (campoChart != null)
                {
                    campoChart.objectReferenceValue = chart;
                    datosJuez.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            T vitrina = CrearVitrina<T>(titulo, subtitulo);
            // El HUD esta arriba al centro (cronometro) y a la izquierda (vida) y abajo (corazon):
            // el rotulo va arriba a la derecha y el panel de teclas arriba a la izquierda, bajo la vida.
            vitrina.panelArriba = true;
            vitrina.separacionPanel = 150f;
            vitrina.tamanoPanel = 17f;
            RotuloVitrina rotulo = vitrina.GetComponent<RotuloVitrina>();
            rotulo.anclaTitulo = new Vector2(1f, 1f);
            rotulo.posicionTitulo = new Vector2(-40f, -55f);

            RitmoVitrina ritmo = vitrina.GetComponent<RitmoVitrina>();
            if (ritmo == null)
                ritmo = vitrina.gameObject.AddComponent<RitmoVitrina>();
            ritmo.juez = juez;
            ritmo.jugador = raices.SelectMany(r => r.GetComponentsInChildren<PlayerHealth>(true)).FirstOrDefault();
            ritmo.plantillaEnemigo = plantilla;

            Debug.Log($"GeneradorVitrinas: {destino} creada ({apagados} enemigos apagados, juez {(juez != null ? "ok" : "FALTA")}, " +
                      $"chart {(chart != null ? "ok" : "FALTA")}, jugador {(ritmo.jugador != null ? "ok" : "FALTA")}).");
            return vitrina;
        }

        // Un bloque es del contador si todos sus textos son sus numeros, una barra o la palabra "Enemigos".
        static bool SoloContador(Transform bloque, List<TMP_Text> textosContador)
        {
            if (bloque.GetComponentsInChildren<Transform>(true).Length > 12)
                return false;

            return bloque.GetComponentsInChildren<TMP_Text>(true).All(t =>
                textosContador.Contains(t) ||
                t.text.IndexOf("enemig", StringComparison.OrdinalIgnoreCase) >= 0 ||
                t.text.Replace("/", "").Trim().All(char.IsDigit));
        }

        /// <summary>
        /// Deja en la copia solo lo que se ve: apaga la interfaz y quita los scripts de juego
        /// (enemigos, jugador, ritmo, Cinemachine...). Los renderers no se tocan, asi sus
        /// lightmaps siguen valiendo. Lo que no se puede quitar se apaga.
        /// </summary>
        static int QuitarJuego(Scene escena)
        {
            int quitados = 0;
            foreach (GameObject raiz in escena.GetRootGameObjects())
            {
                foreach (Canvas canvas in raiz.GetComponentsInChildren<Canvas>(true))
                {
                    if (canvas.isRootCanvas)
                        canvas.gameObject.SetActive(false);
                }
                foreach (EventSystem eventos in raiz.GetComponentsInChildren<EventSystem>(true))
                    eventos.gameObject.SetActive(false);

                for (int pasada = 0; pasada < 4; pasada++)
                {
                    List<MonoBehaviour> scripts = raiz.GetComponentsInChildren<MonoBehaviour>(true).Where(s => s != null).ToList();
                    foreach (MonoBehaviour script in scripts)
                    {
                        if (script == null || SeQueda(script) || UtilVitrina.LoPideOtro(script, scripts))
                            continue;

                        GameObject objeto = script.gameObject;
                        try
                        {
                            Object.DestroyImmediate(script);
                        }
                        catch (Exception error)
                        {
                            Debug.LogWarning($"GeneradorVitrinas: no se pudo quitar {script.GetType().Name} de {objeto.name}, se apaga el objeto ({error.Message}).");
                        }

                        if (script != null)
                            objeto.SetActive(false);
                        else
                            quitados++;
                    }
                }

                foreach (NavMeshAgent agente in raiz.GetComponentsInChildren<NavMeshAgent>(true))
                    Object.DestroyImmediate(agente);
                foreach (Rigidbody cuerpo in raiz.GetComponentsInChildren<Rigidbody>(true))
                    cuerpo.isKinematic = true;
            }
            return quitados;
        }

        static bool SeQueda(MonoBehaviour script)
        {
            Type tipo = script.GetType();
            string espacio = tipo.Namespace ?? "";
            return espacio.StartsWith("UnityEngine") ||
                   espacio.StartsWith("TMPro") ||
                   espacio == typeof(VitrinaPortafolio).Namespace ||
                   ScriptsDeEntorno.Contains(tipo.Name);
        }

        // La camara del nivel (con su postproceso y su fondo) pasa a ser la del recorrido.
        static Camera ElegirCamara(Scene escena)
        {
            List<Camera> camaras = escena.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>(true)).ToList();
            Camera elegida = camaras.FirstOrDefault(c => c.CompareTag("MainCamera")) ?? camaras.FirstOrDefault();

            if (elegida == null)
            {
                GameObject objeto = new GameObject("Camara");
                elegida = objeto.AddComponent<Camera>();
                elegida.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            }

            foreach (Camera otra in camaras)
            {
                if (otra != elegida)
                    otra.enabled = false;
            }

            elegida.tag = "MainCamera";
            elegida.enabled = true;
            for (Transform t = elegida.transform; t != null; t = t.parent)
                t.gameObject.SetActive(true);
            return elegida;
        }

        /// <summary>
        /// Seis puntos en un ovalo alrededor de lo horneado, mirando al centro, empezando por el
        /// lado de la camara del juego. Se pueden mover en el editor para hacer otra ruta.
        /// </summary>
        static List<Transform> CrearRecorrido(Scene escena, Vector3 desde)
        {
            List<Renderer> renders = escena.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<Renderer>())
                .Where(r => r.enabled && !(r is ParticleSystemRenderer))
                .ToList();
            List<Renderer> horneados = renders.Where(r => r.lightmapIndex >= 0 && r.lightmapIndex < 0xFFFE).ToList();
            if (horneados.Count > 0)
                renders = horneados;

            Bounds limites = new Bounds(desde, Vector3.one * 10f);
            if (renders.Count > 0)
            {
                limites = renders[0].bounds;
                foreach (Renderer render in renders)
                    limites.Encapsulate(render.bounds);
            }

            Vector3 centro = limites.center;
            float suelo = limites.min.y;
            float radioX = Mathf.Max(4f, limites.extents.x * 0.55f);
            float radioZ = Mathf.Max(4f, limites.extents.z * 0.55f);
            Vector3 mirar = new Vector3(centro.x, suelo + 1f, centro.z);
            float inicio = Mathf.Atan2(desde.z - centro.z, desde.x - centro.x);

            Transform padre = new GameObject("Recorrido camara").transform;
            List<Transform> puntos = new List<Transform>();
            for (int i = 0; i < 6; i++)
            {
                float angulo = inicio + i * Mathf.PI * 2f / 6f;
                Transform punto = new GameObject("Punto " + (i + 1)).transform;
                punto.SetParent(padre, false);
                // Sube y baja un poco para que el recorrido no sea plano.
                float altura = suelo + 3.2f + (i % 2 == 0 ? 0f : 1.6f);
                punto.position = new Vector3(centro.x + Mathf.Cos(angulo) * radioX, altura, centro.z + Mathf.Sin(angulo) * radioZ);
                punto.rotation = Quaternion.LookRotation(mirar - punto.position);
                puntos.Add(punto);
            }
            return puntos;
        }

        #endregion

        #region Arte del equipo, grid y menu

        // Arte 2D del juego (interfaz, iconos, transiciones). Los cuadros de Crisantia son obras de
        // Wikimedia Commons y la imagen generada de Placeholders no es del equipo: no entran.
        static readonly string[] Arte2D =
        {
            "Assets/_Juego Actual/Arte/Sprites/BeforeTheDawnIcon.png",
            "Assets/_Juego Actual/Arte/UI/Interfaz _ sintitulo.jpg",
            "Assets/_Juego Actual/Arte/UI/ui.jpg",
            "Assets/_Juego Actual/Arte/UI/Instrucciones.png",
            "Assets/_Juego Actual/Arte/UI/Créditos.jpg",
            "Assets/_Juego Actual/Resources/Transiciones/Corazon_Transicion.png",
            "Assets/_Juego Actual/Resources/Transiciones/Fondo_Transicion_Oscuro.png",
            "Assets/_Juego Actual/Arte/Sprites/UI/Heart.png",
            "Assets/_Juego Actual/Arte/UI/barradevida.png",
            "Assets/_Juego Actual/Arte/UI/panel_ui.png",
            "Assets/_Juego Actual/Arte/UI/button.png",
            "Assets/_Juego Actual/Arte/UI/Cursor.png",
            "Assets/_Juego Actual/Arte/UI/nota_musical.png",
            "Assets/_Juego Actual/Arte/Sprites/UI/Pausa.png"
        };

        const string CarpetaTexturas = "Assets/_Juego Actual/Arte/Textures";

        static readonly string[] ModelosProps =
        {
            "Assets/_Juego Actual/Arte/Models/Mapas/cementerio_primerescenario.fbx",
            "Assets/_Juego Actual/Arte/Models/Mapas/Iglesia.fbx",
            "Assets/_Juego Actual/Arte/Models/Mapas/Mazmorra.fbx",
            "Assets/_Juego Actual/Arte/Models/Mapas/crisantia.fbx",
            "Assets/_Juego Actual/Arte/Models/Mapas/Silla.fbx",
            "Assets/_Juego Actual/Arte/Models/Nuestros Modelos/Puerta.fbx"
        };

        static readonly string[] SuelosNiveles =
        {
            "Assets/_Juego Actual/Arte/Ambientacion/Cementerio/M_Suelo_Cementerio.mat",
            "Assets/_Juego Actual/Arte/Ambientacion/Iglesia/M_Suelo_Iglesia.mat",
            "Assets/_Juego Actual/Arte/Ambientacion/Mazmorra/M_Suelo_Mazmorra.mat",
            "Assets/_Juego Actual/Arte/Ambientacion/Crisantia/M_Suelo_Crisantia.mat"
        };

        /// <summary>Galeria del arte del equipo en cuatro salas a lo largo del eje X.</summary>
        static void Generar07()
        {
            if (!PuedeEscribir(Escena07))
                return;

            Scene escena = NuevaEscena();
            CrearCamara(new Vector3(-17f, 2.4f, 2.5f), 12f, 1.8f, 12f, 24f);
            Transform luces = CrearLuces();
            CrearSuelo();
            CrearVolumen();

            Vitrina07Arte vitrina = CrearVitrina<Vitrina07Arte>("Arte del equipo", "");
            vitrina.materialArcilla = MaterialSet("M_Arcilla_Vitrina", new Color(0.72f, 0.7f, 0.67f, 1f), 0.25f, Color.black);

            Material cuadro = MaterialUnlit("M_Cuadro_Vitrina", Color.white, false);
            // Los PNG con transparencia (iconos, barra de vida, cursor) se recortan: se ve el marco detras.
            cuadro.SetFloat("_AlphaClip", 1f);
            cuadro.SetFloat("_Cutoff", 0.1f);
            cuadro.EnableKeyword("_ALPHATEST_ON");
            cuadro.renderQueue = (int)RenderQueue.AlphaTest;
            Material marco = MaterialSet("M_Marco_Vitrina", new Color(0.13f, 0.085f, 0.06f, 1f), 0.45f, Color.black);
            Material pared = MaterialSet("M_Pared_Vitrina", new Color(0.2f, 0.19f, 0.24f, 1f), 0.1f, Color.black);

            // Sala 1: arte 2D e interfaz, en una pared.
            List<Texture2D> imagenes = Arte2D.Where(File.Exists).Select(AssetDatabase.LoadAssetAtPath<Texture2D>).Where(t => t != null).ToList();
            Vitrina07Arte.Sala sala2D = SalaGaleria("Arte 2D e interfaz", "Pantallas, iconos, interfaz y transiciones del juego",
                new Vector3(-17f, 0f, 0f), imagenes, 5, new Vector2(2.1f, 1.55f), new Vector2(2.45f, 1.95f), cuadro, marco, pared);
            vitrina.salas.Add(sala2D);

            // Sala 2: texturas pintadas.
            List<Texture2D> texturas = Directory.GetFiles(CarpetaTexturas)
                .Where(r => (r.EndsWith(".png") || r.EndsWith(".jpg")) && !Path.GetFileName(r).StartsWith("unnamed") && !Path.GetFileName(r).StartsWith("IMG_"))
                .Select(r => AssetDatabase.LoadAssetAtPath<Texture2D>(r.Replace('\\', '/')))
                .Where(t => t != null)
                .OrderBy(t => t.name)
                .ToList();
            vitrina.salas.Add(SalaGaleria("Texturas", "Texturas pintadas de personajes, props y escenarios",
                new Vector3(-3f, 0f, 0f), texturas, 7, new Vector2(1.25f, 1.25f), new Vector2(1.5f, 1.5f), cuadro, marco, pared));

            // Sala 3: props sacados de los modelos de los niveles, en gradas.
            Transform raizProps = new GameObject("Sala Props").transform;
            raizProps.position = new Vector3(11f, 0f, 1.5f);
            List<GameObject> props = new List<GameObject>();
            foreach (string modelo in ModelosProps)
                props.AddRange(PropsDe(modelo, modelo.Contains("/Mapas/") && !modelo.EndsWith("Silla.fbx") ? 4 : 1));
            Vitrina07Arte.Sala salaProps = new Vitrina07Arte.Sala
            {
                nombre = "Props",
                descripcion = "Props modelados y texturizados para los cuatro niveles",
                centro = raizProps,
                distancia = 13f,
                altura = 3f,
                modelos = true
            };
            const int columnas = 6;
            for (int i = 0; i < props.Count; i++)
            {
                int fila = i / columnas;
                int columna = i % columnas;
                float x = raizProps.position.x + (columna - (columnas - 1) * 0.5f) * 2.2f;
                float z = fila * 2.4f;
                float alto = CrearPedestal(new Vector3(x, fila * 0.55f, z), 0.8f, raizProps, null);
                AcomodarProp(props[i], new Vector3(x, alto, z), 1.35f);
                props[i].transform.SetParent(raizProps, true);
                props[i].AddComponent<GiroVitrina>().velocidad = 14f;
                CrearEtiqueta(props[i].name, new Vector3(x, 0.02f + fila * 0.55f, z - 1.05f), raizProps);
                salaProps.piezas.Add(props[i].transform);
            }
            vitrina.salas.Add(salaProps);
            CrearFoco("Foco props", raizProps.position + new Vector3(0f, 0f, 1.5f), 6.5f, luces);
            Debug.Log("GeneradorVitrinas: props de la galeria: " + string.Join(", ", props.Select(p => p.name)));

            // Sala 4: personajes modelados.
            Transform raizPersonajes = new GameObject("Sala Personajes").transform;
            raizPersonajes.position = new Vector3(26f, 0f, 0f);
            Vitrina07Arte.Sala salaPersonajes = new Vitrina07Arte.Sala
            {
                nombre = "Personajes",
                descripcion = "Modelado de los personajes del juego",
                centro = raizPersonajes,
                distancia = 11f,
                altura = 1.8f,
                modelos = true
            };
            var personajes = new (string ruta, string nombre, float altura)[]
            {
                (RutaArabella, "Arabella", 1.7f),
                (RutaEsqueleto, "Esqueleto", 1.8f),
                (RutaMonja, "Monja", 1.75f),
                (RutaSacerdote, "Sacerdote", 1.85f),
                (RutaAngel, "Angel de piedra", 2.1f),
                ("Assets/_Juego Actual/Arte/Models/Personajes/cruz.fbx", "Cruz", 1.3f)
            };
            for (int i = 0; i < personajes.Length; i++)
            {
                float x = raizPersonajes.position.x + (i - (personajes.Length - 1) * 0.5f) * 2.2f;
                float alto = CrearPedestal(new Vector3(x, 0f, 0f), 0.75f, raizPersonajes, null);
                GameObject modelo = ColocarPersonaje(personajes[i].ruta, new Vector3(x, alto, 0f), personajes[i].altura, 180f, raizPersonajes);
                if (modelo == null)
                    continue;
                modelo.name = personajes[i].nombre;
                modelo.AddComponent<GiroVitrina>().velocidad = 10f;
                CrearEtiqueta(personajes[i].nombre, new Vector3(x, 0.02f, -1.05f), raizPersonajes);
                salaPersonajes.piezas.Add(modelo.transform);
            }
            vitrina.salas.Add(salaPersonajes);
            CrearFoco("Foco personajes", raizPersonajes.position, 6.5f, luces);

            Guardar(escena, Escena07);
        }

        static Vitrina07Arte.Sala SalaGaleria(string nombre, string descripcion, Vector3 centro, List<Texture2D> imagenes, int columnas,
            Vector2 caja, Vector2 paso, Material cuadro, Material marco, Material pared)
        {
            int filas = Mathf.Max(1, Mathf.CeilToInt(imagenes.Count / (float)columnas));
            // El centro de la sala es el centro de la pared: ahi apunta la camara.
            Transform raiz = new GameObject("Sala " + nombre).transform;
            raiz.position = centro + new Vector3(0f, 1.2f + (filas - 1) * paso.y * 0.5f, 2.5f);

            GameObject fondo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            fondo.name = "Pared";
            fondo.transform.SetParent(raiz, false);
            fondo.transform.position = centro + new Vector3(0f, 0.6f + filas * paso.y * 0.5f, 3.1f);
            fondo.transform.localScale = new Vector3(columnas * paso.x + 1.2f, filas * paso.y + 1.6f, 1f);
            fondo.GetComponent<Renderer>().sharedMaterial = pared;
            Object.DestroyImmediate(fondo.GetComponent<Collider>());

            Vitrina07Arte.Sala sala = new Vitrina07Arte.Sala
            {
                nombre = nombre,
                descripcion = descripcion,
                centro = raiz,
                distancia = Mathf.Max(8f, columnas * paso.x * 1.15f, filas * paso.y * 2.3f),
                altura = 0.4f
            };

            for (int i = 0; i < imagenes.Count; i++)
            {
                int fila = i / columnas;
                int columna = i % columnas;
                Vector3 posicion = centro + new Vector3((columna - (columnas - 1) * 0.5f) * paso.x, 1.2f + (filas - 1 - fila) * paso.y, 3f);

                Transform pieza = new GameObject(NombreBonito(imagenes[i].name)).transform;
                pieza.SetParent(raiz, false);
                pieza.position = posicion;

                GameObject borde = GameObject.CreatePrimitive(PrimitiveType.Cube);
                borde.name = "Marco";
                borde.transform.SetParent(pieza, false);
                // El quad mira hacia -Z (hacia la camara): la imagen va delante y el marco detras.
                borde.transform.localPosition = new Vector3(0f, 0f, 0.03f);
                borde.transform.localScale = new Vector3(1f, 1f, 0.05f);
                borde.GetComponent<Renderer>().sharedMaterial = marco;
                Object.DestroyImmediate(borde.GetComponent<Collider>());

                GameObject imagen = GameObject.CreatePrimitive(PrimitiveType.Quad);
                imagen.name = "Imagen";
                imagen.transform.SetParent(pieza, false);
                imagen.transform.localPosition = new Vector3(0f, 0f, -0.02f);
                imagen.GetComponent<Renderer>().sharedMaterial = cuadro;
                Object.DestroyImmediate(imagen.GetComponent<Collider>());

                CuadroGaleria galeria = imagen.AddComponent<CuadroGaleria>();
                galeria.imagen = imagenes[i];
                galeria.cajaMaxima = caja;
                galeria.marco = borde.transform;
                galeria.Aplicar();

                sala.piezas.Add(pieza);
            }
            return sala;
        }

        /// <summary>
        /// Props de un modelo de nivel: mallas unicas de tamano medio (sin suelos, paredes ni
        /// primitivas sin nombre), repartidas entre las de distintos tamanos. Cada una se arma como
        /// un objeto nuevo con la misma malla y materiales: el modelo original no se toca.
        /// </summary>
        static List<GameObject> PropsDe(string rutaModelo, int maximo)
        {
            List<GameObject> props = new List<GameObject>();
            GameObject modelo = AssetDatabase.LoadAssetAtPath<GameObject>(rutaModelo);
            if (modelo == null)
                return props;

            HashSet<Mesh> vistas = new HashSet<Mesh>();
            HashSet<string> nombres = new HashSet<string>();
            var candidatos = new List<(MeshFilter malla, float tamano)>();
            foreach (MeshFilter filtro in modelo.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh malla = filtro.sharedMesh;
                if (malla == null || !vistas.Add(malla) || filtro.GetComponent<MeshRenderer>() == null)
                    continue;

                string nombre = NombreBonito(filtro.name);
                if (Generico(nombre) || !nombres.Add(nombre.ToLowerInvariant()))
                    continue;

                Vector3 escala = filtro.transform.lossyScale;
                Vector3 tamano = Vector3.Scale(malla.bounds.size, new Vector3(Mathf.Abs(escala.x), Mathf.Abs(escala.y), Mathf.Abs(escala.z)));
                candidatos.Add((filtro, Mathf.Max(tamano.x, tamano.y, tamano.z)));
            }

            if (candidatos.Count == 0)
                return props;

            // Fuera lo muy grande (suelos, muros, el mapa entero) y lo diminuto, respecto a la mediana.
            candidatos.Sort((a, b) => a.tamano.CompareTo(b.tamano));
            float mediana = candidatos[candidatos.Count / 2].tamano;
            List<MeshFilter> utiles = candidatos
                .Where(c => c.tamano <= mediana * 4f && c.tamano >= mediana / 5f)
                .Select(c => c.malla)
                .ToList();
            if (utiles.Count == 0)
                utiles = candidatos.Select(c => c.malla).ToList();

            int cantidad = Mathf.Min(maximo, utiles.Count);
            for (int i = 0; i < cantidad; i++)
            {
                MeshFilter filtro = utiles[cantidad == 1 ? utiles.Count - 1 : Mathf.RoundToInt(i * (utiles.Count - 1) / (float)(cantidad - 1))];
                GameObject prop = new GameObject(NombreBonito(filtro.name));
                prop.AddComponent<MeshFilter>().sharedMesh = filtro.sharedMesh;
                prop.AddComponent<MeshRenderer>().sharedMaterials = filtro.GetComponent<MeshRenderer>().sharedMaterials;
                prop.transform.rotation = filtro.transform.rotation;
                prop.transform.localScale = filtro.transform.lossyScale;
                props.Add(prop);
            }
            return props;
        }

        static bool Generico(string nombre)
        {
            string n = nombre.ToLowerInvariant();
            string[] primitivas = { "plane", "cube", "cylinder", "sphere", "circle", "default", "mesh", "object", "box", "polysurface", "node", "grid" };
            string[] grandes = { "suelo", "piso", "floor", "pared", "wall", "terreno", "techo", "muro", "ground", "escenario", "mapa" };
            return primitivas.Any(p => n.StartsWith(p)) || grandes.Any(g => n.Contains(g)) || n.Length < 2;
        }

        /// <summary>Escala el prop para que su lado mayor mida 'tamano' y lo apoya centrado en el punto.</summary>
        static void AcomodarProp(GameObject prop, Vector3 apoyo, float tamano)
        {
            Renderer render = prop.GetComponent<Renderer>();
            Bounds limites = render.bounds;
            float mayor = Mathf.Max(limites.size.x, limites.size.y, limites.size.z);
            if (mayor > 0.0001f)
                prop.transform.localScale *= tamano / mayor;
            limites = render.bounds;
            prop.transform.position += new Vector3(apoyo.x - limites.center.x, apoyo.y - limites.min.y, apoyo.z - limites.center.z);
        }

        static string NombreBonito(string nombre)
        {
            string limpio = Regex.Replace(nombre, @"\.\d+$", "");
            limpio = Regex.Replace(limpio, @"[_\-]+", " ");
            limpio = Regex.Replace(limpio, @"\s*\d+$", "");
            limpio = Regex.Replace(limpio, @"([a-z])([A-Z])", "$1 $2").Trim();
            if (limpio.Length == 0)
                return nombre;
            return char.ToUpperInvariant(limpio[0]) + limpio.Substring(1);
        }

        static void CrearEtiqueta(string texto, Vector3 posicion, Transform padre)
        {
            GameObject objeto = new GameObject("Etiqueta " + texto, typeof(RectTransform));
            objeto.transform.SetParent(padre, true);
            objeto.transform.position = posicion;
            objeto.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);

            TextMeshPro tmp = objeto.AddComponent<TextMeshPro>();
            tmp.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(RutaFuente);
            tmp.text = texto;
            tmp.fontSize = 2.2f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.color = new Color(1f, 0.86f, 0.6f, 1f);
            tmp.rectTransform.sizeDelta = new Vector2(2.2f, 0.6f);
        }

        /// <summary>El suelo de grid del juego con un personaje que avanza por casillas y enemigos en otras.</summary>
        static void Generar08()
        {
            if (!PuedeEscribir(Escena08))
                return;

            Scene escena = NuevaEscena();
            CrearCamara(new Vector3(0f, 0f, 0f), 14f, 10f, 18f, 26f);
            Transform luces = CrearLuces();
            CrearSuelo();
            CrearVolumen();

            Vitrina08Grid vitrina = CrearVitrina<Vitrina08Grid>("Sistema de grid", "Suelo procedural con el grid dentro del shader");

            // El plano de grid cubre las casillas de la vitrina y una mas de borde.
            float celda = vitrina.tamanoCelda;
            GameObject suelo = GameObject.CreatePrimitive(PrimitiveType.Plane);
            suelo.name = "Suelo grid";
            suelo.transform.position = new Vector3(0f, 0.01f, 0f);
            suelo.transform.localScale = new Vector3((vitrina.mitadCeldas.x * 2 + 3) * celda / 10f, 1f, (vitrina.mitadCeldas.y * 2 + 3) * celda / 10f);
            Object.DestroyImmediate(suelo.GetComponent<Collider>());

            for (int i = 0; i < SuelosNiveles.Length; i++)
            {
                Material material = AssetDatabase.LoadAssetAtPath<Material>(SuelosNiveles[i]);
                if (material == null)
                    continue;
                vitrina.suelosNiveles.Add(material);
                vitrina.nombresNiveles.Add(Niveles[i]);
            }
            Renderer render = suelo.GetComponent<Renderer>();
            if (vitrina.suelosNiveles.Count > 0)
                render.sharedMaterial = vitrina.suelosNiveles[0];
            vitrina.suelo = render;

            Transform personajes = new GameObject("Personajes").transform;
            vitrina.modeloJugador = ColocarPersonaje(RutaArabella, Vector3.zero, 1.7f, 180f, personajes);
            var ocupantes = new (string ruta, Vector2Int celda, float altura)[]
            {
                (RutaEsqueleto, new Vector2Int(2, 1), 1.8f),
                (RutaMonja, new Vector2Int(-2, -1), 1.75f),
                (RutaSacerdote, new Vector2Int(3, -2), 1.85f),
                (RutaAngel, new Vector2Int(-3, 2), 2.1f)
            };
            foreach (var ocupante in ocupantes)
            {
                Vector3 pie = new Vector3(ocupante.celda.x * celda, 0f, ocupante.celda.y * celda);
                GameObject modelo = ColocarPersonaje(ocupante.ruta, pie, ocupante.altura, 180f, personajes);
                if (modelo != null)
                    vitrina.ocupantes.Add(modelo);
            }

            vitrina.prefabCeldaFuego = AssetDatabase.LoadAssetAtPath<GameObject>(RutaCeldaFuego);
            vitrina.materialCasilla = MaterialUnlit("M_Casilla_Vitrina", Color.white, true);
            CrearFoco("Foco grid", Vector3.zero, 7f, luces);

            Guardar(escena, Escena08);
        }

        static readonly (string escena, string titulo, string categoria, string descripcion)[] Catalogo =
        {
            (Escena01, "Tres formas de morir", "VFX", "Cada enemigo muere segun de que esta hecho: el esqueleto se deshace en polvo, las monjas y el sacerdote arden y el angel de piedra revienta en rocas."),
            (Escena02, "El kit del sacerdote", "VFX", "Cruces que flotan y atacan, un escudo divino que se agrieta, el descenso al infierno, la invocacion y el fuego de las monjas."),
            (Escena03, "Animaciones de personajes", "Animacion", "Ciclos, ataques, reacciones y muertes de Arabella, el esqueleto, la monja, el sacerdote y el angel."),
            (CarpetaEscenas + "/04_Iluminacion_Cementerio.unity", "Luz y bake: Cementerio", "Iluminacion", "Recorre el nivel y enciende o apaga cada capa: lightmaps horneados, luces, reflejos, niebla, particulas y postproceso."),
            (CarpetaEscenas + "/04_Iluminacion_Iglesia.unity", "Luz y bake: Iglesia", "Iluminacion", "Haces de luz, niebla y bake de la iglesia, capa por capa y con recorrido libre."),
            (CarpetaEscenas + "/04_Iluminacion_Mazmorra.unity", "Luz y bake: Mazmorra", "Iluminacion", "Antorchas, ojos que brillan y bake de la mazmorra, capa por capa y con recorrido libre."),
            (CarpetaEscenas + "/04_Iluminacion_Crisantia.unity", "Luz y bake: Crisantia", "Iluminacion", "Cuadros emisivos, luciernagas y bake de Crisantia, capa por capa y con recorrido libre."),
            (Escena05, "Interfaz y feedback en juego", "Game feel", "El HUD real con el ritmo funcionando: corazon, calificaciones, pista, dano, recompensas y cronometro."),
            (Escena06, "Interfaz aislada", "UI", "Cada elemento de la interfaz solo y en grande sobre un fondo neutro, con el ritmo de verdad debajo."),
            (Escena07, "Arte del equipo", "Arte", "Arte 2D e interfaz, texturas pintadas, props de los niveles y personajes modelados, con vista de arcilla."),
            (Escena08, "Sistema de grid", "Arte tecnico", "El suelo procedural con el grid dentro del shader: estilos, integracion, patrones, pulso con el beat y casillas.")
        };

        /// <summary>
        /// El menu de seleccion. Cada entrada lleva su miniatura: la de -miniaturas (capturas en Play
        /// del verificador) si existe, la que ya este en Portafolio/Menu/Miniaturas, o una captura de la
        /// camara de la escena en el editor.
        /// </summary>
        static void GenerarMenu()
        {
            if (!PuedeEscribir(CargadorEscenas.RutaMenu))
                return;

            string capturas = Argumento("-miniaturas");
            List<Texture2D> miniaturas = Catalogo.Select(c => Miniatura(c.escena, capturas)).ToList();

            Scene escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject objetoCamara = new GameObject("Camara");
            objetoCamara.tag = "MainCamera";
            Camera camara = objetoCamara.AddComponent<Camera>();
            camara.clearFlags = CameraClearFlags.SolidColor;
            camara.backgroundColor = Color.black;
            // En la escena no hay nada mas que el menu; con la camara viendolo el verificador puede capturarlo.
            camara.cullingMask = ~0;
            objetoCamara.AddComponent<AudioListener>();

            GameObject eventos = new GameObject("EventSystem");
            eventos.AddComponent<EventSystem>();
            eventos.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

            MenuPortafolio menu = new GameObject("Menu").AddComponent<MenuPortafolio>();
            menu.fuenteTitulos = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(RutaFuente);
            menu.fuenteTexto = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(RutaRijusans);
            for (int i = 0; i < Catalogo.Length; i++)
            {
                if (!File.Exists(Catalogo[i].escena))
                    continue;
                menu.entradas.Add(new MenuPortafolio.Entrada
                {
                    titulo = Catalogo[i].titulo,
                    categoria = Catalogo[i].categoria,
                    descripcion = Catalogo[i].descripcion,
                    ruta = Catalogo[i].escena,
                    miniatura = miniaturas[i]
                });
            }

            Guardar(escena, CargadorEscenas.RutaMenu);
        }

        static Texture2D Miniatura(string escena, string capturas)
        {
            if (!File.Exists(escena))
                return null;

            string nombre = Path.GetFileNameWithoutExtension(escena);
            string destino = CarpetaMiniaturas + "/" + nombre + ".png";
            CrearCarpeta(CarpetaMiniaturas);

            string origen = capturas != null ? Path.Combine(capturas, nombre + ".png") : null;
            if (origen != null && File.Exists(origen))
            {
                File.Copy(origen, destino, true);
            }
            else if (!File.Exists(destino))
            {
                EditorSceneManager.OpenScene(escena, OpenSceneMode.Single);
                Camera camara = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
                if (camara == null)
                    return null;
                Capturar(camara, Path.GetFullPath(destino), 1280, 720);
            }

            AssetDatabase.ImportAsset(destino, ImportAssetOptions.ForceUpdate);
            TextureImporter importador = AssetImporter.GetAtPath(destino) as TextureImporter;
            if (importador != null)
            {
                importador.textureType = TextureImporterType.Default;
                importador.mipmapEnabled = false;
                importador.maxTextureSize = 1024;
                importador.npotScale = TextureImporterNPOTScale.None;
                importador.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(destino);
        }

        static Material MaterialUnlit(string nombre, Color color, bool transparente)
        {
            string ruta = CarpetaSet + "/" + nombre + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            bool nuevo = material == null;
            if (nuevo)
            {
                CrearCarpeta(CarpetaSet);
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            }

            material.SetColor("_BaseColor", color);
            if (transparente)
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Transparent;
            }

            if (nuevo)
                AssetDatabase.CreateAsset(material, ruta);
            else
                EditorUtility.SetDirty(material);
            return material;
        }

        #endregion

        #region Piezas del set

        static Scene NuevaEscena()
        {
            Scene escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Fondo gris azulado y ambiente medio: se ve el set, y el contraste lo ponen los focos y el contraluz.
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.55f, 0.66f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.32f, 0.31f, 0.38f, 1f);
            RenderSettings.ambientGroundColor = new Color(0.14f, 0.13f, 0.16f, 1f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.03f;
            RenderSettings.fogColor = Fondo;
            return escena;
        }

        static void CrearCamara(Vector3 objetivo, float distancia, float altura, float amplitud, float ciclo)
        {
            GameObject objeto = new GameObject("Camara");
            objeto.tag = "MainCamera";

            Camera camara = objeto.AddComponent<Camera>();
            camara.clearFlags = CameraClearFlags.SolidColor;
            camara.backgroundColor = Fondo;
            camara.fieldOfView = 35f;
            camara.nearClipPlane = 0.1f;
            camara.farClipPlane = 150f;

            UniversalAdditionalCameraData datos = camara.GetUniversalAdditionalCameraData();
            datos.renderPostProcessing = true;
            datos.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

            objeto.AddComponent<AudioListener>();

            CamaraOrbitaVitrina orbita = objeto.AddComponent<CamaraOrbitaVitrina>();
            orbita.objetivo = objetivo;
            orbita.distancia = distancia;
            orbita.altura = altura;
            orbita.amplitud = amplitud;
            orbita.segundosPorCiclo = ciclo;
            orbita.Colocar(orbita.anguloInicial);
        }

        static Transform CrearLuces()
        {
            Transform padre = new GameObject("Luces").transform;

            // Luna fria y suave de relleno, contraluz calido fuerte para recortar las siluetas contra
            // el fondo y un toque violeta. La luz principal de cada personaje es su foco.
            // El contraluz entra rasante: recorta los bordes de los personajes sin tenir el suelo.
            Light luna = CrearLuz("Luna", new Color(0.78f, 0.82f, 1f, 1f), 0.9f, new Vector3(42f, -35f, 0f), padre);
            luna.shadows = LightShadows.Soft;
            CrearLuz("Contraluz", new Color(1f, 0.72f, 0.45f, 1f), 2.4f, new Vector3(8f, 160f, 0f), padre);
            CrearLuz("Relleno", new Color(0.65f, 0.62f, 0.9f, 1f), 0.5f, new Vector3(10f, 60f, 0f), padre);
            return padre;
        }

        static Light CrearLuz(string nombre, Color color, float intensidad, Vector3 rotacion, Transform padre)
        {
            GameObject objeto = new GameObject(nombre);
            objeto.transform.SetParent(padre, false);
            objeto.transform.rotation = Quaternion.Euler(rotacion);
            Light luz = objeto.AddComponent<Light>();
            luz.type = LightType.Directional;
            luz.color = color;
            luz.intensity = intensidad;
            luz.shadows = LightShadows.None;
            return luz;
        }

        /// <summary>Foco de escenario desde arriba y delante, apuntando a la altura del pecho.</summary>
        static void CrearFoco(string nombre, Vector3 apoyo, float radio, Transform padre)
        {
            GameObject objeto = new GameObject(nombre);
            objeto.transform.SetParent(padre, false);
            Vector3 posicion = apoyo + new Vector3(0f, 4.6f, -1.8f);
            objeto.transform.position = posicion;
            Vector3 objetivo = apoyo + Vector3.up * 0.9f;
            objeto.transform.rotation = Quaternion.LookRotation(objetivo - posicion);

            float distancia = Vector3.Distance(posicion, objetivo);
            Light luz = objeto.AddComponent<Light>();
            luz.type = LightType.Spot;
            luz.color = ColorFoco;
            luz.intensity = 45f;
            luz.range = 11f;
            luz.spotAngle = Mathf.Clamp(2f * Mathf.Atan((radio + 0.6f) / distancia) * Mathf.Rad2Deg * 1.25f, 20f, 100f);
            luz.innerSpotAngle = luz.spotAngle * 0.55f;
            luz.shadows = LightShadows.Soft;
        }

        static void CrearSuelo()
        {
            // Grande para que la niebla se coma el borde y no se vea un horizonte. Con collider:
            // las rocas del angel y las astillas rebotan en el.
            GameObject suelo = GameObject.CreatePrimitive(PrimitiveType.Plane);
            suelo.name = "Suelo";
            suelo.transform.localScale = new Vector3(20f, 1f, 20f);
            suelo.GetComponent<Renderer>().sharedMaterial = MaterialSet("M_Suelo_Vitrina", new Color(0.19f, 0.185f, 0.22f, 1f), 0.18f, Color.black);
        }

        /// <summary>Pedestal con un aro que brilla y su foco. Devuelve la altura de su cara de arriba.</summary>
        static float CrearPedestal(Vector3 posicion, float radio, Transform padre, Transform luces)
        {
            const float alto = 0.12f;

            GameObject pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pedestal.name = "Pedestal";
            pedestal.transform.SetParent(padre, false);
            pedestal.transform.position = posicion + Vector3.up * (alto * 0.5f);
            pedestal.transform.localScale = new Vector3(radio * 2f, alto * 0.5f, radio * 2f);
            pedestal.GetComponent<Renderer>().sharedMaterial = MaterialSet("M_Pedestal_Vitrina", new Color(0.32f, 0.31f, 0.36f, 1f), 0.3f, Color.black);

            GameObject aro = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            aro.name = "Aro";
            aro.transform.SetParent(pedestal.transform, false);
            aro.transform.localPosition = Vector3.up * 0.9f;
            aro.transform.localScale = new Vector3(1.07f, 0.12f, 1.07f);
            aro.GetComponent<Renderer>().sharedMaterial = MaterialSet("M_Aro_Vitrina", Color.black, 0.2f, ColorAcento * 2.2f);
            Object.DestroyImmediate(aro.GetComponent<Collider>());

            // Sin luces no lleva foco propio (la galeria ilumina cada sala con uno grande).
            if (luces != null)
                CrearFoco("Foco", posicion, radio, luces);
            return posicion.y + alto;
        }

        static void CrearMarca(string nombre, Vector3 posicion, float radio, Transform padre)
        {
            GameObject marca = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marca.name = nombre;
            marca.transform.SetParent(padre, false);
            marca.transform.position = posicion + Vector3.up * 0.005f;
            marca.transform.localScale = new Vector3(radio * 2f, 0.005f, radio * 2f);
            marca.GetComponent<Renderer>().sharedMaterial = MaterialSet("M_Marca_Vitrina", new Color(0.26f, 0.25f, 0.3f, 1f), 0.15f, Color.black);
            Object.DestroyImmediate(marca.GetComponent<Collider>());
        }

        static void CrearVolumen()
        {
            GameObject objeto = new GameObject("Volumen global");
            Volume volumen = objeto.AddComponent<Volume>();
            volumen.isGlobal = true;
            volumen.sharedProfile = PerfilVolumen();
        }

        static T CrearVitrina<T>(string titulo, string subtitulo) where T : VitrinaPortafolio
        {
            GameObject objeto = new GameObject("Vitrina");
            T vitrina = objeto.AddComponent<T>();
            vitrina.titulo = titulo;
            vitrina.subtitulo = subtitulo;

            RotuloVitrina rotulo = objeto.AddComponent<RotuloVitrina>();
            rotulo.fuente = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(RutaFuente);
            rotulo.firma = "Technical Art  |  VFX  |  Animation";
            return vitrina;
        }

        /// <summary>
        /// Instancia el prefab (sin modificarlo), lo escala a la altura indicada y lo apoya sobre el
        /// punto. Los personajes de AnimationTest vienen a escalas distintas. Los pies van en el
        /// pivote del prefab, que es donde los deja su Idle al dar Play (la pose del rig puede
        /// tener los pies levantados); si el pivote esta lejos de la malla se usa su base.
        /// </summary>
        static GameObject ColocarPersonaje(string ruta, Vector3 apoyo, float altura, float giroY, Transform padre)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
            if (prefab == null)
            {
                Debug.LogWarning("GeneradorVitrinas: no se encontro " + ruta);
                return null;
            }

            GameObject instancia = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instancia.transform.SetParent(padre, false);
            instancia.transform.SetPositionAndRotation(apoyo, Quaternion.Euler(0f, giroY, 0f) * prefab.transform.rotation);

            if (!TryLimites(instancia, out Bounds limites) || limites.size.y <= 0.01f)
                return instancia;

            instancia.transform.localScale *= altura / limites.size.y;
            TryLimites(instancia, out limites);

            Vector3 pivote = instancia.transform.position;
            bool pivoteEnLosPies = Mathf.Abs(pivote.y - limites.min.y) < limites.size.y * 0.25f &&
                                   new Vector2(pivote.x - limites.center.x, pivote.z - limites.center.z).magnitude < limites.size.y * 0.25f;
            if (pivoteEnLosPies)
                instancia.transform.position = apoyo;
            else
                instancia.transform.position += new Vector3(apoyo.x - limites.center.x, apoyo.y - limites.min.y, apoyo.z - limites.center.z);
            return instancia;
        }

        static bool TryLimites(GameObject objeto, out Bounds limites)
        {
            limites = default;
            bool hay = false;
            foreach (Renderer render in objeto.GetComponentsInChildren<Renderer>())
            {
                if (!hay)
                {
                    limites = render.bounds;
                    hay = true;
                }
                else
                {
                    limites.Encapsulate(render.bounds);
                }
            }
            return hay;
        }

        #endregion

        #region Assets del set

        static Material MaterialSet(string nombre, Color color, float suavidad, Color emision)
        {
            // Los materiales del set son del generador: si ya existen se les vuelven a aplicar los valores.
            string ruta = CarpetaSet + "/" + nombre + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            bool nuevo = material == null;
            if (nuevo)
            {
                CrearCarpeta(CarpetaSet);
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            }

            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", suavidad);
            if (emision.maxColorComponent > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emision);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            else
            {
                material.DisableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.black);
            }

            if (nuevo)
                AssetDatabase.CreateAsset(material, ruta);
            else
                EditorUtility.SetDirty(material);
            return material;
        }

        static VolumeProfile PerfilVolumen()
        {
            string ruta = CarpetaSet + "/Volumen_Vitrina.asset";
            VolumeProfile perfil = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ruta);
            if (perfil == null)
            {
                CrearCarpeta(CarpetaSet);
                perfil = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(perfil, ruta);
            }

            // ACES como los volumenes de los niveles, bloom para que los VFX brillen igual que en el
            // juego, algo de exposicion para que el set no quede apagado y una vineta suave.
            Componente<Tonemapping>(perfil).mode.Override(TonemappingMode.ACES);
            Bloom bloom = Componente<Bloom>(perfil);
            bloom.threshold.Override(0.9f);
            bloom.intensity.Override(0.8f);
            bloom.scatter.Override(0.65f);
            Vignette vineta = Componente<Vignette>(perfil);
            vineta.intensity.Override(0.2f);
            vineta.smoothness.Override(0.5f);
            ColorAdjustments color = Componente<ColorAdjustments>(perfil);
            color.postExposure.Override(0.35f);
            color.contrast.Override(8f);

            EditorUtility.SetDirty(perfil);
            AssetDatabase.SaveAssets();
            return perfil;
        }

        // El componente del perfil, creado (y guardado dentro del asset) si todavia no esta.
        static T Componente<T>(VolumeProfile perfil) where T : VolumeComponent
        {
            if (perfil.TryGet(out T componente))
                return componente;

            componente = perfil.Add<T>(true);
            AssetDatabase.AddObjectToAsset(componente, perfil);
            return componente;
        }

        #endregion

        #region Utilidades

        static bool PuedeEscribir(string ruta, string descripcion = null)
        {
            if (!File.Exists(ruta))
                return true;

            if (Application.isBatchMode)
            {
                bool reemplazar = Array.IndexOf(Environment.GetCommandLineArgs(), "-reemplazar") >= 0;
                if (!reemplazar)
                    Debug.Log("GeneradorVitrinas: " + ruta + " ya existe, no se reemplaza (usa -reemplazar).");
                return reemplazar;
            }

            return EditorUtility.DisplayDialog(
                "Portafolio",
                "Ya existe " + (descripcion ?? Path.GetFileName(ruta)) + ".\nSi la reemplazas se pierden los cambios hechos a mano en esa escena.",
                "Reemplazar",
                "Cancelar");
        }

        static void Guardar(Scene escena, string ruta)
        {
            CrearCarpeta(CarpetaEscenas);
            EditorSceneManager.SaveScene(escena, ruta);
            Debug.Log("GeneradorVitrinas: escena creada en " + ruta);
        }

        static void CrearCarpeta(string ruta)
        {
            if (AssetDatabase.IsValidFolder(ruta))
                return;

            string padre = Path.GetDirectoryName(ruta).Replace('\\', '/');
            CrearCarpeta(padre);
            AssetDatabase.CreateFolder(padre, Path.GetFileName(ruta));
        }

        static void Capturar(Camera camara, string archivo, int ancho, int alto)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(archivo));

            RenderTexture destino = new RenderTexture(ancho, alto, 24);
            RenderTexture anterior = RenderTexture.active;
            RenderTexture destinoAnterior = camara.targetTexture;
            camara.targetTexture = destino;
            camara.Render();

            RenderTexture.active = destino;
            Texture2D imagen = new Texture2D(ancho, alto, TextureFormat.RGB24, false);
            imagen.ReadPixels(new Rect(0f, 0f, ancho, alto), 0, 0);
            imagen.Apply();

            camara.targetTexture = destinoAnterior;
            RenderTexture.active = anterior;
            File.WriteAllBytes(archivo, imagen.EncodeToPNG());
            Object.DestroyImmediate(destino);
            Object.DestroyImmediate(imagen);
            Debug.Log("GeneradorVitrinas: captura guardada en " + archivo);
        }

        static string Argumento(string nombre)
        {
            string[] argumentos = Environment.GetCommandLineArgs();
            int indice = Array.IndexOf(argumentos, nombre);
            return indice >= 0 && indice + 1 < argumentos.Length ? argumentos[indice + 1] : null;
        }

        #endregion
    }
}
