using UnityEngine;

/// <summary>
/// Sprites sencillos generados por código para efectos de UI que no necesitan un asset
/// de arte propio: un halo suave para luces, una cápsula de extremos redondeados para
/// barras y dos degradados horizontales. Se crean la primera vez que alguien los pide y
/// se reutilizan desde entonces.
/// </summary>
public static class SpritesProcedurales
{
    private static Sprite brilloRadial;
    private static Sprite capsula;
    private static Sprite degradadoEntrada;
    private static Sprite degradadoSalida;

    /// <summary>Círculo blanco que se desvanece hacia el borde. Para halos y luces.</summary>
    public static Sprite BrilloRadial
    {
        get
        {
            if (brilloRadial == null)
                brilloRadial = CrearBrilloRadial(128);

            return brilloRadial;
        }
    }

    /// <summary>
    /// Cápsula blanca (extremos en semicírculo) con bordes de 9-slice, para Image.Type.Sliced.
    /// Con pixelsPerUnitMultiplier = 64 / alto los extremos quedan perfectamente redondos.
    /// </summary>
    public static Sprite Capsula
    {
        get
        {
            if (capsula == null)
                capsula = CrearCapsula();

            return capsula;
        }
    }

    /// <summary>Blanco de transparente (izquierda) a opaco (derecha).</summary>
    public static Sprite DegradadoEntrada
    {
        get
        {
            if (degradadoEntrada == null)
                degradadoEntrada = CrearDegradado(true);

            return degradadoEntrada;
        }
    }

    /// <summary>Blanco de opaco (izquierda) a transparente (derecha).</summary>
    public static Sprite DegradadoSalida
    {
        get
        {
            if (degradadoSalida == null)
                degradadoSalida = CrearDegradado(false);

            return degradadoSalida;
        }
    }

    private static Texture2D NuevaTextura(int ancho, int alto, string nombre)
    {
        return new Texture2D(ancho, alto, TextureFormat.RGBA32, false)
        {
            name = nombre,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };
    }

    private static Sprite CrearBrilloRadial(int tamano)
    {
        Texture2D textura = NuevaTextura(tamano, tamano, "BrilloRadial");
        Color[] pixeles = new Color[tamano * tamano];
        float radio = tamano * 0.5f;

        for (int y = 0; y < tamano; y++)
        {
            for (int x = 0; x < tamano; x++)
            {
                float dx = (x + 0.5f - radio) / radio;
                float dy = (y + 0.5f - radio) / radio;
                float distancia = Mathf.Sqrt(dx * dx + dy * dy);

                // Núcleo brillante que cae suave hasta cero justo en el borde
                float alfa = Mathf.SmoothStep(0f, 1f, 1f - distancia);
                pixeles[y * tamano + x] = new Color(1f, 1f, 1f, alfa * alfa);
            }
        }

        textura.SetPixels(pixeles);
        textura.Apply();

        Sprite sprite = Sprite.Create(textura, new Rect(0, 0, tamano, tamano), new Vector2(0.5f, 0.5f), 100f);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    private static Sprite CrearCapsula()
    {
        // 66 x 64: dos semicírculos de radio 32 y una franja recta de 2 px en el medio,
        // que es la que estira el 9-slice.
        const int ancho = 66;
        const int alto = 64;
        const float radio = 32f;

        Texture2D textura = NuevaTextura(ancho, alto, "Capsula");
        Color[] pixeles = new Color[ancho * alto];

        for (int y = 0; y < alto; y++)
        {
            for (int x = 0; x < ancho; x++)
            {
                float px = x + 0.5f;
                float py = y + 0.5f;

                // Distancia al segmento central de la cápsula
                float cx = Mathf.Clamp(px, radio, ancho - radio);
                float dx = px - cx;
                float dy = py - radio;
                float distancia = Mathf.Sqrt(dx * dx + dy * dy);

                // Borde con un píxel de suavizado para que no se vea dentado
                float alfa = Mathf.Clamp01(radio - distancia + 0.5f);
                pixeles[y * ancho + x] = new Color(1f, 1f, 1f, alfa);
            }
        }

        textura.SetPixels(pixeles);
        textura.Apply();

        Sprite sprite = Sprite.Create(
            textura,
            new Rect(0, 0, ancho, alto),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(radio, radio, radio, radio)
        );

        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    private static Sprite CrearDegradado(bool entrada)
    {
        const int ancho = 64;
        const int alto = 2;

        Texture2D textura = NuevaTextura(ancho, alto, entrada ? "DegradadoEntrada" : "DegradadoSalida");
        Color[] pixeles = new Color[ancho * alto];

        for (int x = 0; x < ancho; x++)
        {
            float t = x / (float)(ancho - 1);
            float alfa = entrada ? t : 1f - t;

            for (int y = 0; y < alto; y++)
                pixeles[y * ancho + x] = new Color(1f, 1f, 1f, alfa);
        }

        textura.SetPixels(pixeles);
        textura.Apply();

        Sprite sprite = Sprite.Create(textura, new Rect(0, 0, ancho, alto), new Vector2(0.5f, 0.5f), 100f);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }
}
