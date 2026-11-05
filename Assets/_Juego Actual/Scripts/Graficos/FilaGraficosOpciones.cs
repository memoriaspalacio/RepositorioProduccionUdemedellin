using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Agrega las filas "Graficos" y "Filtros" a los paneles de opciones del juego: los que tienen
/// el slider de ControlVolumen (el panel Opciones del menu principal y el prefab Opciones). Las
/// filas copian la etiqueta "Volumen" y el boton del propio panel, asi que se ven igual que lo
/// demas, y quedan debajo del volumen. Los botones muestran la calidad y el filtro en uso y
/// abren su menu.
///
/// Se hace al cargar la escena en vez de guardarla en ella para no tocar escenas que edita
/// todo el equipo. Si alguien pone a mano un boton con BotonAbrirGraficos en el panel, no se
/// agrega nada.
/// </summary>
public static class FilaGraficosOpciones
{
    /// <summary>Distancia entre filas (volumen, graficos, filtros), en unidades del panel. Con 50 las dos filas caben sobre el boton de volver del prefab Opciones.</summary>
    const float Separacion = 50f;

    /// <summary>Devuelve true si la escena tiene algun panel de opciones (con o sin fila nueva).</summary>
    public static bool Integrar(Scene escena)
    {
        bool hayPanel = false;
        foreach (GameObject raiz in escena.GetRootGameObjects())
        {
            foreach (ControlVolumen volumen in raiz.GetComponentsInChildren<ControlVolumen>(true))
                hayPanel |= AgregarFila(volumen.transform as RectTransform);
        }

        return hayPanel;
    }

    static bool AgregarFila(RectTransform slider)
    {
        if (slider == null || slider.parent == null)
            return false;

        Transform panel = slider.parent;
        bool tieneGraficos = panel.GetComponentInChildren<BotonAbrirGraficos>(true) != null;
        bool tieneFiltros = panel.GetComponentInChildren<BotonAbrirFiltros>(true) != null;
        if (tieneGraficos && tieneFiltros)
            return true;

        TMP_Text etiqueta = BuscarEtiqueta(panel, slider);
        Button modelo = BuscarBoton(panel, slider);
        if (etiqueta == null || modelo == null)
            return tieneGraficos;

        if (!tieneGraficos)
        {
            GameObject graficos = CrearFila(panel, etiqueta, modelo, slider, "Gráficos", "Graficos", 1);
            BotonAbrirGraficos abrir = graficos.AddComponent<BotonAbrirGraficos>();
            abrir.textoNivel = TextoBoton(graficos, OpcionesGraficas.NombreCalidad(GestorGraficos.Ajustes));
        }

        // Los filtros van debajo de graficos, antes del boton de volver
        if (!tieneFiltros)
        {
            GameObject filtros = CrearFila(panel, etiqueta, modelo, slider, "Filtros", "Filtros", 2);
            BotonAbrirFiltros abrir = filtros.AddComponent<BotonAbrirFiltros>();
            abrir.textoFiltro = TextoBoton(filtros, MenuFiltros.NombreActual);
        }

        return true;
    }

    /// <summary>
    /// Copia la etiqueta y el boton del panel en la fila numero 'puesto' bajo el volumen. El boton
    /// va donde el slider, centrado en la altura de su etiqueta. Devuelve el boton.
    /// </summary>
    static GameObject CrearFila(Transform panel, TMP_Text etiqueta, Button modelo, RectTransform slider,
        string texto, string nombre, int puesto)
    {
        float bajada = Separacion * puesto;

        TMP_Text nuevaEtiqueta = Object.Instantiate(etiqueta.gameObject, panel).GetComponent<TMP_Text>();
        nuevaEtiqueta.name = "Texto " + nombre.ToLowerInvariant();
        nuevaEtiqueta.text = texto;
        nuevaEtiqueta.rectTransform.anchoredPosition = etiqueta.rectTransform.anchoredPosition - new Vector2(0f, bajada);

        GameObject objetoBoton = Object.Instantiate(modelo.gameObject, panel);
        objetoBoton.name = nombre;
        Button boton = objetoBoton.GetComponent<Button>();
        boton.onClick = new Button.ButtonClickedEvent();

        RectTransform rectBoton = (RectTransform)objetoBoton.transform;
        Vector2 tamano = rectBoton.rect.size;
        rectBoton.anchorMin = slider.anchorMin;
        rectBoton.anchorMax = slider.anchorMax;
        rectBoton.pivot = new Vector2(0.5f, 0.5f);
        rectBoton.sizeDelta = new Vector2(Mathf.Max(tamano.x * 0.85f, slider.rect.width), tamano.y);
        rectBoton.anchoredPosition = new Vector2(slider.anchoredPosition.x, slider.anchoredPosition.y - bajada);

        // Con teclado o mando se llega del slider a los botones nuevos y de estos al de volver
        Navigation navegacion = boton.navigation;
        navegacion.mode = Navigation.Mode.Automatic;
        boton.navigation = navegacion;

        return objetoBoton;
    }

    static TMP_Text TextoBoton(GameObject boton, string inicial)
    {
        TMP_Text texto = boton.GetComponentInChildren<TMP_Text>(true);
        if (texto == null)
            return null;

        texto.enableAutoSizing = true;
        texto.fontSizeMax = texto.fontSize;
        texto.fontSizeMin = texto.fontSize * 0.6f;
        texto.text = inicial;
        return texto;
    }

    /// <summary>El texto "Volumen" del panel (o el primer texto suelto si lo renombraron).</summary>
    static TMP_Text BuscarEtiqueta(Transform panel, Transform slider)
    {
        TMP_Text primero = null;
        for (int i = 0; i < panel.childCount; i++)
        {
            Transform hijo = panel.GetChild(i);
            if (hijo == slider || !hijo.TryGetComponent(out TMP_Text texto))
                continue;

            if (texto.text.Trim().ToLowerInvariant().StartsWith("volumen"))
                return texto;

            if (primero == null)
                primero = texto;
        }

        return primero;
    }

    static Button BuscarBoton(Transform panel, Transform slider)
    {
        for (int i = 0; i < panel.childCount; i++)
        {
            Transform hijo = panel.GetChild(i);
            if (hijo != slider && hijo.TryGetComponent(out Button boton))
                return boton;
        }

        return null;
    }
}
