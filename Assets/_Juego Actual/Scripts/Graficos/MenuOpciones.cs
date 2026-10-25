using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Menu de opciones generico: dibuja por codigo cualquier lista de OpcionMenu (titulo,
/// filas con flechas, ayuda de la fila senalada y botones abajo). No necesita prefab ni
/// estar en la escena. Funciona con raton, tactil, teclado y mando: arriba y abajo eligen
/// fila, izquierda y derecha cambian el valor y Escape (o B) lo cierra.
/// </summary>
public class MenuOpciones : MonoBehaviour
{
    const float AnchoMaximo = 1180f;
    const float AltoMaximo = 980f;
    const float AltoFila = 66f;

    class Fila
    {
        public OpcionMenu opcion;
        public Image fondo;
        public TextMeshProUGUI nombre;
        public TextMeshProUGUI valor;
        public Button izquierda;
        public Button derecha;
    }

    /// <summary>Avisa al menu cuando el puntero (false) o la seleccion de teclado o mando (true) llegan a una fila.</summary>
    class AvisoFila : MonoBehaviour, IPointerEnterHandler, ISelectHandler
    {
        public Action<bool> alSenalar;

        public void OnPointerEnter(PointerEventData datos) => alSenalar?.Invoke(false);
        public void OnSelect(BaseEventData datos) => alSenalar?.Invoke(true);
    }

    public event Action AlCerrar;

    readonly List<Fila> filas = new List<Fila>();
    Func<string> estado;
    RectTransform panel;
    RectTransform vista;
    RectTransform contenido;
    TextMeshProUGUI textoEstado;
    TextMeshProUGUI textoAyuda;
    GameObject eventSystemPropio;
    Vector2Int pantalla;
    float siguienteEstado;

    /// <summary>
    /// Abre un menu. estado es una linea de informacion bajo el titulo (puede ser null) y
    /// botones son las acciones de abajo; "Cerrar" se agrega siempre.
    /// </summary>
    public static MenuOpciones Crear(string titulo, List<OpcionMenu> opciones, Func<string> estado, params (string texto, Action accion)[] botones)
    {
        Canvas canvas = ConstructorUI.CrearCanvas("Menu " + titulo, 5000);
        MenuOpciones menu = canvas.gameObject.AddComponent<MenuOpciones>();
        menu.estado = estado;
        menu.Construir(titulo, opciones, botones);
        return menu;
    }

    public void Cerrar()
    {
        AlCerrar?.Invoke();
        AlCerrar = null;

        if (eventSystemPropio != null)
            Destroy(eventSystemPropio);

        Destroy(gameObject);
    }

    #region Construccion

    void Construir(string titulo, List<OpcionMenu> opciones, (string texto, Action accion)[] botones)
    {
        eventSystemPropio = ConstructorUI.AsegurarEventSystem(transform);

        // Fondo oscuro que ademas bloquea los clics a lo que hay detras
        Image fondo = ConstructorUI.CrearImagen("Fondo", transform, new Color(0f, 0f, 0f, 0.6f));
        ConstructorUI.Llenar(fondo.rectTransform);

        Image imagenPanel = ConstructorUI.CrearImagen("Panel", transform, ConstructorUI.ColorPanel);
        Outline borde = imagenPanel.gameObject.AddComponent<Outline>();
        borde.effectColor = ConstructorUI.ColorBorde;
        borde.effectDistance = new Vector2(2f, -2f);
        panel = imagenPanel.rectTransform;
        AjustarTamano();

        TextMeshProUGUI textoTitulo = ConstructorUI.CrearTexto("Titulo", panel, titulo, 58f, ConstructorUI.ColorTitulo, TextAlignmentOptions.Center);
        Franja(textoTitulo.rectTransform, 18f, 76f, true);

        textoEstado = ConstructorUI.CrearTexto("Estado", panel, string.Empty, 24f, ConstructorUI.ColorTenue, TextAlignmentOptions.Center);
        Franja(textoEstado.rectTransform, 94f, 34f, true);

        CrearLista(140f, 196f);
        for (int i = 0; i < opciones.Count; i++)
        {
            if (opciones[i].EsSeccion)
                CrearSeccion(contenido, opciones[i]);
            else
                CrearFila(contenido, opciones[i]);
        }

        textoAyuda = ConstructorUI.CrearTexto("Ayuda", panel, string.Empty, 26f, ConstructorUI.ColorTenue, TextAlignmentOptions.TopLeft);
        textoAyuda.textWrappingMode = TextWrappingModes.Normal;
        textoAyuda.overflowMode = TextOverflowModes.Truncate;
        Franja(textoAyuda.rectTransform, 110f, 82f, false);

        CrearBotones(botones);
        Refrescar();

        if (filas.Count > 0)
        {
            MostrarAyuda(filas[0].opcion);
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(filas[0].derecha.gameObject);
        }
    }

    /// <summary>Lista con desplazamiento (y su barra) entre el encabezado y el pie del panel.</summary>
    void CrearLista(float arriba, float abajo)
    {
        RectTransform area = ConstructorUI.CrearRect("Lista", panel);
        area.anchorMin = Vector2.zero;
        area.anchorMax = Vector2.one;
        area.offsetMin = new Vector2(36f, abajo);
        area.offsetMax = new Vector2(-36f, -arriba);

        // La vista recorta las filas; la barra queda fuera de ella, a la derecha
        Image fondoVista = ConstructorUI.CrearImagen("Vista", area, new Color(0f, 0f, 0f, 0.001f));
        vista = fondoVista.rectTransform;
        ConstructorUI.Llenar(vista);
        vista.offsetMax = new Vector2(-22f, 0f);
        vista.gameObject.AddComponent<RectMask2D>();

        Image fondoBarra = ConstructorUI.CrearImagen("Barra", area, new Color(1f, 1f, 1f, 0.06f));
        RectTransform rectBarra = fondoBarra.rectTransform;
        rectBarra.anchorMin = new Vector2(1f, 0f);
        rectBarra.anchorMax = new Vector2(1f, 1f);
        rectBarra.pivot = new Vector2(1f, 0.5f);
        rectBarra.sizeDelta = new Vector2(10f, 0f);
        rectBarra.anchoredPosition = Vector2.zero;

        RectTransform zonaManija = ConstructorUI.CrearRect("Zona", rectBarra);
        ConstructorUI.Llenar(zonaManija);
        Image manija = ConstructorUI.CrearImagen("Manija", zonaManija, ConstructorUI.ColorBorde);
        ConstructorUI.Llenar(manija.rectTransform);

        Scrollbar barra = fondoBarra.gameObject.AddComponent<Scrollbar>();
        barra.handleRect = manija.rectTransform;
        barra.targetGraphic = manija;
        barra.direction = Scrollbar.Direction.BottomToTop;
        barra.navigation = new Navigation { mode = Navigation.Mode.None };

        contenido = ConstructorUI.CrearRect("Contenido", vista);
        contenido.anchorMin = new Vector2(0f, 1f);
        contenido.anchorMax = new Vector2(1f, 1f);
        contenido.pivot = new Vector2(0.5f, 1f);
        contenido.offsetMin = Vector2.zero;
        contenido.offsetMax = Vector2.zero;

        VerticalLayoutGroup columna = contenido.gameObject.AddComponent<VerticalLayoutGroup>();
        columna.spacing = 6f;
        columna.padding = new RectOffset(0, 12, 4, 12);
        columna.childControlWidth = true;
        columna.childControlHeight = true;
        columna.childForceExpandWidth = true;
        columna.childForceExpandHeight = false;

        ContentSizeFitter ajuste = contenido.gameObject.AddComponent<ContentSizeFitter>();
        ajuste.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect desplazamiento = area.gameObject.AddComponent<ScrollRect>();
        desplazamiento.content = contenido;
        desplazamiento.viewport = vista;
        desplazamiento.horizontal = false;
        desplazamiento.movementType = ScrollRect.MovementType.Clamped;
        desplazamiento.scrollSensitivity = 40f;
        desplazamiento.verticalScrollbar = barra;
        desplazamiento.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
    }

    void CrearSeccion(RectTransform contenido, OpcionMenu opcion)
    {
        TextMeshProUGUI texto = ConstructorUI.CrearTexto("Seccion " + opcion.nombre, contenido, opcion.nombre.ToUpperInvariant(), 24f, ConstructorUI.ColorTitulo, TextAlignmentOptions.BottomLeft);
        texto.characterSpacing = 6f;
        texto.margin = new Vector4(8f, 0f, 0f, 4f);
        ConstructorUI.Medidas(texto, -1f, 48f);
    }

    void CrearFila(RectTransform contenido, OpcionMenu opcion)
    {
        Fila fila = new Fila { opcion = opcion };

        fila.fondo = ConstructorUI.CrearImagen("Fila " + opcion.nombre, contenido, ConstructorUI.ColorFila);
        ConstructorUI.Medidas(fila.fondo, -1f, AltoFila);

        HorizontalLayoutGroup linea = fila.fondo.gameObject.AddComponent<HorizontalLayoutGroup>();
        linea.padding = new RectOffset(20, 10, 6, 6);
        linea.spacing = 10f;
        linea.childAlignment = TextAnchor.MiddleLeft;
        linea.childControlWidth = true;
        linea.childControlHeight = true;
        linea.childForceExpandWidth = false;
        linea.childForceExpandHeight = false;

        fila.nombre = ConstructorUI.CrearTexto("Nombre", fila.fondo.transform, opcion.nombre, 32f, ConstructorUI.ColorTexto);
        ConstructorUI.Medidas(fila.nombre, -1f, AltoFila - 12f, 1f);

        fila.izquierda = ConstructorUI.CrearBoton("Anterior", fila.fondo.transform, "<", 34f, () => Cambiar(fila, -1));
        ConstructorUI.Medidas(fila.izquierda, 62f, 52f);
        // Las flechas no se navegan: con teclado o mando se elige la fila y se cambia con izquierda/derecha
        fila.izquierda.navigation = new Navigation { mode = Navigation.Mode.None };

        fila.valor = ConstructorUI.CrearTexto("Valor", fila.fondo.transform, string.Empty, 30f, ConstructorUI.ColorTitulo, TextAlignmentOptions.Center);
        ConstructorUI.Medidas(fila.valor, 330f, AltoFila - 12f);

        fila.derecha = ConstructorUI.CrearBoton("Siguiente", fila.fondo.transform, ">", 34f, () => Cambiar(fila, 1));
        ConstructorUI.Medidas(fila.derecha, 62f, 52f);
        fila.derecha.navigation = new Navigation { mode = Navigation.Mode.Vertical };

        Action<bool> senalar = porSeleccion =>
        {
            MostrarAyuda(opcion);

            // Con teclado o mando la lista sigue a la fila elegida; con el raton no salta
            if (porSeleccion)
                AsegurarVisible(fila.fondo.rectTransform);
        };
        fila.fondo.gameObject.AddComponent<AvisoFila>().alSenalar = senalar;
        fila.izquierda.gameObject.AddComponent<AvisoFila>().alSenalar = senalar;
        fila.derecha.gameObject.AddComponent<AvisoFila>().alSenalar = senalar;

        filas.Add(fila);
    }

    void CrearBotones((string texto, Action accion)[] botones)
    {
        RectTransform pie = ConstructorUI.CrearRect("Botones", panel);
        Franja(pie, 26f, 68f, false);

        HorizontalLayoutGroup linea = pie.gameObject.AddComponent<HorizontalLayoutGroup>();
        linea.spacing = 24f;
        linea.childAlignment = TextAnchor.MiddleCenter;
        linea.childControlWidth = true;
        linea.childControlHeight = true;
        linea.childForceExpandWidth = false;
        linea.childForceExpandHeight = false;

        for (int i = 0; i < botones.Length; i++)
        {
            Action accion = botones[i].accion;
            Button boton = ConstructorUI.CrearBoton(botones[i].texto, pie, botones[i].texto, 30f, () =>
            {
                accion?.Invoke();
                Refrescar();
            });
            ConstructorUI.Medidas(boton, 330f, 62f);
        }

        Button cerrar = ConstructorUI.CrearBoton("Cerrar", pie, "Cerrar", 30f, Cerrar);
        ConstructorUI.Medidas(cerrar, 260f, 62f);
    }

    /// <summary>Ancla un rect a todo el ancho del panel, pegado arriba o abajo.</summary>
    static void Franja(RectTransform rect, float distanciaBorde, float alto, bool arriba)
    {
        float y = arriba ? 1f : 0f;
        rect.anchorMin = new Vector2(0f, y);
        rect.anchorMax = new Vector2(1f, y);
        rect.pivot = new Vector2(0.5f, y);
        rect.sizeDelta = new Vector2(-72f, alto);
        rect.anchoredPosition = new Vector2(0f, arriba ? -distanciaBorde : distanciaBorde);
    }

    /// <summary>El panel ocupa lo que quepa de la pantalla, hasta su tamano maximo (moviles en vertical incluidos).</summary>
    void AjustarTamano()
    {
        pantalla = new Vector2Int(Screen.width, Screen.height);

        // Misma escala que aplica el CanvasScaler (1920x1080, mitad ancho y mitad alto)
        float escala = Mathf.Sqrt((Screen.width / 1920f) * (Screen.height / 1080f));
        Vector2 lienzo = new Vector2(Screen.width, Screen.height) / Mathf.Max(0.0001f, escala);

        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = Vector2.zero;
        panel.sizeDelta = new Vector2(Mathf.Min(AnchoMaximo, lienzo.x - 40f), Mathf.Min(AltoMaximo, lienzo.y - 30f));
    }

    #endregion

    #region Uso

    void Update()
    {
        Keyboard teclado = Keyboard.current;
        Gamepad mando = Gamepad.current;

        if ((teclado != null && teclado.escapeKey.wasPressedThisFrame) || (mando != null && mando.buttonEast.wasPressedThisFrame))
        {
            Cerrar();
            return;
        }

        int direccion = 0;
        if (teclado != null)
        {
            if (teclado.leftArrowKey.wasPressedThisFrame || teclado.aKey.wasPressedThisFrame)
                direccion = -1;
            else if (teclado.rightArrowKey.wasPressedThisFrame || teclado.dKey.wasPressedThisFrame)
                direccion = 1;
        }
        if (direccion == 0 && mando != null)
        {
            if (mando.dpad.left.wasPressedThisFrame)
                direccion = -1;
            else if (mando.dpad.right.wasPressedThisFrame)
                direccion = 1;
        }

        if (direccion != 0)
        {
            Fila seleccionada = FilaSeleccionada();
            if (seleccionada != null)
                Cambiar(seleccionada, direccion);
        }

        if (Time.unscaledTime >= siguienteEstado)
        {
            siguienteEstado = Time.unscaledTime + 0.5f;
            textoEstado.text = estado != null ? estado() : string.Empty;
        }

        if (Screen.width != pantalla.x || Screen.height != pantalla.y)
            AjustarTamano();
    }

    Fila FilaSeleccionada()
    {
        GameObject seleccionado = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (seleccionado == null)
            return null;

        for (int i = 0; i < filas.Count; i++)
        {
            if (filas[i].derecha.gameObject == seleccionado || filas[i].izquierda.gameObject == seleccionado)
                return filas[i];
        }

        return null;
    }

    void Cambiar(Fila fila, int direccion)
    {
        fila.opcion.Cambiar(direccion);

        // Un cambio puede mover otras filas (la calidad general las cambia todas)
        Refrescar();
        MostrarAyuda(fila.opcion);
    }

    /// <summary>Desplaza la lista lo justo para que la fila quede entera a la vista.</summary>
    void AsegurarVisible(RectTransform fila)
    {
        Canvas.ForceUpdateCanvases();

        // El VerticalLayoutGroup ancla las filas arriba con el pivote en su centro
        float arriba = -fila.anchoredPosition.y - fila.rect.height * (1f - fila.pivot.y);
        float abajo = arriba + fila.rect.height;
        float alto = vista.rect.height;
        float desplazamiento = contenido.anchoredPosition.y;

        if (arriba < desplazamiento)
            desplazamiento = arriba;
        else if (abajo > desplazamiento + alto)
            desplazamiento = abajo - alto;

        float maximo = Mathf.Max(0f, contenido.rect.height - alto);
        contenido.anchoredPosition = new Vector2(contenido.anchoredPosition.x, Mathf.Clamp(desplazamiento, 0f, maximo));
    }

    /// <summary>Vuelve a leer todos los valores. Llamalo si los ajustes cambian desde fuera del menu.</summary>
    public void Refrescar()
    {
        for (int i = 0; i < filas.Count; i++)
        {
            Fila fila = filas[i];
            bool disponible = fila.opcion.Disponible;

            fila.valor.text = fila.opcion.Texto;
            fila.valor.color = disponible ? ConstructorUI.ColorTitulo : ConstructorUI.ColorTenue;
            fila.nombre.color = disponible ? ConstructorUI.ColorTexto : ConstructorUI.ColorTenue;
            fila.izquierda.interactable = disponible;
            fila.derecha.interactable = disponible;
        }

        siguienteEstado = 0f;
    }

    void MostrarAyuda(OpcionMenu opcion)
    {
        textoAyuda.text = opcion.ayuda ?? string.Empty;
    }

    #endregion
}
