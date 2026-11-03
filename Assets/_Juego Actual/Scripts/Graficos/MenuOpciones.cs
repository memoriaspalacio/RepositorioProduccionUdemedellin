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
/// fila, izquierda y derecha cambian el valor y Escape (o B) lo cierra. El arte y los colores
/// salen de EstiloMenu.
/// </summary>
public class MenuOpciones : MonoBehaviour
{
    const float AltoMaximo = 1000f;
    const float AltoFila = 56f;
    const float AnchoFlecha = 52f;
    const float AnchoValor = 300f;

    class Fila
    {
        public OpcionMenu opcion;
        public Image fondo;
        public TextMeshProUGUI nombre;
        public TextMeshProUGUI valor;
        public Button izquierda;
        public Button derecha;
        public Image[] pasos;
        // Lo que eligen el teclado y el mando: la fila entera, no una flecha, porque una flecha
        // apagada en un extremo no se puede seleccionar
        public Selectable selector;
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
    EstiloMenu estilo;
    Fila senalada;
    RectTransform panel;
    RectTransform interior;
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
        estilo = EstiloMenu.Actual;

        // Fondo translucido: deja ver el juego (y como cambia) pero bloquea los clics detras
        Image fondo = ConstructorUI.CrearImagen("Fondo", transform, estilo.fondo);
        ConstructorUI.Llenar(fondo.rectTransform);

        panel = ConstructorUI.CrearRect("Panel", transform);
        AjustarTamano();
        CrearMarco();

        TextMeshProUGUI textoTitulo = ConstructorUI.CrearTexto("Titulo", interior, titulo, 60f, estilo.titulo, TextAlignmentOptions.Center);
        textoTitulo.font = estilo.FuenteTitulo(titulo);
        // A este tamano la linea es mas alta que la franja; con Ellipsis TextMeshPro la ocultaba entera
        textoTitulo.overflowMode = TextOverflowModes.Overflow;
        Franja(textoTitulo.rectTransform, 0f, 68f, true);

        textoEstado = ConstructorUI.CrearTexto("Estado", interior, string.Empty, 22f, estilo.tenue, TextAlignmentOptions.Center);
        Franja(textoEstado.rectTransform, 68f, 30f, true);

        CrearLista(108f, 132f);
        for (int i = 0; i < opciones.Count; i++)
        {
            if (opciones[i].EsLeyenda)
                CrearLeyenda(contenido, opciones[i]);
            else if (opciones[i].EsSeccion)
                CrearSeccion(contenido, opciones[i]);
            else
                CrearFila(contenido, opciones[i]);
        }

        textoAyuda = ConstructorUI.CrearTexto("Ayuda", interior, string.Empty, 22f, estilo.tenue, TextAlignmentOptions.Top);
        textoAyuda.textWrappingMode = TextWrappingModes.Normal;
        textoAyuda.overflowMode = TextOverflowModes.Truncate;
        textoAyuda.fontStyle = FontStyles.Italic;
        Franja(textoAyuda.rectTransform, 66f, 60f, false);

        CrearBotones(botones);
        Refrescar();

        if (filas.Count > 0)
        {
            Senalar(filas[0]);
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(filas[0].selector.gameObject);
        }
    }

    /// <summary>
    /// El marco ornamentado (o un panel liso si el estilo no tiene). El panel mide lo que la
    /// parte visible del marco, asi que la imagen se sale por los lados lo que ocupan sus margenes
    /// transparentes. El contenido va en el interior, apartado de los adornos.
    /// </summary>
    void CrearMarco()
    {
        Vector4 margen;
        if (estilo.marco != null)
        {
            Image marco = ConstructorUI.CrearImagen("Marco", panel, Color.white);
            marco.sprite = estilo.marco;

            Rect visible = estilo.marcoVisible;
            RectTransform rect = marco.rectTransform;
            rect.anchorMin = new Vector2(-visible.x / visible.width, -visible.y / visible.height);
            rect.anchorMax = new Vector2((1f - visible.x) / visible.width, (1f - visible.y) / visible.height);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            margen = estilo.margenInterior;
        }
        else
        {
            Image liso = ConstructorUI.CrearImagen("Panel liso", panel, estilo.panelSinMarco);
            ConstructorUI.Llenar(liso.rectTransform);
            Outline borde = liso.gameObject.AddComponent<Outline>();
            borde.effectColor = estilo.titulo;
            borde.effectDistance = new Vector2(3f, -3f);
            margen = new Vector4(0.04f, 0.04f, 0.04f, 0.04f);
        }

        interior = ConstructorUI.CrearRect("Interior", panel);
        interior.anchorMin = new Vector2(margen.x, margen.w);
        interior.anchorMax = new Vector2(1f - margen.y, 1f - margen.z);
        interior.offsetMin = Vector2.zero;
        interior.offsetMax = Vector2.zero;
    }

    /// <summary>Lista con desplazamiento (y su barra) entre el encabezado y el pie del panel.</summary>
    void CrearLista(float arriba, float abajo)
    {
        RectTransform area = ConstructorUI.CrearRect("Lista", interior);
        area.anchorMin = Vector2.zero;
        area.anchorMax = Vector2.one;
        area.offsetMin = new Vector2(0f, abajo);
        area.offsetMax = new Vector2(0f, -arriba);

        // La vista recorta las filas; la barra queda fuera de ella, a la derecha
        Image fondoVista = ConstructorUI.CrearImagen("Vista", area, new Color(0f, 0f, 0f, 0.001f));
        vista = fondoVista.rectTransform;
        ConstructorUI.Llenar(vista);
        vista.offsetMax = new Vector2(-22f, 0f);
        vista.gameObject.AddComponent<RectMask2D>();

        Image fondoBarra = ConstructorUI.CrearImagen("Barra", area, new Color(estilo.titulo.r, estilo.titulo.g, estilo.titulo.b, 0.12f));
        fondoBarra.sprite = SpritesProcedurales.Capsula;
        fondoBarra.type = Image.Type.Sliced;
        RectTransform rectBarra = fondoBarra.rectTransform;
        rectBarra.anchorMin = new Vector2(1f, 0f);
        rectBarra.anchorMax = new Vector2(1f, 1f);
        rectBarra.pivot = new Vector2(1f, 0.5f);
        rectBarra.sizeDelta = new Vector2(8f, 0f);
        rectBarra.anchoredPosition = Vector2.zero;

        RectTransform zonaManija = ConstructorUI.CrearRect("Zona", rectBarra);
        ConstructorUI.Llenar(zonaManija);
        Image manija = ConstructorUI.CrearImagen("Manija", zonaManija, new Color(estilo.titulo.r, estilo.titulo.g, estilo.titulo.b, 0.75f));
        manija.sprite = SpritesProcedurales.Capsula;
        manija.type = Image.Type.Sliced;
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
        RectTransform seccion = ConstructorUI.CrearRect("Seccion " + opcion.nombre, contenido);
        ConstructorUI.Medidas(seccion, -1f, 46f);

        TextMeshProUGUI texto = ConstructorUI.CrearTexto("Texto", seccion, opcion.nombre.ToUpperInvariant(), 22f, estilo.titulo, TextAlignmentOptions.BottomLeft);
        texto.characterSpacing = 6f;
        texto.margin = new Vector4(8f, 0f, 0f, 6f);
        ConstructorUI.Llenar(texto.rectTransform);

        // Filete bajo el titulo, que se desvanece hacia la derecha
        Image filete = ConstructorUI.CrearImagen("Filete", seccion, new Color(estilo.titulo.r, estilo.titulo.g, estilo.titulo.b, 0.45f));
        filete.sprite = SpritesProcedurales.DegradadoSalida;
        RectTransform rectFilete = filete.rectTransform;
        rectFilete.anchorMin = Vector2.zero;
        rectFilete.anchorMax = new Vector2(1f, 0f);
        rectFilete.pivot = new Vector2(0.5f, 0f);
        rectFilete.sizeDelta = new Vector2(0f, 2f);
        rectFilete.anchoredPosition = Vector2.zero;
    }

    /// <summary>
    /// Linea encima de las flechas que dice a donde lleva cada una, alineada con ellas: el texto de
    /// la izquierda arranca sobre la flecha izquierda y el de la derecha acaba sobre la derecha.
    /// </summary>
    void CrearLeyenda(RectTransform contenido, OpcionMenu opcion)
    {
        RectTransform fila = ConstructorUI.CrearRect("Leyenda", contenido);
        ConstructorUI.Medidas(fila, -1f, 30f);

        HorizontalLayoutGroup linea = fila.gameObject.AddComponent<HorizontalLayoutGroup>();
        linea.padding = new RectOffset(24, 14, 0, 0);
        linea.spacing = 6f;
        linea.childAlignment = TextAnchor.MiddleLeft;
        linea.childControlWidth = true;
        linea.childControlHeight = true;
        linea.childForceExpandWidth = false;
        linea.childForceExpandHeight = false;

        RectTransform hueco = ConstructorUI.CrearRect("Hueco", fila);
        ConstructorUI.Medidas(hueco, -1f, 26f, 1f);

        RectTransform zona = ConstructorUI.CrearRect("Flechas", fila);
        ConstructorUI.Medidas(zona, AnchoFlecha * 2f + AnchoValor + 12f, 26f);

        TextMeshProUGUI izquierda = ConstructorUI.CrearTexto("Izquierda", zona, opcion.izquierda, 20f, estilo.tenue, TextAlignmentOptions.MidlineLeft);
        ConstructorUI.Llenar(izquierda.rectTransform);
        izquierda.fontStyle = FontStyles.Italic;

        TextMeshProUGUI derecha = ConstructorUI.CrearTexto("Derecha", zona, opcion.derecha, 20f, estilo.tenue, TextAlignmentOptions.MidlineRight);
        ConstructorUI.Llenar(derecha.rectTransform);
        derecha.fontStyle = FontStyles.Italic;
    }

    void CrearFila(RectTransform contenido, OpcionMenu opcion)
    {
        Fila fila = new Fila { opcion = opcion };

        // La pincelada solo se ve en la fila senalada
        fila.fondo = ConstructorUI.CrearImagen("Fila " + opcion.nombre, contenido, Color.clear);
        fila.fondo.sprite = estilo.pincelada;
        ConstructorUI.Medidas(fila.fondo, -1f, AltoFila);

        HorizontalLayoutGroup linea = fila.fondo.gameObject.AddComponent<HorizontalLayoutGroup>();
        linea.padding = new RectOffset(24, 14, 2, 2);
        linea.spacing = 6f;
        linea.childAlignment = TextAnchor.MiddleLeft;
        linea.childControlWidth = true;
        linea.childControlHeight = true;
        linea.childForceExpandWidth = false;
        linea.childForceExpandHeight = false;

        fila.nombre = ConstructorUI.CrearTexto("Nombre", fila.fondo.transform, opcion.nombre, 30f, estilo.texto);
        ConstructorUI.Medidas(fila.nombre, -1f, AltoFila - 4f, 1f);

        fila.izquierda = ConstructorUI.CrearBotonTexto("Anterior", fila.fondo.transform, "<", 38f, estilo.valor, () => Cambiar(fila, -1));
        ConstructorUI.Medidas(fila.izquierda, AnchoFlecha, AltoFila - 6f);
        // Las flechas no se navegan: con teclado o mando se elige la fila y se cambia con izquierda/derecha
        fila.izquierda.navigation = new Navigation { mode = Navigation.Mode.None };

        fila.valor = ConstructorUI.CrearTexto("Valor", fila.fondo.transform, string.Empty, 28f, estilo.valor, TextAlignmentOptions.Center);
        ConstructorUI.Medidas(fila.valor, AnchoValor, AltoFila - 4f);
        fila.valor.margin = new Vector4(0f, 0f, 0f, 10f);
        fila.pasos = CrearPasos(fila.valor.rectTransform, opcion.valores.Length);

        fila.derecha = ConstructorUI.CrearBotonTexto("Siguiente", fila.fondo.transform, ">", 38f, estilo.valor, () => Cambiar(fila, 1));
        ConstructorUI.Medidas(fila.derecha, AnchoFlecha, AltoFila - 6f);
        fila.derecha.navigation = new Navigation { mode = Navigation.Mode.None };

        fila.selector = fila.fondo.gameObject.AddComponent<Selectable>();
        fila.selector.transition = Selectable.Transition.None;
        fila.selector.navigation = new Navigation { mode = Navigation.Mode.Vertical };

        Action<bool> senalar = porSeleccion =>
        {
            Senalar(fila);

            // Con teclado o mando la lista sigue a la fila elegida; con el raton no salta
            if (porSeleccion)
                AsegurarVisible(fila.fondo.rectTransform);
        };
        fila.fondo.gameObject.AddComponent<AvisoFila>().alSenalar = senalar;
        fila.izquierda.gameObject.AddComponent<AvisoFila>().alSenalar = senalar;
        fila.derecha.gameObject.AddComponent<AvisoFila>().alSenalar = senalar;

        filas.Add(fila);
    }

    /// <summary>
    /// Marcas bajo el valor, una por opcion: las encendidas hasta la actual dicen en que punto de
    /// la escala estas y que la derecha es "mas".
    /// </summary>
    Image[] CrearPasos(RectTransform valor, int cantidad)
    {
        RectTransform zona = ConstructorUI.CrearRect("Pasos", valor);
        zona.anchorMin = new Vector2(0.5f, 0f);
        zona.anchorMax = new Vector2(0.5f, 0f);
        zona.pivot = new Vector2(0.5f, 0f);
        float ancho = Mathf.Min(AnchoValor - 60f, cantidad * 34f);
        zona.sizeDelta = new Vector2(ancho, 6f);
        zona.anchoredPosition = new Vector2(0f, 4f);

        HorizontalLayoutGroup linea = zona.gameObject.AddComponent<HorizontalLayoutGroup>();
        linea.spacing = 5f;
        linea.childControlWidth = true;
        linea.childControlHeight = true;
        linea.childForceExpandWidth = true;
        linea.childForceExpandHeight = true;

        Image[] pasos = new Image[cantidad];
        for (int i = 0; i < cantidad; i++)
        {
            pasos[i] = ConstructorUI.CrearImagen("Paso " + i, zona, estilo.valor);
            pasos[i].sprite = SpritesProcedurales.Capsula;
            pasos[i].type = Image.Type.Sliced;
            pasos[i].raycastTarget = false;
        }

        return pasos;
    }

    void CrearBotones((string texto, Action accion)[] botones)
    {
        RectTransform pie = ConstructorUI.CrearRect("Botones", interior);
        Franja(pie, 0f, 60f, false);

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
            Button boton = ConstructorUI.CrearBotonPincelada(botones[i].texto, pie, botones[i].texto, 28f, () =>
            {
                accion?.Invoke();
                Refrescar();
            });
            ConstructorUI.Medidas(boton, 330f, 58f);
        }

        Button cerrar = ConstructorUI.CrearBotonPincelada("Cerrar", pie, "Cerrar", 28f, Cerrar);
        ConstructorUI.Medidas(cerrar, 240f, 58f);
    }

    /// <summary>Ancla un rect a todo el ancho del interior, pegado arriba o abajo.</summary>
    static void Franja(RectTransform rect, float distanciaBorde, float alto, bool arriba)
    {
        float y = arriba ? 1f : 0f;
        rect.anchorMin = new Vector2(0f, y);
        rect.anchorMax = new Vector2(1f, y);
        rect.pivot = new Vector2(0.5f, y);
        rect.sizeDelta = new Vector2(0f, alto);
        rect.anchoredPosition = new Vector2(0f, arriba ? -distanciaBorde : distanciaBorde);
    }

    /// <summary>
    /// El panel ocupa lo que quepa de la pantalla, hasta su tamano maximo, con la proporcion
    /// del marco para que no se deforme (moviles en vertical incluidos).
    /// </summary>
    void AjustarTamano()
    {
        pantalla = new Vector2Int(Screen.width, Screen.height);

        // Misma escala que aplica el CanvasScaler (1920x1080, mitad ancho y mitad alto)
        float escala = Mathf.Sqrt((Screen.width / 1920f) * (Screen.height / 1080f));
        Vector2 lienzo = new Vector2(Screen.width, Screen.height) / Mathf.Max(0.0001f, escala);

        float aspecto = EstiloMenu.Actual.AspectoMarco;
        float alto = Mathf.Min(AltoMaximo, lienzo.y - 20f);
        float ancho = alto * aspecto;
        if (ancho > lienzo.x - 20f)
        {
            ancho = lienzo.x - 20f;
            alto = ancho / aspecto;
        }

        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = Vector2.zero;
        panel.sizeDelta = new Vector2(ancho, alto);
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
            if (filas[i].selector.gameObject == seleccionado ||
                filas[i].derecha.gameObject == seleccionado || filas[i].izquierda.gameObject == seleccionado)
                return filas[i];
        }

        return null;
    }

    void Cambiar(Fila fila, int direccion)
    {
        fila.opcion.Cambiar(direccion);

        // Un cambio puede mover otras filas (la calidad general las cambia todas)
        Refrescar();
        Senalar(fila);
    }

    /// <summary>Resalta la fila con la pincelada y muestra su ayuda.</summary>
    void Senalar(Fila fila)
    {
        if (senalada != null && senalada != fila)
            senalada.fondo.color = Color.clear;

        senalada = fila;
        fila.fondo.color = estilo.resaltado;
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
            fila.valor.color = disponible ? estilo.valor : estilo.tenue;
            fila.nombre.color = disponible ? estilo.texto : estilo.tenue;
            // En un extremo la flecha de ese lado se apaga: no da la vuelta
            fila.izquierda.interactable = fila.opcion.PuedeIr(-1);
            fila.derecha.interactable = fila.opcion.PuedeIr(1);

            int indice = fila.opcion.Indice;
            Color encendido = disponible ? estilo.valor : estilo.tenue;
            Color apagado = new Color(estilo.tenue.r, estilo.tenue.g, estilo.tenue.b, 0.25f);
            for (int p = 0; p < fila.pasos.Length; p++)
                fila.pasos[p].color = p <= indice ? encendido : apagado;
        }

        siguienteEstado = 0f;
    }

    void MostrarAyuda(OpcionMenu opcion)
    {
        textoAyuda.text = opcion.ayuda ?? string.Empty;
    }

    #endregion
}
