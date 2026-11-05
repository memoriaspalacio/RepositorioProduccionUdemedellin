using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UdeM.Portafolio
{
    /// <summary>
    /// Titulo de la pieza arriba de la pantalla y una firma abajo, con el acabado de caricatura de
    /// las calificaciones del juego (TextoFlotante.EstiloCaricatura), para que cada clip salga con
    /// su nombre puesto. Crea su propio Canvas al mostrarse; centrado sirve igual en 16:9 y 9:16.
    /// </summary>
    public class RotuloVitrina : MonoBehaviour
    {
        public TMP_FontAsset fuente;
        [Tooltip("Linea pequena abajo, por ejemplo tu nombre y cargo.")]
        public string firma = "Arte tecnico · VFX · Animacion";
        public Color colorArriba = new Color(1f, 0.94f, 0.66f, 1f);
        public Color colorAbajo = new Color(1f, 0.6f, 0.24f, 1f);
        [Min(0f)] public float duracionEntrada = 0.45f;

        [Header("Posicion del titulo")]
        [Tooltip("Punto de la pantalla: (0.5, 1) arriba al centro, (1, 1) arriba a la derecha. El texto se alinea hacia ese lado.")]
        public Vector2 anclaTitulo = new Vector2(0.5f, 1f);
        public Vector2 posicionTitulo = new Vector2(0f, -70f);

        Canvas canvas;
        RectTransform rectTitulo;
        float inicio = -100f;

        public void Mostrar(string titulo, string subtitulo)
        {
            if (canvas == null)
                Crear();

            rectTitulo.GetComponent<TMP_Text>().text = titulo;
            rectTitulo.GetChild(0).GetComponent<TMP_Text>().text = subtitulo;
            inicio = Time.unscaledTime;
        }

        public void Alternar()
        {
            if (canvas == null)
                return;
            canvas.enabled = !canvas.enabled;
            if (canvas.enabled)
                inicio = Time.unscaledTime;
        }

        void Crear()
        {
            GameObject objeto = new GameObject("Rotulo vitrina", typeof(RectTransform));
            objeto.transform.SetParent(transform, false);
            canvas = objeto.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            CanvasScaler escalador = objeto.AddComponent<CanvasScaler>();
            escalador.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            escalador.referenceResolution = new Vector2(1920f, 1080f);
            escalador.matchWidthOrHeight = 0.5f;

            // El subtitulo cuelga del titulo con el mismo lado de alineacion.
            rectTitulo = CrearTexto(objeto.transform, "Titulo", 64f, 0.32f, anclaTitulo, posicionTitulo, anclaTitulo.x);
            TMP_Text sub = CrearTexto(rectTitulo, "Subtitulo", 30f, 0.22f, new Vector2(anclaTitulo.x, 0f), new Vector2(0f, -28f), anclaTitulo.x).GetComponent<TMP_Text>();
            TextoFlotante.Degradado(sub, Color.white, new Color(0.82f, 0.8f, 0.92f, 1f));

            if (!string.IsNullOrEmpty(firma))
            {
                RectTransform rectFirma = CrearTexto(objeto.transform, "Firma", 26f, 0.22f, new Vector2(0.5f, 0f), new Vector2(0f, 40f));
                TMP_Text textoFirma = rectFirma.GetComponent<TMP_Text>();
                textoFirma.text = firma;
                TextoFlotante.Degradado(textoFirma, Color.white, new Color(0.82f, 0.8f, 0.92f, 1f));
            }
        }

        // 'lado' 0 = izquierda, 0.5 = centro, 1 = derecha: es el pivote horizontal y la alineacion.
        RectTransform CrearTexto(Transform padre, string nombre, float tamano, float contorno, Vector2 ancla, Vector2 posicion, float lado = 0.5f)
        {
            GameObject objeto = new GameObject(nombre, typeof(RectTransform));
            RectTransform rect = (RectTransform)objeto.transform;
            rect.SetParent(padre, false);
            rect.anchorMin = ancla;
            rect.anchorMax = ancla;
            rect.pivot = new Vector2(lado, 0.5f);
            rect.sizeDelta = new Vector2(1600f, tamano * 1.4f);
            rect.anchoredPosition = posicion;

            TextMeshProUGUI tmp = objeto.AddComponent<TextMeshProUGUI>();
            tmp.font = fuente != null ? fuente : TMP_Settings.defaultFontAsset;
            tmp.fontSize = tamano;
            tmp.alignment = lado < 0.34f ? TextAlignmentOptions.Left : lado > 0.66f ? TextAlignmentOptions.Right : TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.raycastTarget = false;
            TextoFlotante.EstiloCaricatura(tmp, contorno);
            TextoFlotante.Degradado(tmp, colorArriba, colorAbajo);
            return rect;
        }

        void Update()
        {
            if (rectTitulo == null)
                return;

            // Entra con un rebote pequeno, como las calificaciones.
            float t = duracionEntrada > 0f ? Mathf.Clamp01((Time.unscaledTime - inicio) / duracionEntrada) : 1f;
            float rebote = 1f + Mathf.Sin(t * Mathf.PI) * 0.12f * (1f - t);
            rectTitulo.localScale = Vector3.one * (Mathf.SmoothStep(0.6f, 1f, t) * rebote);
        }
    }
}
