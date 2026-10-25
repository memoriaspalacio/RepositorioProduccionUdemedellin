using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// El menu de graficos: un MenuOpciones con las filas de OpcionesGraficas. Se abre con F1
/// en cualquier escena, con el boton "Graficos" de las escenas de GestorGraficos.EscenasConBoton
/// o desde cualquier boton que tenga un BotonAbrirGraficos.
/// </summary>
public static class MenuGraficos
{
    static MenuOpciones abierto;

    public static bool EstaAbierto => abierto != null;

    public static void Abrir()
    {
        if (abierto != null)
            return;

        abierto = MenuOpciones.Crear("Gráficos", OpcionesGraficas.Crear(), OpcionesGraficas.Estado,
            ("Detectar automático", () => GestorGraficos.UsarNivel(GestorGraficos.DetectarNivel())));

        // Si algo cambia los ajustes con el menu abierto, las filas lo reflejan
        GestorGraficos.AlCambiar += Refrescar;
        abierto.AlCerrar += () =>
        {
            GestorGraficos.AlCambiar -= Refrescar;
            abierto = null;
        };
    }

    static void Refrescar()
    {
        if (abierto != null)
            abierto.Refrescar();
    }

    public static void Cerrar()
    {
        if (abierto != null)
            abierto.Cerrar();
    }

    public static void Alternar()
    {
        if (EstaAbierto)
            Cerrar();
        else
            Abrir();
    }

    /// <summary>Boton pequeno en la esquina superior derecha; vive y muere con su escena.</summary>
    public static void CrearBotonFlotante(Scene escena)
    {
        Canvas canvas = ConstructorUI.CrearCanvas("Boton Graficos", 4000);
        SceneManager.MoveGameObjectToScene(canvas.gameObject, escena);
        ConstructorUI.AsegurarEventSystem(canvas.transform);

        RectTransform zona = ConstructorUI.CrearRect("Zona segura", canvas.transform);
        ConstructorUI.AjustarAZonaSegura(zona);

        Button boton = ConstructorUI.CrearBoton("Graficos", zona, "Gráficos", 30f, Abrir);
        RectTransform rect = (RectTransform)boton.transform;
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.sizeDelta = new Vector2(230f, 64f);
        rect.anchoredPosition = new Vector2(-28f, -28f);
    }
}
