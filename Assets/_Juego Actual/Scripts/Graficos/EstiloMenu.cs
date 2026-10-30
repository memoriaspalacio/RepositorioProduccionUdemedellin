using TMPro;
using UnityEngine;

/// <summary>
/// Arte y colores de los menus hechos por codigo (el de graficos y cualquier otro MenuOpciones).
/// Vive en Resources/EstiloMenu para que el menu lo encuentre sin referencias en las escenas;
/// cambiar un sprite o un color aqui cambia todos los menus. Sin el asset los menus siguen
/// funcionando con un panel liso.
/// </summary>
[CreateAssetMenu(menuName = "Juego/Estilo de menu", fileName = "EstiloMenu")]
public class EstiloMenu : ScriptableObject
{
    const string Ruta = "EstiloMenu";

    [Header("Arte")]
    [Tooltip("Marco del panel (panel_ui). Se estira para que su parte visible ocupe el panel entero.")]
    public Sprite marco;

    [Tooltip("Parte visible del marco dentro de su imagen, en coordenadas 0-1 (x, y desde abajo a la izquierda).")]
    public Rect marcoVisible = new Rect(0.1315f, 0.2055f, 0.737f, 0.664f);

    [Tooltip("Margen interior del marco para que el contenido no pise los adornos, en fraccion del panel (izquierda, derecha, arriba, abajo).")]
    public Vector4 margenInterior = new Vector4(0.12f, 0.12f, 0.13f, 0.12f);

    [Tooltip("Pincelada de los botones y del resaltado de la fila elegida.")]
    public Sprite pincelada;

    [Tooltip("Adorno junto a los titulos de seccion.")]
    public Sprite adorno;

    [Header("Tipografia")]
    public TMP_FontAsset fuenteTexto;

    [Tooltip("Para el titulo. Si no tiene algun caracter del titulo (Kingstone no tiene tildes) se usa la de texto.")]
    public TMP_FontAsset fuenteTitulo;

    [Header("Colores")]
    public Color fondo = new Color(0.02f, 0.02f, 0.07f, 0.72f);
    public Color panelSinMarco = new Color(0.95f, 0.93f, 0.93f, 1f);
    public Color titulo = new Color(0.45f, 0.05f, 0.1f);
    public Color texto = new Color(0.18f, 0.07f, 0.09f);
    public Color tenue = new Color(0.47f, 0.39f, 0.41f);
    public Color valor = new Color(0.62f, 0.1f, 0.15f);
    public Color resaltado = new Color(0.66f, 0.13f, 0.18f, 0.22f);
    public Color textoBoton = Color.white;

    static EstiloMenu actual;

    /// <summary>El asset de Resources, o uno con los colores por defecto y sin arte si no existe.</summary>
    public static EstiloMenu Actual
    {
        get
        {
            if (actual == null)
            {
                actual = Resources.Load<EstiloMenu>(Ruta);
                if (actual == null)
                {
                    actual = CreateInstance<EstiloMenu>();
                    actual.hideFlags = HideFlags.DontSave;
                }
            }

            return actual;
        }
    }

    /// <summary>Relacion de aspecto de la parte visible del marco (1 si no hay marco).</summary>
    public float AspectoMarco
    {
        get
        {
            if (marco == null)
                return 1.45f;

            Rect imagen = marco.rect;
            return imagen.width * marcoVisible.width / Mathf.Max(1f, imagen.height * marcoVisible.height);
        }
    }

    public TMP_FontAsset FuenteTitulo(string titulo)
    {
        if (fuenteTitulo != null && fuenteTitulo.HasCharacters(titulo))
            return fuenteTitulo;

        return ConstructorUI.Fuente;
    }
}
