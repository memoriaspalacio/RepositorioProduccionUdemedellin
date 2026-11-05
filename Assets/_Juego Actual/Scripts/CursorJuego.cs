using UnityEngine;

/// <summary>
/// Cursor del juego: el corazon alado boca abajo, como una punta de lanza inclinada, con el punto
/// de clic justo en la punta (Resources/CursorPuntero). Se pone solo al arrancar y se reescala con
/// la pantalla para que mida lo mismo en cualquier resolucion (unos 54 px a 1080p): la imagen
/// original era un dibujo pequeno en medio de un lienzo enorme y con el punto de clic en una
/// esquina vacia, asi que se veia diminuto y el clic caia lejos de donde parecia.
/// </summary>
public static class CursorJuego
{
    const string Recurso = "CursorPuntero";

    /// <summary>Punta de la lanza dentro de la imagen, en fraccion de su lado (desde arriba a la izquierda).</summary>
    static readonly Vector2 Punta = new Vector2(106.4f / 256f, 18.7f / 256f);

    /// <summary>Lado del cursor en fraccion del alto de la pantalla.</summary>
    const float FraccionPantalla = 0.05f;
    const int LadoMinimo = 32;
    const int LadoMaximo = 128;

    static Texture2D original;
    static Texture2D escalado;
    static int altoAplicado;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Iniciar()
    {
        if (Application.isMobilePlatform)
            return;

        original = Resources.Load<Texture2D>(Recurso);
        if (original == null)
            return;

        Aplicar();

        // Al cambiar de resolucion (ventana, pantalla completa) se vuelve a escalar
        GameObject vigia = new GameObject("Cursor Juego") { hideFlags = HideFlags.HideInHierarchy };
        Object.DontDestroyOnLoad(vigia);
        vigia.AddComponent<Vigia>();
    }

    static void Aplicar()
    {
        altoAplicado = Screen.height;
        int lado = Mathf.Clamp(Mathf.RoundToInt(Screen.height * FraccionPantalla), LadoMinimo, LadoMaximo);

        if (escalado != null)
            Object.Destroy(escalado);
        escalado = Escalar(original, lado);

        Vector2 punto = new Vector2(Punta.x * lado, Punta.y * lado);
        Cursor.SetCursor(escalado, punto, CursorMode.Auto);
    }

    /// <summary>Copia reducida con 4x4 muestras por pixel, para que no se vea dentada.</summary>
    static Texture2D Escalar(Texture2D fuente, int lado)
    {
        Texture2D destino = new Texture2D(lado, lado, TextureFormat.RGBA32, false)
        {
            name = "CursorPuntero " + lado,
            wrapMode = TextureWrapMode.Clamp
        };

        const int Muestras = 4;
        Color[] pixeles = new Color[lado * lado];
        for (int y = 0; y < lado; y++)
        {
            for (int x = 0; x < lado; x++)
            {
                Color suma = Color.clear;
                for (int sy = 0; sy < Muestras; sy++)
                {
                    for (int sx = 0; sx < Muestras; sx++)
                    {
                        float u = (x + (sx + 0.5f) / Muestras) / lado;
                        float v = (y + (sy + 0.5f) / Muestras) / lado;
                        Color c = fuente.GetPixelBilinear(u, v);
                        // Promedio premultiplicado: los bordes no se oscurecen con el negro transparente
                        suma += new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a);
                    }
                }

                suma /= Muestras * Muestras;
                pixeles[y * lado + x] = suma.a > 0.001f
                    ? new Color(suma.r / suma.a, suma.g / suma.a, suma.b / suma.a, suma.a)
                    : Color.clear;
            }
        }

        destino.SetPixels(pixeles);
        destino.Apply(false, false);
        return destino;
    }

    class Vigia : MonoBehaviour
    {
        void Update()
        {
            if (Screen.height != altoAplicado)
                Aplicar();
        }
    }
}
