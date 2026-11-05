using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UdeM.Portafolio
{
    /// <summary>
    /// Base de las escenas de portafolio. Cada vitrina registra sus acciones (tecla y nombre) y
    /// esta clase las dispara, dibuja el panel de ayuda y maneja lo comun a todas:
    ///   - R reinicia la vitrina, T alterna camara lenta, N oculta el rotulo y H oculta el panel
    ///     para grabar limpio.
    ///   - F enfoca por turnos lo que la vitrina ofrece (PuntosDeFoco), G vuelve al plano general y
    ///     P detiene o reanuda el paneo de la camara.
    /// No depende de BeatManager, del grid ni de ningun otro sistema del juego: los efectos se
    /// llaman directo por su API, asi la escena no puede romper nada de los niveles.
    /// </summary>
    public abstract class VitrinaPortafolio : MonoBehaviour
    {
        struct Accion
        {
            public Key tecla;
            public string nombre;
            public Action ejecutar;
            public Func<bool> activo;
            // Nombre que cambia (por ejemplo segun la pagina); si devuelve null la accion no esta.
            public Func<string> nombreDinamico;

            public string Nombre => nombreDinamico != null ? nombreDinamico() : nombre;
        }

        [Header("Rotulo")]
        public string titulo = "";
        [TextArea] public string subtitulo = "";

        [Header("Grabacion")]
        [Tooltip("Velocidad del juego con la camara lenta activada (tecla T).")]
        [Range(0.05f, 1f)] public float escalaCamaraLenta = 0.3f;
        [Tooltip("Panel con las teclas en la esquina (tecla H).")]
        public bool mostrarPanel = true;
        [Tooltip("Panel arriba a la izquierda en vez de abajo (para no tapar un HUD de abajo).")]
        public bool panelArriba;
        [Tooltip("Pixeles (a 1080p) que se separa el panel de su borde, por ejemplo para no tapar una barra de vida.")]
        [Min(0f)] public float separacionPanel;
        [Range(12f, 28f)] public float tamanoPanel = 20f;

        /// <summary>Algo que la camara puede enfocar: su base en el suelo y su alto.</summary>
        protected struct Foco
        {
            public string nombre;
            public Vector3 apoyo;
            public float alto;
        }

        readonly List<Accion> acciones = new List<Accion>();
        readonly List<Foco> focos = new List<Foco>();
        int indiceFoco = -1;
        string nombreFoco;
        RotuloVitrina rotulo;
        bool camaraLenta;
        GUIStyle estiloTexto;
        GUIStyle estiloCaja;
        GUIStyle estiloBoton;

        /// <summary>Registra las acciones y arma lo que la vitrina necesita. Se llama una vez.</summary>
        protected abstract void Preparar();

        /// <summary>Vuelve todo al estado inicial (tecla R).</summary>
        protected abstract void Reiniciar();

        /// <summary>Con 'activo' el panel muestra si la opcion esta encendida o apagada.</summary>
        protected void Registrar(Key tecla, string nombre, Action ejecutar, Func<bool> activo = null)
        {
            acciones.Add(new Accion { tecla = tecla, nombre = nombre, ejecutar = ejecutar, activo = activo });
        }

        /// <summary>Accion cuyo nombre se pide cada vez; si es null la tecla no hace nada y no sale en el panel.</summary>
        protected void Registrar(Key tecla, Func<string> nombre, Action ejecutar, Func<bool> activo = null)
        {
            acciones.Add(new Accion { tecla = tecla, nombreDinamico = nombre, ejecutar = ejecutar, activo = activo });
        }

        /// <summary>Teclas registradas, en el orden del panel (las usa el verificador del editor).</summary>
        public List<Key> Teclas
        {
            get
            {
                List<Key> teclas = new List<Key>();
                for (int i = 0; i < acciones.Count; i++)
                    teclas.Add(acciones[i].tecla);
                return teclas;
            }
        }

        /// <summary>Teclas que prueba el verificador del editor, en orden (por defecto todas).</summary>
        public virtual List<Key> SecuenciaDePrueba => Teclas;

        /// <summary>Teclas que se pulsan antes de sacar la miniatura del menu (el momento mas vistoso).</summary>
        public virtual List<Key> AccionesMiniatura => new List<Key>();

        /// <summary>Segundos entre esas teclas y la captura de la miniatura.</summary>
        public virtual float EsperaMiniatura => 1.5f;

        /// <summary>Cambia el titulo y el subtitulo del rotulo (por ejemplo al cambiar de pagina).</summary>
        protected void CambiarRotulo(string nuevoTitulo, string nuevoSubtitulo)
        {
            titulo = nuevoTitulo;
            subtitulo = nuevoSubtitulo;
            if (rotulo != null)
                rotulo.Mostrar(titulo, subtitulo);
        }

        /// <summary>Hace lo mismo que pulsar la tecla. Devuelve false si la vitrina no la usa.</summary>
        public bool EjecutarAccion(Key tecla)
        {
            for (int i = 0; i < acciones.Count; i++)
            {
                if (acciones[i].tecla == tecla && acciones[i].Nombre != null)
                {
                    acciones[i].ejecutar();
                    return true;
                }
            }
            return false;
        }

        protected CamaraOrbitaVitrina Camara { get; private set; }

        /// <summary>Linea al pie del panel con los controles de la camara (null = sin linea).</summary>
        protected virtual string AyudaCamara => Camara != null ? "Rueda: acercar   Clic derecho: girar" : null;

        /// <summary>
        /// Llena la lista con lo que se puede enfocar con F. Se pide en cada pulsacion, asi sigue a
        /// los personajes aunque se muevan o reaparezcan.
        /// </summary>
        protected virtual void PuntosDeFoco(List<Foco> lista) { }

        protected virtual void Start()
        {
            Camara = Camera.main != null ? Camera.main.GetComponent<CamaraOrbitaVitrina>() : null;

            Preparar();
            if (Camara != null)
            {
                Registrar(Key.F, "Enfocar siguiente", EnfocarSiguiente);
                Registrar(Key.G, "Plano general", PlanoGeneral);
                Registrar(Key.P, "Paneo de camara", () => Camara.orbitar = !Camara.orbitar, () => Camara.orbitar);
            }
            Registrar(Key.M, "Volver al menu", CargadorEscenas.VolverAlMenu);
            Registrar(Key.R, "Reiniciar", Reiniciar);
            Registrar(Key.T, "Camara lenta", AlternarCamaraLenta);
            Registrar(Key.N, "Mostrar / ocultar rotulo", () => { if (rotulo != null) rotulo.Alternar(); });
            Registrar(Key.H, "Ocultar este panel", () => mostrarPanel = !mostrarPanel);

            rotulo = GetComponent<RotuloVitrina>();
            if (rotulo != null)
                rotulo.Mostrar(titulo, subtitulo);
        }

        protected virtual void Update()
        {
            Keyboard teclado = Keyboard.current;
            if (teclado == null)
                return;

            for (int i = 0; i < acciones.Count; i++)
            {
                if (teclado[acciones[i].tecla].wasPressedThisFrame && acciones[i].Nombre != null)
                    acciones[i].ejecutar();
            }
        }

        void EnfocarSiguiente()
        {
            focos.Clear();
            PuntosDeFoco(focos);
            if (focos.Count == 0)
                return;

            indiceFoco = (indiceFoco + 1) % focos.Count;
            EnfocarEn(focos[indiceFoco]);
        }

        protected void EnfocarEn(Foco foco)
        {
            if (Camara == null)
                return;

            nombreFoco = foco.nombre;
            Camara.Enfocar(foco.apoyo + Vector3.up * (foco.alto * 0.55f), foco.alto * 1.6f + 1.4f, foco.alto * 0.25f);
        }

        protected void PlanoGeneral()
        {
            indiceFoco = -1;
            nombreFoco = null;
            if (Camara != null)
                Camara.VolverAlPlano();
        }

        /// <summary>Foco con el tamano real de lo que se ve del objeto (sus renderers encendidos).</summary>
        protected static Foco FocoDe(string nombre, GameObject objeto, float altoPorDefecto = 1.9f)
        {
            Foco foco = new Foco { nombre = nombre, apoyo = objeto.transform.position, alto = altoPorDefecto };
            bool hay = false;
            Bounds limites = default;
            foreach (Renderer render in objeto.GetComponentsInChildren<Renderer>())
            {
                if (!render.enabled || render is ParticleSystemRenderer || render is TrailRenderer || render is LineRenderer)
                    continue;
                if (!hay)
                    limites = render.bounds;
                else
                    limites.Encapsulate(render.bounds);
                hay = true;
            }

            if (hay && limites.size.y > 0.05f)
            {
                foco.apoyo = new Vector3(limites.center.x, limites.min.y, limites.center.z);
                foco.alto = limites.size.y;
            }
            return foco;
        }

        void AlternarCamaraLenta()
        {
            camaraLenta = !camaraLenta;
            Time.timeScale = camaraLenta ? escalaCamaraLenta : 1f;
        }

        protected virtual void OnDisable()
        {
            // La camara lenta no debe seguir puesta al salir de Play o cambiar de escena.
            Time.timeScale = 1f;
        }

        void OnGUI()
        {
            if (!mostrarPanel)
                return;

            float escala = Screen.height / 1080f;
            if (estiloTexto == null)
            {
                estiloTexto = new GUIStyle(GUI.skin.label) { richText = true };
                estiloCaja = new GUIStyle(GUI.skin.box);
                estiloBoton = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold };
            }
            estiloTexto.fontSize = Mathf.RoundToInt(tamanoPanel * escala);

            // Boton a la vista para volver al menu (se oculta con el panel, para grabar limpio).
            estiloBoton.fontSize = Mathf.RoundToInt(22f * escala);
            Vector2 tamanoBoton = new Vector2(290f, 56f) * escala;
            Rect boton = new Rect(Screen.width - tamanoBoton.x - 20f * escala, Screen.height - tamanoBoton.y - 20f * escala, tamanoBoton.x, tamanoBoton.y);
            if (GUI.Button(boton, "<  Menu principal  (M)", estiloBoton))
                CargadorEscenas.VolverAlMenu();

            if (acciones.Count == 0)
                return;

            float alto = estiloTexto.fontSize * 1.45f;
            float ancho = 470f * escala;
            float margen = 20f * escala;
            string ayuda = AyudaCamara;
            int visibles = 0;
            for (int i = 0; i < acciones.Count; i++)
            {
                if (acciones[i].Nombre != null)
                    visibles++;
            }
            int lineas = visibles + (ayuda != null ? 1 : 0);
            float altoCaja = alto * (lineas + 1.4f);
            float separacion = separacionPanel * escala;
            float yCaja = panelArriba ? margen + separacion : Screen.height - margen - separacion - altoCaja;
            Rect caja = new Rect(margen, yCaja, ancho * tamanoPanel / 20f, altoCaja);
            GUI.Box(caja, GUIContent.none, estiloCaja);

            float y = caja.y + alto * 0.35f;
            string estado = camaraLenta ? "  <color=#ffcf66>(camara lenta)</color>" : "";
            if (!string.IsNullOrEmpty(nombreFoco))
                estado += $"  <color=#9fc8ff>Foco: {nombreFoco}</color>";
            GUI.Label(new Rect(caja.x + margen * 0.6f, y, ancho, alto), "<b>Teclas</b>" + estado, estiloTexto);
            for (int i = 0; i < acciones.Count; i++)
            {
                string nombre = acciones[i].Nombre;
                if (nombre == null)
                    continue;

                y += alto;
                string marca = acciones[i].activo == null
                    ? ""
                    : acciones[i].activo() ? "  <color=#8fe38f>ON</color>" : "  <color=#e37b7b>OFF</color>";
                GUI.Label(new Rect(caja.x + margen * 0.6f, y, ancho, alto),
                    $"<color=#ffcf66><b>{NombreTecla(acciones[i].tecla)}</b></color>  {nombre}{marca}", estiloTexto);
            }

            if (ayuda != null)
            {
                y += alto;
                GUI.Label(new Rect(caja.x + margen * 0.6f, y, ancho, alto), $"<color=#b8b4c8>{ayuda}</color>", estiloTexto);
            }
        }

        static string NombreTecla(Key tecla)
        {
            if (tecla >= Key.Digit1 && tecla <= Key.Digit9)
                return ((int)(tecla - Key.Digit1) + 1).ToString();
            if (tecla == Key.Digit0)
                return "0";
            if (tecla == Key.Space)
                return "Espacio";
            return tecla.ToString();
        }

        /// <summary>Tecla del numero indicado (1 a 9, 0 para el 10).</summary>
        protected static Key TeclaNumero(int numero)
        {
            return numero >= 1 && numero <= 9 ? Key.Digit1 + (numero - 1) : Key.Digit0;
        }

        /// <summary>
        /// Punto desde donde "ataca" el golpe: al lado izquierdo de la camara, asi las particulas
        /// salen hacia la derecha del encuadre y se leen bien.
        /// </summary>
        protected static Vector3 PuntoAtacante(Vector3 objetivo)
        {
            Camera camara = Camera.main;
            Vector3 lado = camara != null ? camara.transform.right : Vector3.right;
            lado.y = 0f;
            if (lado.sqrMagnitude < 0.0001f)
                lado = Vector3.right;
            return objetivo - lado.normalized * 2f + Vector3.up;
        }
    }
}
