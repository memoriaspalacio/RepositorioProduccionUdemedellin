using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Agrega la fila "Graficos" a los paneles de opciones del juego: los que tienen el slider de
/// ControlVolumen (el panel Opciones del menu principal y el prefab Opciones). La fila copia la
/// etiqueta "Volumen" y el boton del propio panel, asi que se ve igual que lo demas, y queda
/// justo debajo del volumen. El boton muestra la calidad en uso y abre el menu de graficos.
///
/// Se hace al cargar la escena en vez de guardarla en ella para no tocar escenas que edita
/// todo el equipo. Si alguien pone a mano un boton con BotonAbrirGraficos en el panel, no se
/// agrega nada.
/// </summary>
public static class FilaGraficosOpciones
{
    /// <summary>Distancia entre la fila del volumen y la de graficos, en unidades del panel.</summary>
    const float Separacion = 58f;

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
        if (panel.GetComponentInChildren<BotonAbrirGraficos>(true) != null)
            return true;

        TMP_Text etiqueta = BuscarEtiqueta(panel, slider);
        Button modelo = BuscarBoton(panel, slider);
        if (etiqueta == null || modelo == null)
            return false;

        RectTransform rectEtiqueta = etiqueta.rectTransform;
        TMP_Text nuevaEtiqueta = Object.Instantiate(etiqueta.gameObject, panel).GetComponent<TMP_Text>();
        nuevaEtiqueta.name = "Texto graficos";
        nuevaEtiqueta.text = "Gráficos";
        nuevaEtiqueta.rectTransform.anchoredPosition = rectEtiqueta.anchoredPosition - new Vector2(0f, Separacion);

        // El boton va donde el slider, centrado en la altura de la etiqueta nueva
        GameObject objetoBoton = Object.Instantiate(modelo.gameObject, panel);
        objetoBoton.name = "Graficos";
        Button boton = objetoBoton.GetComponent<Button>();
        boton.onClick = new Button.ButtonClickedEvent();

        RectTransform rectBoton = (RectTransform)objetoBoton.transform;
        Vector2 tamano = rectBoton.rect.size;
        rectBoton.anchorMin = slider.anchorMin;
        rectBoton.anchorMax = slider.anchorMax;
        rectBoton.pivot = new Vector2(0.5f, 0.5f);
        rectBoton.sizeDelta = new Vector2(Mathf.Max(tamano.x * 0.85f, slider.rect.width), tamano.y);
        rectBoton.anchoredPosition = new Vector2(slider.anchoredPosition.x, slider.anchoredPosition.y - Separacion);

        BotonAbrirGraficos abrir = objetoBoton.AddComponent<BotonAbrirGraficos>();
        abrir.textoNivel = objetoBoton.GetComponentInChildren<TMP_Text>(true);
        if (abrir.textoNivel != null)
        {
            abrir.textoNivel.enableAutoSizing = true;
            abrir.textoNivel.fontSizeMax = abrir.textoNivel.fontSize;
            abrir.textoNivel.fontSizeMin = abrir.textoNivel.fontSize * 0.6f;
            abrir.textoNivel.text = OpcionesGraficas.NombreCalidad(GestorGraficos.Ajustes);
        }

        // Con teclado o mando se llega del slider al boton nuevo y de este al de volver
        Navigation navegacion = boton.navigation;
        navegacion.mode = Navigation.Mode.Automatic;
        boton.navigation = navegacion;

        return true;
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
