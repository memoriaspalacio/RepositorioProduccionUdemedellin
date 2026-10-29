using UnityEngine;

/// <summary>
/// Sprites sencillos generados por código para efectos de UI que no necesitan un asset
/// de arte propio: un halo suave para luces, una cápsula de extremos redondeados para
/// barras, dos degradados horizontales y unas formas de caricatura (estrella, anillo,
/// círculo y gota) con contorno oscuro para las partículas. Se crean la primera vez que
/// alguien los pide y se reutilizan desde entonces.
/// </summary>
public static class SpritesProcedurales
{
    private static Sprite brilloRadial;
    private static Sprite capsula;
    private static Sprite degradadoEntrada;
    private static Sprite degradadoSalida;
    private static Sprite estrella;
    private static Sprite anillo;
    private static Sprite circulo;
    private static Sprite gota;
    private static Sprite zonaDifusa;

    // Contorno de las formas de caricatura. El relleno es blanco, así que Image.color lo
    // tiñe; el contorno, casi negro, se queda oscuro con cualquier color.
    private static readonly Color ContornoCaricatura = new Color(0.13f, 0.05f, 0.1f, 1f);

    /// <summary>Estrella de cinco puntas, gordita, con contorno oscuro.</summary>
    public static Sprite Estrella
    {
        get
        {
            if (estrella == null)
                estrella = CrearForma("Estrella", 128, 0.1f, p => DistanciaPoligono(p, PuntasEstrella()));

            return estrella;
        }
    }

    /// <summary>Aro grueso con contorno oscuro por dentro y por fuera. Para ondas de choque.</summary>
    public static Sprite Anillo
    {
        get
        {
            if (anillo == null)
                anillo = CrearForma("Anillo", 128, 0.035f, p => Mathf.Abs(p.magnitude - 0.85f) - 0.085f);

            return anillo;
        }
    }

    /// <summary>Círculo lleno con contorno oscuro. Para chispas y humo.</summary>
    public static Sprite Circulo
    {
        get
        {
            if (circulo == null)
                circulo = CrearForma("Circulo", 64, 0.16f, p => p.magnitude - 0.92f);

            return circulo;
        }
    }

    /// <summary>
    /// Mancha blanca sin bordes: opaca en el centro y desvanecida hacia los cuatro lados (más
    /// suave arriba y abajo). Para zonas que deben leerse como un brillo, no como una caja.
    /// </summary>
    public static Sprite ZonaDifusa
    {
        get
        {
            if (zonaDifusa == null)
                zonaDifusa = CrearZonaDifusa(64, 64);

            return zonaDifusa;
        }
    }

    /// <summary>Gota con la punta hacia arriba, con contorno oscuro. Para el sudor del BAD.</summary>
    public static Sprite Gota
    {
        get
        {
            if (gota == null)
                gota = CrearForma("Gota", 64, 0.14f, p => DistanciaPoligono(p, PuntosGota()));

            return gota;
        }
    }

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

    private static Sprite CrearZonaDifusa(int ancho, int alto)
    {
        Texture2D textura = NuevaTextura(ancho, alto, "ZonaDifusa");
        Color[] pixeles = new Color[ancho * alto];

        for (int y = 0; y < alto; y++)
        {
            // Vertical: campana suave, sin bordes duros arriba ni abajo
            float v = Mathf.Abs((y + 0.5f) / alto * 2f - 1f);
            float alfaY = Mathf.SmoothStep(1f, 0f, v);
            alfaY *= alfaY;

            for (int x = 0; x < ancho; x++)
            {
                // Horizontal: plano en el centro, desvanecido en el último 30% de cada lado
                float h = Mathf.Abs((x + 0.5f) / ancho * 2f - 1f);
                float alfaX = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.4f, 1f, h));

                pixeles[y * ancho + x] = new Color(1f, 1f, 1f, alfaX * alfaY);
            }
        }

        textura.SetPixels(pixeles);
        textura.Apply();

        Sprite sprite = Sprite.Create(textura, new Rect(0, 0, ancho, alto), new Vector2(0.5f, 0.5f), 100f);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    /// <summary>
    /// Pinta una forma a partir de su distancia con signo (negativa dentro), en un espacio
    /// de -1 a 1: relleno blanco, una franja de contorno oscuro de 'grosorContorno' por
    /// dentro del borde y el borde suavizado un píxel.
    /// </summary>
    private static Sprite CrearForma(string nombre, int tamano, float grosorContorno, System.Func<Vector2, float> distancia)
    {
        Texture2D textura = NuevaTextura(tamano, tamano, nombre);
        Color[] pixeles = new Color[tamano * tamano];
        float radio = tamano * 0.5f;

        for (int y = 0; y < tamano; y++)
        {
            for (int x = 0; x < tamano; x++)
            {
                Vector2 p = new Vector2((x + 0.5f - radio) / radio, (y + 0.5f - radio) / radio);

                // En píxeles, para suavizar exactamente un píxel sea cual sea el tamaño
                float d = distancia(p) * radio;
                float alfa = Mathf.Clamp01(0.5f - d);
                float contorno = Mathf.Clamp01(d + grosorContorno * radio + 0.5f);

                Color color = Color.Lerp(Color.white, ContornoCaricatura, contorno);
                color.a = alfa;
                pixeles[y * tamano + x] = color;
            }
        }

        textura.SetPixels(pixeles);
        textura.Apply();

        Sprite sprite = Sprite.Create(textura, new Rect(0, 0, tamano, tamano), new Vector2(0.5f, 0.5f), 100f);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    private static Vector2[] PuntasEstrella()
    {
        Vector2[] puntos = new Vector2[10];

        for (int i = 0; i < puntos.Length; i++)
        {
            float angulo = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
            float distancia = i % 2 == 0 ? 0.95f : 0.48f;
            puntos[i] = new Vector2(Mathf.Cos(angulo), Mathf.Sin(angulo)) * distancia;
        }

        return puntos;
    }

    // Una punta arriba y un círculo abajo que la abraza
    private static Vector2[] PuntosGota()
    {
        const int arco = 24;
        Vector2 centro = new Vector2(0f, -0.3f);
        const float radioCirculo = 0.62f;

        Vector2[] puntos = new Vector2[arco + 1];
        puntos[0] = new Vector2(0f, 0.95f);

        // De la tangente derecha, por abajo, hasta la izquierda
        for (int i = 0; i < arco; i++)
        {
            float angulo = Mathf.Lerp(30f, -210f, i / (float)(arco - 1)) * Mathf.Deg2Rad;
            puntos[i + 1] = centro + new Vector2(Mathf.Cos(angulo), Mathf.Sin(angulo)) * radioCirculo;
        }

        return puntos;
    }

    /// <summary>Distancia con signo de un punto a un polígono (negativa dentro).</summary>
    private static float DistanciaPoligono(Vector2 p, Vector2[] v)
    {
        float d = (p - v[0]).sqrMagnitude;
        float signo = 1f;

        for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
        {
            Vector2 e = v[j] - v[i];
            Vector2 w = p - v[i];
            Vector2 b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
            d = Mathf.Min(d, b.sqrMagnitude);

            bool c1 = p.y >= v[i].y;
            bool c2 = p.y < v[j].y;
            bool c3 = e.x * w.y > e.y * w.x;
            if ((c1 && c2 && c3) || (!c1 && !c2 && !c3))
                signo = -signo;
        }

        return signo * Mathf.Sqrt(d);
    }
}
