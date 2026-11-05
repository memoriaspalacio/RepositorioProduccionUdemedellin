using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UdeM.Portafolio
{
    /// <summary>
    /// Los elementos de la interfaz del juego por separado, cada uno solo y grande sobre un fondo
    /// neutro, con el ritmo de verdad funcionando debajo (RitmoVitrina). Es el mismo HUD del juego:
    /// para aislar un bloque se apagan los demas con un CanvasGroup y se escala y centra el HUD
    /// entero, asi el corazon y la pista siguen alineados y sus efectos salen donde deben.
    ///   - 1 a 7 cambian de pagina; Q, E y F son las acciones de cada pagina.
    ///   - Z, X, C, V, B y 0 eligen como pulsa el bot (perfecto, bueno, malo, dejar pasar, mezcla, apagado).
    /// </summary>
    [RequireComponent(typeof(RitmoVitrina))]
    public class Vitrina06InterfazAislada : VitrinaPortafolio
    {
        [Header("Fondo de las paginas aisladas")]
        public Color colorFondo = new Color(0.14f, 0.13f, 0.19f, 1f);
        public Color colorBrillo = new Color(0.55f, 0.42f, 0.85f, 0.45f);

        [Header("Encuadre del bloque")]
        [Tooltip("Parte del alto de la pantalla que ocupa el bloque aislado.")]
        [Range(0.2f, 0.9f)] public float ocupacionAlto = 0.5f;
        [Tooltip("Parte del ancho de la pantalla que puede ocupar como mucho.")]
        [Range(0.2f, 0.9f)] public float ocupacionAncho = 0.6f;
        [Min(1f)] public float escalaMaxima = 2.6f;

        class Accion
        {
            public string nombre;
            public Action ejecutar;
        }

        class Pagina
        {
            public string titulo;
            public string subtitulo;
            public bool fondo;
            public bool hudCompleto;
            public RectTransform[] bloques = new RectTransform[0];
            public Accion[] acciones = new Accion[3];
        }

        static readonly Key[] TeclasAccion = { Key.Q, Key.E, Key.F };

        RitmoVitrina ritmo;
        readonly List<Pagina> paginas = new List<Pagina>();
        int actual = -1;

        RectTransform hud;
        Vector2 posicionHud;
        Vector3 escalaHud;
        RectTransform corazon;
        RectTransform pista;
        RectTransform reloj;
        RectTransform vida;
        Canvas fondo;
        bool panelVisible;

        protected override void Preparar()
        {
            ritmo = GetComponent<RitmoVitrina>();
            BuscarHud();
            CrearPaginas();

            for (int i = 0; i < paginas.Count; i++)
            {
                int indice = i;
                Registrar(TeclaNumero(i + 1), "Ver: " + paginas[i].titulo, () => IrA(indice), () => actual == indice);
            }
            for (int a = 0; a < TeclasAccion.Length; a++)
            {
                int indice = a;
                Registrar(TeclasAccion[a], () => AccionActual(indice)?.nombre, () => AccionActual(indice)?.ejecutar());
            }

            RegistrarBot(Key.Z, "Bot: perfecto", RitmoVitrina.Calidad.Perfecto);
            RegistrarBot(Key.X, "Bot: bueno", RitmoVitrina.Calidad.Bueno);
            RegistrarBot(Key.C, "Bot: malo", RitmoVitrina.Calidad.Malo);
            RegistrarBot(Key.V, "Bot: dejar pasar notas", RitmoVitrina.Calidad.Fallar);
            RegistrarBot(Key.B, "Bot: mezcla", RitmoVitrina.Calidad.Mezcla);
            RegistrarBot(Key.Digit0, "Bot apagado", RitmoVitrina.Calidad.Apagado);

            panelVisible = mostrarPanel;
            IrA(0);
        }

        public override List<Key> AccionesMiniatura => new List<Key> { Key.Q };
        public override float EsperaMiniatura => 0.35f;

        void RegistrarBot(Key tecla, string nombre, RitmoVitrina.Calidad calidad)
        {
            Registrar(tecla, nombre, () => ritmo.Bot(calidad), () => ritmo.bot == calidad);
        }

        Accion AccionActual(int indice)
        {
            return actual >= 0 ? paginas[actual].acciones[indice] : null;
        }

        protected override void Reiniciar()
        {
            ritmo.Bot(RitmoVitrina.Calidad.Mezcla);
            IrA(0);
        }

        /// <summary>Para el verificador: cada pagina seguida de sus acciones.</summary>
        public override List<Key> SecuenciaDePrueba
        {
            get
            {
                List<Key> secuencia = new List<Key>();
                for (int i = 0; i < paginas.Count; i++)
                {
                    secuencia.Add(TeclaNumero(i + 1));
                    for (int a = 0; a < TeclasAccion.Length; a++)
                    {
                        if (paginas[i].acciones[a] != null)
                            secuencia.Add(TeclasAccion[a]);
                    }
                }
                return secuencia;
            }
        }

        #region HUD

        void BuscarHud()
        {
            HeartBeatFeedback latido = FindFirstObjectByType<HeartBeatFeedback>();
            BeatTrackLane carril = FindFirstObjectByType<BeatTrackLane>();
            corazon = latido != null ? latido.transform as RectTransform : null;
            pista = carril != null ? carril.transform as RectTransform : null;
            vida = ritmo.jugador != null && ritmo.jugador.lifeBar != null ? ritmo.jugador.lifeBar.transform as RectTransform : null;

            hud = corazon != null ? corazon.parent as RectTransform : null;
            if (hud == null)
            {
                Debug.LogWarning("Vitrina06InterfazAislada: no se encontro el HUD (Gametime UI) en la escena.", this);
                return;
            }

            posicionHud = hud.anchoredPosition;
            escalaHud = hud.localScale;
            foreach (RectTransform hijo in hud)
            {
                if (hijo.name == "EstadisticaTiempo")
                    reloj = hijo;
            }
        }

        void CrearPaginas()
        {
            paginas.Add(new Pagina
            {
                titulo = "Corazon y calificaciones",
                subtitulo = "Late con el ritmo y reacciona a cada golpe",
                fondo = true,
                bloques = new[] { corazon },
                acciones = new[] { new Accion { nombre = "Golpe fuera de ritmo", ejecutar = ritmo.GolpeAlAire }, null, null }
            });
            paginas.Add(new Pagina
            {
                titulo = "Pista de ritmo",
                subtitulo = "Las notas viajan hacia el corazon",
                fondo = true,
                bloques = new[] { pista, corazon },
                acciones = new[] { new Accion { nombre = "Golpe fuera de ritmo", ejecutar = ritmo.GolpeAlAire }, null, null }
            });
            paginas.Add(new Pagina
            {
                titulo = "Cronometro vivo",
                subtitulo = "Gana, pierde y se pone nervioso",
                fondo = true,
                bloques = new[] { reloj },
                acciones = new[]
                {
                    new Accion { nombre = "Ganar 15 segundos", ejecutar = () => ritmo.GanarTiempo(15f) },
                    new Accion { nombre = "Perder 5 segundos", ejecutar = () => ritmo.PerderTiempo(5f) },
                    new Accion { nombre = "Tiempo critico", ejecutar = ritmo.TiempoCritico }
                }
            });
            paginas.Add(new Pagina
            {
                titulo = "Vida y recompensas",
                subtitulo = "Lo que se gana vuela hasta el HUD",
                fondo = true,
                bloques = new[] { vida, reloj },
                acciones = new[]
                {
                    new Accion { nombre = "Enemigo derrotado", ejecutar = ritmo.DerrotarEnemigo },
                    new Accion { nombre = "Recibir dano", ejecutar = ritmo.RecibirDano },
                    new Accion { nombre = "Curar", ejecutar = ritmo.Curar }
                }
            });
            paginas.Add(new Pagina
            {
                titulo = "Indicador de suelo y golpe",
                subtitulo = "El ritmo bajo los pies del personaje",
                fondo = false,
                acciones = new[]
                {
                    new Accion { nombre = "Recibir dano", ejecutar = ritmo.RecibirDano },
                    new Accion { nombre = "Golpe fuera de ritmo", ejecutar = ritmo.GolpeAlAire },
                    null
                }
            });
            paginas.Add(new Pagina
            {
                titulo = "Transiciones",
                subtitulo = "Entre escenas y cinematicas",
                fondo = true,
                acciones = new[]
                {
                    new Accion { nombre = "Transicion del corazon", ejecutar = ritmo.TransicionCorazon },
                    new Accion { nombre = "Fundido de inicio", ejecutar = ritmo.FundidoDeInicio },
                    null
                }
            });
            paginas.Add(new Pagina
            {
                titulo = "HUD completo",
                subtitulo = "Todo junto, como en el juego",
                fondo = false,
                hudCompleto = true,
                acciones = new[]
                {
                    new Accion { nombre = "Recibir dano", ejecutar = ritmo.RecibirDano },
                    new Accion { nombre = "Enemigo derrotado", ejecutar = ritmo.DerrotarEnemigo },
                    new Accion { nombre = "Tiempo critico", ejecutar = ritmo.TiempoCritico }
                }
            });
        }

        void IrA(int indice)
        {
            actual = Mathf.Clamp(indice, 0, paginas.Count - 1);
            Pagina pagina = paginas[actual];
            CambiarRotulo(pagina.titulo, pagina.subtitulo);
            MostrarFondo(pagina.fondo);
            Aislar(pagina);
        }

        void Aislar(Pagina pagina)
        {
            if (hud == null)
                return;

            hud.localScale = escalaHud;
            hud.anchoredPosition = posicionHud;

            List<RectTransform> visibles = new List<RectTransform>();
            foreach (RectTransform bloque in pagina.bloques)
            {
                if (bloque != null)
                    visibles.Add(bloque);
            }

            // Las calificaciones viven en un hermano del corazon: van con el.
            foreach (RectTransform hijo in hud)
            {
                bool visible = pagina.hudCompleto || visibles.Contains(hijo) ||
                               (hijo.name == "EfectosCalificacion" && visibles.Contains(corazon));
                if (hijo.name == "EstadisticaEnemigos")
                    visible = false;

                CanvasGroup grupo = hijo.GetComponent<CanvasGroup>();
                if (grupo == null)
                    grupo = hijo.gameObject.AddComponent<CanvasGroup>();
                grupo.alpha = visible ? 1f : 0f;
                grupo.blocksRaycasts = visible;
            }

            if (pagina.hudCompleto || visibles.Count == 0)
                return;

            // Limites de los bloques en el espacio del HUD, escala para que llenen la parte pedida
            // de la pantalla y traslado para que su centro quede en el centro (corrido a la derecha
            // si el panel de teclas esta a la vista).
            Bounds limites = Limites(visibles);
            RectTransform lienzo = hud.GetComponentInParent<Canvas>().rootCanvas.transform as RectTransform;
            float factor = Mathf.Min(escalaMaxima,
                ocupacionAlto * lienzo.rect.height / Mathf.Max(1f, limites.size.y),
                ocupacionAncho * lienzo.rect.width / Mathf.Max(1f, limites.size.x));
            hud.localScale = escalaHud * factor;

            Vector3 centroBloques = hud.TransformPoint(limites.center);
            Vector2 centroPantalla = lienzo.rect.center + new Vector2(mostrarPanel ? lienzo.rect.width * 0.1f : 0f, -lienzo.rect.height * 0.03f);
            Vector3 destino = lienzo.TransformPoint(centroPantalla);
            hud.position += destino - centroBloques;
        }

        Bounds Limites(List<RectTransform> bloques)
        {
            Vector3[] esquinas = new Vector3[4];
            Bounds limites = default;
            bool hay = false;
            foreach (RectTransform bloque in bloques)
            {
                bloque.GetWorldCorners(esquinas);
                foreach (Vector3 esquina in esquinas)
                {
                    Vector3 local = hud.InverseTransformPoint(esquina);
                    if (!hay)
                        limites = new Bounds(local, Vector3.zero);
                    else
                        limites.Encapsulate(local);
                    hay = true;
                }
            }
            return limites;
        }

        // Fondo neutro por debajo del HUD y por encima del nivel: tapa el mundo en las paginas aisladas.
        void MostrarFondo(bool visible)
        {
            if (fondo == null)
            {
                if (!visible || hud == null)
                    return;
                CrearFondo();
            }
            fondo.enabled = visible;
        }

        void CrearFondo()
        {
            Canvas raizHud = hud.GetComponentInParent<Canvas>().rootCanvas;

            GameObject objeto = new GameObject("Fondo vitrina UI", typeof(RectTransform));
            objeto.transform.SetParent(transform, false);
            fondo = objeto.AddComponent<Canvas>();
            fondo.renderMode = RenderMode.ScreenSpaceOverlay;
            fondo.sortingOrder = raizHud.sortingOrder - 1;
            CanvasScaler escalador = objeto.AddComponent<CanvasScaler>();
            escalador.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            escalador.referenceResolution = new Vector2(1920f, 1080f);
            escalador.matchWidthOrHeight = 0.5f;

            Image color = CrearImagen(objeto.transform, "Color", null, colorFondo);
            color.rectTransform.anchorMin = Vector2.zero;
            color.rectTransform.anchorMax = Vector2.one;
            color.rectTransform.sizeDelta = Vector2.zero;

            Image brillo = CrearImagen(objeto.transform, "Brillo", SpritesProcedurales.BrilloRadial, colorBrillo);
            brillo.rectTransform.anchorMin = brillo.rectTransform.anchorMax = new Vector2(0.55f, 0.47f);
            brillo.rectTransform.sizeDelta = new Vector2(1500f, 1100f);
        }

        static Image CrearImagen(Transform padre, string nombre, Sprite sprite, Color color)
        {
            GameObject objeto = new GameObject(nombre, typeof(RectTransform));
            objeto.transform.SetParent(padre, false);
            Image imagen = objeto.AddComponent<Image>();
            imagen.sprite = sprite;
            imagen.color = color;
            imagen.raycastTarget = false;
            return imagen;
        }

        #endregion

        protected override void Update()
        {
            base.Update();

            // Con el panel oculto (H) el bloque vuelve al centro exacto.
            if (panelVisible != mostrarPanel)
            {
                panelVisible = mostrarPanel;
                if (actual >= 0)
                    Aislar(paginas[actual]);
            }
        }
    }
}
