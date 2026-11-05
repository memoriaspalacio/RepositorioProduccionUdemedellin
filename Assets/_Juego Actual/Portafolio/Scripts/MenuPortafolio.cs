using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UdeM.Portafolio
{
    /// <summary>
    /// Menu del portafolio al estilo de los menus de DVD / Blu-ray: la miniatura de la escena elegida
    /// llena el fondo (oscurecida y con un zoom lento), un carrusel de tarjetas con numero de capitulo
    /// abajo y el titulo con su descripcion encima. Toda la interfaz se crea por codigo al empezar.
    ///   - Flechas, A / D o la rueda eligen; clic en una tarjeta la elige y otro clic la abre.
    ///   - Enter o Espacio abren la escena con un fundido a negro.
    /// Desde cada vitrina, M vuelve aqui y el menu recuerda cual se vio por ultima vez.
    /// </summary>
    public class MenuPortafolio : MonoBehaviour
    {
        [Serializable]
        public class Entrada
        {
            public string titulo = "Escena";
            public string categoria = "";
            [TextArea] public string descripcion = "";
            public string ruta = "";
            public Texture2D miniatura;
        }

        public List<Entrada> entradas = new List<Entrada>();

        [Header("Estilo")]
        public TMP_FontAsset fuenteTitulos;
        public TMP_FontAsset fuenteTexto;
        public string encabezado = "Portafolio";
        public string firma = "Technical Art  |  VFX  |  Animation";
        public Color colorAcento = new Color(1f, 0.72f, 0.32f, 1f);
        public Color colorFondo = new Color(0.03f, 0.028f, 0.05f, 1f);

        [Header("Carrusel")]
        public Vector2 tamanoTarjeta = new Vector2(380f, 214f);
        [Min(100f)] public float separacion = 420f;
        [Min(1f)] public float escalaElegida = 1.35f;

        // Se recuerda entre escenas (y se pierde al salir de Play).
        static int ultimaElegida;

        class Tarjeta
        {
            public RectTransform rect;
            public Image borde;
            public Image brillo;
            public CanvasGroup grupo;
            public TextMeshProUGUI nombre;
        }

        readonly List<Tarjeta> tarjetas = new List<Tarjeta>();
        RectTransform lienzo;
        RawImage fondoA;
        RawImage fondoB;
        bool fondoAActivo = true;
        float mezclaFondo = 1f;
        TextMeshProUGUI titulo;
        TextMeshProUGUI categoria;
        TextMeshProUGUI descripcion;
        TextMeshProUGUI contador;
        Image cortina;
        int elegida;
        float elegidaSuave;
        float inicio;
        bool cargando;

        void Awake()
        {
            // Si se viene de una vitrina con el reloj del juego, aqui no debe seguir corriendo.
            if (ControladorContrarreloj.Instancia != null)
            {
                Destroy(ControladorContrarreloj.Instancia.gameObject);
                ControladorContrarreloj.Instancia = null;
            }
            Time.timeScale = 1f;
        }

        void Start()
        {
            Crear();
            elegida = Mathf.Clamp(ultimaElegida, 0, Mathf.Max(0, entradas.Count - 1));
            elegidaSuave = elegida - 3f;
            inicio = Time.unscaledTime;
            Elegir(elegida, true);
        }

        #region Construccion

        void Crear()
        {
            GameObject objeto = new GameObject("Menu portafolio", typeof(RectTransform));
            objeto.transform.SetParent(transform, false);
            Canvas canvas = objeto.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler escalador = objeto.AddComponent<CanvasScaler>();
            escalador.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            escalador.referenceResolution = new Vector2(1920f, 1080f);
            escalador.matchWidthOrHeight = 0.5f;
            objeto.AddComponent<GraphicRaycaster>();
            lienzo = (RectTransform)objeto.transform;

            Image base0 = Imagen(lienzo, "Color", null, colorFondo);
            Estirar(base0.rectTransform);
            fondoA = FondoMiniatura("Fondo A");
            fondoB = FondoMiniatura("Fondo B");

            // Oscurece abajo (donde van el texto y el carrusel) y un poco arriba, como un menu de disco.
            Image sombraAbajo = Imagen(lienzo, "Sombra abajo", SpritesProcedurales.DegradadoSalida, new Color(0f, 0f, 0f, 0.78f));
            Anclar(sombraAbajo.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 330f), new Vector2(1080f * 0.62f, 1920f));
            sombraAbajo.rectTransform.localEulerAngles = new Vector3(0f, 0f, 90f);
            Image sombraArriba = Imagen(lienzo, "Sombra arriba", SpritesProcedurales.DegradadoSalida, new Color(0f, 0f, 0f, 0.45f));
            Anclar(sombraArriba.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(220f, 1920f));
            sombraArriba.rectTransform.localEulerAngles = new Vector3(0f, 0f, -90f);

            TextMeshProUGUI cabecera = Texto(lienzo, "Encabezado", encabezado.ToUpperInvariant(), fuenteTitulos, 44f, TextAlignmentOptions.Left);
            Anclar(cabecera.rectTransform, new Vector2(0f, 1f), new Vector2(70f, -70f), new Vector2(900f, 60f), new Vector2(0f, 0.5f));
            TextoFlotante.EstiloCaricatura(cabecera, 0.28f);
            TextoFlotante.Degradado(cabecera, new Color(1f, 0.95f, 0.75f, 1f), colorAcento);

            TextMeshProUGUI lema = Texto(lienzo, "Firma", firma, fuenteTexto, 24f, TextAlignmentOptions.Left);
            Anclar(lema.rectTransform, new Vector2(0f, 1f), new Vector2(72f, -118f), new Vector2(900f, 34f), new Vector2(0f, 0.5f));
            lema.color = new Color(0.86f, 0.84f, 0.95f, 0.85f);

            contador = Texto(lienzo, "Contador", "", fuenteTexto, 26f, TextAlignmentOptions.Right);
            Anclar(contador.rectTransform, new Vector2(1f, 1f), new Vector2(-70f, -76f), new Vector2(400f, 40f), new Vector2(1f, 0.5f));
            contador.color = new Color(1f, 1f, 1f, 0.7f);

            categoria = Texto(lienzo, "Categoria", "", fuenteTexto, 26f, TextAlignmentOptions.Left);
            Anclar(categoria.rectTransform, new Vector2(0f, 0f), new Vector2(110f, 610f), new Vector2(1400f, 36f), new Vector2(0f, 0.5f));
            categoria.color = colorAcento;

            titulo = Texto(lienzo, "Titulo", "", fuenteTitulos, 76f, TextAlignmentOptions.Left);
            Anclar(titulo.rectTransform, new Vector2(0f, 0f), new Vector2(104f, 545f), new Vector2(1600f, 90f), new Vector2(0f, 0.5f));
            TextoFlotante.EstiloCaricatura(titulo, 0.3f);

            descripcion = Texto(lienzo, "Descripcion", "", fuenteTexto, 28f, TextAlignmentOptions.TopLeft);
            Anclar(descripcion.rectTransform, new Vector2(0f, 0f), new Vector2(110f, 488f), new Vector2(1150f, 80f), new Vector2(0f, 1f));
            descripcion.textWrappingMode = TextWrappingModes.Normal;
            descripcion.color = new Color(0.9f, 0.89f, 0.96f, 1f);

            for (int i = 0; i < entradas.Count; i++)
                tarjetas.Add(CrearTarjeta(i));

            TextMeshProUGUI ayuda = Texto(lienzo, "Ayuda", "<  >  elegir      Enter  ver escena      M  volver al menu desde una escena", fuenteTexto, 22f, TextAlignmentOptions.Center);
            Anclar(ayuda.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 38f), new Vector2(1600f, 30f));
            ayuda.color = new Color(1f, 1f, 1f, 0.55f);

            cortina = Imagen(lienzo, "Cortina", null, Color.black);
            Estirar(cortina.rectTransform);
            cortina.raycastTarget = false;
        }

        Tarjeta CrearTarjeta(int indice)
        {
            Entrada entrada = entradas[indice];
            GameObject objeto = new GameObject("Tarjeta " + (indice + 1), typeof(RectTransform));
            RectTransform rect = (RectTransform)objeto.transform;
            rect.SetParent(lienzo, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.sizeDelta = tamanoTarjeta;
            CanvasGroup grupo = objeto.AddComponent<CanvasGroup>();

            Image brillo = Imagen(rect, "Brillo", SpritesProcedurales.BrilloRadial, new Color(colorAcento.r, colorAcento.g, colorAcento.b, 0f));
            Anclar(brillo.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, tamanoTarjeta * 1.9f);

            Image borde = Imagen(rect, "Borde", null, Color.white);
            Estirar(borde.rectTransform, -6f);

            RawImage miniatura = new GameObject("Miniatura", typeof(RectTransform)).AddComponent<RawImage>();
            miniatura.rectTransform.SetParent(rect, false);
            Estirar(miniatura.rectTransform);
            miniatura.texture = entrada.miniatura;
            miniatura.color = entrada.miniatura != null ? Color.white : new Color(0.15f, 0.14f, 0.2f, 1f);
            miniatura.raycastTarget = true;

            Image placa = Imagen(rect, "Placa numero", null, new Color(0f, 0f, 0f, 0.65f));
            Anclar(placa.rectTransform, new Vector2(0f, 1f), new Vector2(10f, -10f), new Vector2(62f, 40f), new Vector2(0f, 1f));
            TextMeshProUGUI numero = Texto(placa.rectTransform, "Numero", (indice + 1).ToString("00"), fuenteTexto, 26f, TextAlignmentOptions.Center);
            Estirar(numero.rectTransform);
            numero.color = colorAcento;

            TextMeshProUGUI nombre = Texto(rect, "Nombre", entrada.titulo, fuenteTexto, 22f, TextAlignmentOptions.Top);
            Anclar(nombre.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -22f), new Vector2(tamanoTarjeta.x + 40f, 30f));
            nombre.color = new Color(1f, 1f, 1f, 0.85f);

            Button boton = objeto.AddComponent<Button>();
            boton.targetGraphic = miniatura;
            boton.transition = Selectable.Transition.None;
            boton.onClick.AddListener(() =>
            {
                if (indice == elegida)
                    Abrir();
                else
                    Elegir(indice, false);
            });

            return new Tarjeta { rect = rect, borde = borde, brillo = brillo, grupo = grupo, nombre = nombre };
        }

        RawImage FondoMiniatura(string nombre)
        {
            RawImage imagen = new GameObject(nombre, typeof(RectTransform)).AddComponent<RawImage>();
            imagen.rectTransform.SetParent(lienzo, false);
            Estirar(imagen.rectTransform);
            imagen.raycastTarget = false;
            imagen.color = new Color(1f, 1f, 1f, 0f);
            return imagen;
        }

        Image Imagen(Transform padre, string nombre, Sprite sprite, Color color)
        {
            Image imagen = new GameObject(nombre, typeof(RectTransform)).AddComponent<Image>();
            imagen.rectTransform.SetParent(padre, false);
            imagen.sprite = sprite;
            imagen.color = color;
            imagen.raycastTarget = false;
            return imagen;
        }

        TextMeshProUGUI Texto(Transform padre, string nombre, string contenido, TMP_FontAsset fuente, float tamano, TextAlignmentOptions alineacion)
        {
            TextMeshProUGUI texto = new GameObject(nombre, typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            texto.rectTransform.SetParent(padre, false);
            texto.font = fuente != null ? fuente : TMP_Settings.defaultFontAsset;
            texto.fontSize = tamano;
            texto.alignment = alineacion;
            texto.textWrappingMode = TextWrappingModes.NoWrap;
            texto.overflowMode = TextOverflowModes.Overflow;
            texto.raycastTarget = false;
            texto.text = contenido;
            return texto;
        }

        static void Estirar(RectTransform rect, float margen = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(margen, margen);
            rect.offsetMax = new Vector2(-margen, -margen);
        }

        static void Anclar(RectTransform rect, Vector2 ancla, Vector2 posicion, Vector2 tamano, Vector2? pivote = null)
        {
            rect.anchorMin = rect.anchorMax = ancla;
            rect.pivot = pivote ?? new Vector2(0.5f, 0.5f);
            rect.sizeDelta = tamano;
            rect.anchoredPosition = posicion;
        }

        #endregion

        #region Navegacion

        void Update()
        {
            LeerEntrada();
            Animar();
        }

        void LeerEntrada()
        {
            if (cargando || entradas.Count == 0)
                return;

            Keyboard teclado = Keyboard.current;
            if (teclado != null)
            {
                if (teclado[Key.RightArrow].wasPressedThisFrame || teclado[Key.D].wasPressedThisFrame)
                    Elegir(elegida + 1, false);
                if (teclado[Key.LeftArrow].wasPressedThisFrame || teclado[Key.A].wasPressedThisFrame)
                    Elegir(elegida - 1, false);
                if (teclado[Key.Enter].wasPressedThisFrame || teclado[Key.NumpadEnter].wasPressedThisFrame || teclado[Key.Space].wasPressedThisFrame)
                    Abrir();
            }

            Mouse raton = Mouse.current;
            if (raton != null)
            {
                float rueda = raton.scroll.ReadValue().y;
                if (Mathf.Abs(rueda) > 0.01f)
                    Elegir(elegida + (rueda < 0f ? 1 : -1), false);
            }
        }

        /// <summary>Elige una entrada (da la vuelta en los extremos). Publico para el verificador.</summary>
        public void Elegir(int indice, bool alInstante)
        {
            if (entradas.Count == 0)
                return;

            elegida = (indice % entradas.Count + entradas.Count) % entradas.Count;
            ultimaElegida = elegida;
            Entrada entrada = entradas[elegida];

            titulo.text = entrada.titulo;
            TextoFlotante.Degradado(titulo, new Color(1f, 0.96f, 0.8f, 1f), colorAcento);
            categoria.text = entrada.categoria.ToUpperInvariant();
            descripcion.text = entrada.descripcion;
            contador.text = $"Escena {elegida + 1:00} / {entradas.Count:00}";

            // Fundido cruzado entre las dos capas del fondo.
            fondoAActivo = !fondoAActivo;
            RawImage nuevo = fondoAActivo ? fondoA : fondoB;
            nuevo.texture = entrada.miniatura;
            mezclaFondo = alInstante ? 1f : 0f;
        }

        public void Abrir()
        {
            if (cargando || entradas.Count == 0 || string.IsNullOrEmpty(entradas[elegida].ruta))
                return;
            StartCoroutine(Cargar(entradas[elegida].ruta));
        }

        IEnumerator Cargar(string ruta)
        {
            cargando = true;
            for (float t = 0f; t < 0.4f; t += Time.unscaledDeltaTime)
            {
                cortina.color = new Color(0f, 0f, 0f, t / 0.4f);
                yield return null;
            }
            cortina.color = Color.black;
            CargadorEscenas.Cargar(ruta);
        }

        void Animar()
        {
            float dt = Time.unscaledDeltaTime;
            float tiempo = Time.unscaledTime - inicio;
            elegidaSuave = Mathf.Lerp(elegidaSuave, elegida, 1f - Mathf.Exp(-9f * dt));

            // Fondo: fundido de la miniatura nueva y un zoom lento con algo de deriva, como un menu de disco.
            mezclaFondo = Mathf.MoveTowards(mezclaFondo, 1f, dt * 2.5f);
            RawImage activo = fondoAActivo ? fondoA : fondoB;
            RawImage viejo = fondoAActivo ? fondoB : fondoA;
            activo.color = new Color(0.88f, 0.88f, 0.92f, mezclaFondo);
            viejo.color = new Color(0.88f, 0.88f, 0.92f, 1f - mezclaFondo);
            float zoom = 1.06f + 0.04f * Mathf.Sin(tiempo * 0.12f);
            Vector2 deriva = new Vector2(Mathf.Sin(tiempo * 0.07f), Mathf.Cos(tiempo * 0.05f)) * 18f;
            foreach (RawImage capa in new[] { fondoA, fondoB })
            {
                capa.rectTransform.localScale = Vector3.one * zoom;
                capa.rectTransform.anchoredPosition = deriva;
            }

            // Cortina de entrada al abrir el menu.
            if (!cargando)
                cortina.color = new Color(0f, 0f, 0f, Mathf.Clamp01(1f - tiempo / 0.6f));

            for (int i = 0; i < tarjetas.Count; i++)
            {
                Tarjeta tarjeta = tarjetas[i];
                float distancia = i - elegidaSuave;
                float cercania = Mathf.Clamp01(1f - Mathf.Abs(distancia));
                float escala = Mathf.Lerp(0.82f, escalaElegida, cercania);
                // Las tarjetas se separan un poco mas alrededor de la elegida para dejarle sitio.
                float x = distancia * separacion + Mathf.Sign(distancia) * Mathf.Clamp01(Mathf.Abs(distancia)) * separacion * (escalaElegida - 1f) * 0.6f;

                tarjeta.rect.anchoredPosition = new Vector2(x, 250f + 18f * cercania);
                tarjeta.rect.localScale = Vector3.one * escala;
                tarjeta.grupo.alpha = Mathf.Lerp(0.45f, 1f, cercania) * Mathf.Clamp01(1.6f - Mathf.Abs(distancia) * 0.18f);
                tarjeta.borde.color = Color.Lerp(new Color(1f, 1f, 1f, 0.15f), colorAcento, cercania);
                float latido = 0.75f + 0.25f * Mathf.Sin(tiempo * 3f);
                tarjeta.brillo.color = new Color(colorAcento.r, colorAcento.g, colorAcento.b, 0.55f * cercania * latido);
                tarjeta.nombre.alpha = Mathf.Abs(distancia) < 3.5f ? 0.85f : 0f;

                // La elegida se dibuja encima de las demas.
                if (i == elegida)
                    tarjeta.rect.SetAsLastSibling();
            }
            cortina.transform.SetAsLastSibling();
        }

        #endregion

        /// <summary>Para el verificador: elegir la siguiente sin teclado.</summary>
        public int Cantidad => entradas.Count;
    }
}
