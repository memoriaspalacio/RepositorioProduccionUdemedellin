using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// FPS, milisegundos por fotograma y el peor fotograma de cada medio segundo, en la esquina
/// superior izquierda. Sirve para medir de verdad lo que cuesta cada ajuste en el equipo
/// donde se prueba, en vez de discutirlo. Se enciende desde el menu de graficos.
/// </summary>
public class ContadorFps : MonoBehaviour
{
    const float Intervalo = 0.5f;

    static ContadorFps instancia;

    TextMeshProUGUI texto;
    float tiempo;
    int cuadros;
    float peor;

    public static void Mostrar(bool mostrar, Transform padre)
    {
        if (!mostrar)
        {
            if (instancia != null)
                instancia.gameObject.SetActive(false);
            return;
        }

        if (instancia == null)
            instancia = Crear(padre);

        instancia.gameObject.SetActive(true);
    }

    static ContadorFps Crear(Transform padre)
    {
        Canvas canvas = ConstructorUI.CrearCanvas("Contador FPS", 5100, padre);
        canvas.GetComponent<GraphicRaycaster>().enabled = false;

        RectTransform zona = ConstructorUI.CrearRect("Zona segura", canvas.transform);
        ConstructorUI.AjustarAZonaSegura(zona);

        Image fondo = ConstructorUI.CrearImagen("Fondo", zona, new Color(0f, 0f, 0f, 0.6f));
        fondo.raycastTarget = false;
        RectTransform rect = fondo.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(400f, 86f);
        rect.anchoredPosition = new Vector2(16f, -16f);

        ContadorFps contador = canvas.gameObject.AddComponent<ContadorFps>();
        contador.texto = ConstructorUI.CrearTexto("Texto", rect, "...", 28f, ConstructorUI.ColorTexto, TextAlignmentOptions.TopLeft);
        ConstructorUI.Llenar(contador.texto.rectTransform, 10f);
        return contador;
    }

    void OnDestroy()
    {
        if (instancia == this)
            instancia = null;
    }

    void Update()
    {
        float delta = Time.unscaledDeltaTime;
        tiempo += delta;
        cuadros++;
        peor = Mathf.Max(peor, delta);

        if (tiempo < Intervalo)
            return;

        float fps = cuadros / tiempo;
        float milisegundos = 1000f * tiempo / cuadros;
        string color = fps >= 55f ? "#8CE07A" : fps >= 28f ? "#F2C14E" : "#F06A5A";

        AjustesGraficos ajustes = GestorGraficos.Ajustes;
        int ancho = Mathf.RoundToInt(Screen.width * ajustes.FactorResolucion);
        int alto = Mathf.RoundToInt(Screen.height * ajustes.FactorResolucion);

        texto.text = $"<color={color}>{fps:0} FPS</color>  {milisegundos:0.0} ms  <size=75%>peor {peor * 1000f:0} ms</size>\n" +
                     $"<size=75%>{GestorGraficos.NombreNivelActivo} · 3D {ancho}×{alto}</size>";

        tiempo = 0f;
        cuadros = 0;
        peor = 0f;
    }
}
