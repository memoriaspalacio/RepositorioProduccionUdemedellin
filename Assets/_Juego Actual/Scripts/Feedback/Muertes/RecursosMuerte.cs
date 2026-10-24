using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Recursos compartidos por todos los efectos de muerte: materiales base (en Resources para
/// que sus shaders entren en la build), texturas de particula generadas por codigo, mallas de
/// roca y copias legibles de las texturas de los personajes para muestrear sus colores.
/// Todo se crea la primera vez que se pide y se reutiliza en las muertes siguientes.
/// </summary>
public static class RecursosMuerte
{
    public enum Mezcla { Alfa, Aditiva }

    const string RutaMaterialCuerpo = "EfectosMuerte/M_CuerpoMuerte";
    const string RutaMaterialParticula = "EfectosMuerte/M_ParticulaMuerte";
    const string ShaderCuerpo = "UdeM/Efectos/CuerpoMuerte";
    const string ShaderParticula = "UdeM/Efectos/ParticulaMuerte";
    const int LadoMaximoCopia = 256;

    static readonly int IdBaseMap = Shader.PropertyToID("_BaseMap");
    static readonly int IdMainTex = Shader.PropertyToID("_MainTex");
    static readonly int IdBaseColor = Shader.PropertyToID("_BaseColor");
    static readonly int IdColor = Shader.PropertyToID("_Color");
    static readonly int IdSrcBlend = Shader.PropertyToID("_SrcBlend");
    static readonly int IdDstBlend = Shader.PropertyToID("_DstBlend");
    static readonly int IdNieblaAditiva = Shader.PropertyToID("_NieblaAditiva");

    static Material baseCuerpo;
    static Material baseParticula;
    static readonly Dictionary<string, Material> materialesParticula = new Dictionary<string, Material>();
    static readonly Dictionary<Texture, Texture2D> copiasLegibles = new Dictionary<Texture, Texture2D>();

    static Texture2D texturaPunto;
    static Texture2D texturaHumo;
    static Texture2D texturaLlama;
    static Texture2D texturaEscama;
    static Mesh[] mallasRoca;

    #region Materiales

    public static Material NuevoMaterialCuerpo()
    {
        if (baseCuerpo == null)
            baseCuerpo = Cargar(RutaMaterialCuerpo, ShaderCuerpo);

        return baseCuerpo != null ? new Material(baseCuerpo) : null;
    }

    /// <summary>
    /// Material de particula compartido. La clave identifica el papel (polvo, llama...) para
    /// que todas las muertes de ese tipo usen la misma instancia.
    /// </summary>
    public static Material Particula(string clave, Texture2D textura, Mezcla mezcla, Color color)
    {
        if (materialesParticula.TryGetValue(clave, out Material existente) && existente != null)
            return existente;

        if (baseParticula == null)
            baseParticula = Cargar(RutaMaterialParticula, ShaderParticula);

        if (baseParticula == null)
            return null;

        bool aditiva = mezcla == Mezcla.Aditiva;
        Material material = new Material(baseParticula) { name = "Muerte " + clave };
        material.SetTexture(IdBaseMap, textura);
        material.SetColor(IdBaseColor, color);
        material.SetFloat(IdSrcBlend, (float)BlendMode.SrcAlpha);
        material.SetFloat(IdDstBlend, aditiva ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat(IdNieblaAditiva, aditiva ? 1f : 0f);
        material.renderQueue = (int)RenderQueue.Transparent;

        materialesParticula[clave] = material;
        return material;
    }

    static Material Cargar(string ruta, string nombreShader)
    {
        Material material = Resources.Load<Material>(ruta);
        if (material != null)
            return material;

        Shader shader = Shader.Find(nombreShader);
        if (shader != null)
            return new Material(shader);

        Debug.LogWarning($"EfectoMuerte: no se encontro el material '{ruta}' ni el shader '{nombreShader}'.");
        return null;
    }

    #endregion

    #region Colores de las texturas

    public static Texture TexturaDe(Material material)
    {
        if (material == null)
            return null;
        if (material.HasProperty(IdBaseMap))
            return material.GetTexture(IdBaseMap);
        if (material.HasProperty(IdMainTex))
            return material.GetTexture(IdMainTex);
        return null;
    }

    public static Color TinteDe(Material material)
    {
        if (material == null)
            return Color.white;
        if (material.HasProperty(IdBaseColor))
            return material.GetColor(IdBaseColor);
        if (material.HasProperty(IdColor))
            return material.GetColor(IdColor);
        return Color.white;
    }

    public static Vector4 EscalaDesplazamientoDe(Material material)
    {
        int id = material != null && material.HasProperty(IdBaseMap) ? IdBaseMap : IdMainTex;
        if (material == null || !material.HasProperty(id))
            return new Vector4(1f, 1f, 0f, 0f);

        Vector2 escala = material.GetTextureScale(id);
        Vector2 desplazamiento = material.GetTextureOffset(id);
        return new Vector4(escala.x, escala.y, desplazamiento.x, desplazamiento.y);
    }

    /// <summary>
    /// Devuelve una copia pequena y legible desde CPU de cualquier textura, aunque en su
    /// importacion no tenga Read/Write. Se copia por GPU una sola vez y queda en cache.
    /// </summary>
    public static Texture2D CopiaLegible(Texture textura)
    {
        if (textura == null)
            return null;

        if (copiasLegibles.TryGetValue(textura, out Texture2D copia) && copia != null)
            return copia;

        int ancho = Mathf.Clamp(textura.width, 1, LadoMaximoCopia);
        int alto = Mathf.Clamp(textura.height, 1, LadoMaximoCopia);

        // Destino sRGB: el blit decodifica la textura y la vuelve a codificar, asi los bytes
        // leidos son los mismos colores que se ven en la textura original.
        RenderTexture temporal = RenderTexture.GetTemporary(ancho, alto, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        RenderTexture anterior = RenderTexture.active;
        Graphics.Blit(textura, temporal);
        RenderTexture.active = temporal;

        copia = new Texture2D(ancho, alto, TextureFormat.RGBA32, false, false)
        {
            name = textura.name + " (colores muerte)",
            wrapMode = textura.wrapMode,
            filterMode = FilterMode.Bilinear
        };
        copia.ReadPixels(new Rect(0, 0, ancho, alto), 0, 0, false);
        copia.Apply(false, false);

        RenderTexture.active = anterior;
        RenderTexture.ReleaseTemporary(temporal);

        copiasLegibles[textura] = copia;
        return copia;
    }

    #endregion

    #region Texturas de particula

    /// <summary>Disco solido de borde suave: motas de polvo y brasas.</summary>
    public static Texture2D Punto
    {
        get
        {
            if (texturaPunto == null)
                texturaPunto = Generar("Muerte Punto", 32, (x, y) =>
                {
                    float r = Mathf.Sqrt(x * x + y * y);
                    return Suave(1f, 0.45f, r);
                });
            return texturaPunto;
        }
    }

    /// <summary>Bocanada difusa con algo de textura: humo y nubes de polvo.</summary>
    public static Texture2D Humo
    {
        get
        {
            if (texturaHumo == null)
                texturaHumo = Generar("Muerte Humo", 64, (x, y) =>
                {
                    float r = Mathf.Sqrt(x * x + y * y);
                    float nucleo = Mathf.Pow(Mathf.Clamp01(1f - r), 1.6f);
                    float detalle = Mathf.PerlinNoise(x * 2.3f + 5.1f, y * 2.3f + 1.7f) * 0.6f
                                  + Mathf.PerlinNoise(x * 5.2f + 9.4f, y * 5.2f + 3.3f) * 0.4f;
                    return nucleo * Mathf.Lerp(0.55f, 1.15f, detalle);
                });
            return texturaHumo;
        }
    }

    /// <summary>Lengua de fuego: bulbo redondo abajo que se afila hacia arriba.</summary>
    public static Texture2D Llama
    {
        get
        {
            if (texturaLlama == null)
                texturaLlama = Generar("Muerte Llama", 64, (x, y) =>
                {
                    const float centro = -0.35f;
                    const float radio = 0.6f;
                    const float punta = 0.95f;

                    float distancia;
                    float subida = 0f;
                    if (y <= centro)
                    {
                        distancia = Mathf.Sqrt(x * x + (y - centro) * (y - centro)) / radio;
                    }
                    else
                    {
                        subida = Mathf.Clamp01((y - centro) / (punta - centro));
                        float ancho = radio * Mathf.Pow(1f - subida, 0.8f);
                        distancia = ancho > 0.001f ? Mathf.Abs(x) / ancho : 2f;
                    }

                    return Suave(1f, 0.15f, distancia) * (1f - Suave(0.8f, 1f, subida));
                });
            return texturaLlama;
        }
    }

    /// <summary>Escama irregular: copos de ceniza.</summary>
    public static Texture2D Escama
    {
        get
        {
            if (texturaEscama == null)
                texturaEscama = Generar("Muerte Escama", 32, (x, y) =>
                {
                    float r = Mathf.Sqrt(x * x + y * y);
                    float angulo = Mathf.Atan2(y, x);
                    float borde = 0.6f + 0.22f * Mathf.Sin(3f * angulo + 1.3f) + 0.12f * Mathf.Sin(5f * angulo + 0.4f);
                    return Suave(borde, borde - 0.12f, r);
                });
            return texturaEscama;
        }
    }

    // Recibe coordenadas de -1 a 1 (y hacia arriba) y devuelve la opacidad del pixel.
    static Texture2D Generar(string nombre, int lado, System.Func<float, float, float> opacidad)
    {
        Texture2D textura = new Texture2D(lado, lado, TextureFormat.RGBA32, false, false)
        {
            name = nombre,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color32[] pixeles = new Color32[lado * lado];
        for (int j = 0; j < lado; j++)
        {
            float y = (j + 0.5f) / lado * 2f - 1f;
            for (int i = 0; i < lado; i++)
            {
                float x = (i + 0.5f) / lado * 2f - 1f;
                byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(opacidad(x, y)) * 255f);
                pixeles[j * lado + i] = new Color32(255, 255, 255, a);
            }
        }

        textura.SetPixels32(pixeles);
        textura.Apply(false, true);
        return textura;
    }

    // smoothstep que va de 0 en "desde" a 1 en "hasta" (acepta desde > hasta).
    static float Suave(float desde, float hasta, float valor)
    {
        float t = Mathf.Clamp01((valor - desde) / (hasta - desde));
        return t * t * (3f - 2f * t);
    }

    #endregion

    #region Rocas

    /// <summary>Tres trozos de roca facetados de diametro aproximado 1.</summary>
    public static Mesh[] MallasRoca
    {
        get
        {
            if (mallasRoca == null || mallasRoca[0] == null)
            {
                mallasRoca = new Mesh[3];
                for (int i = 0; i < mallasRoca.Length; i++)
                    mallasRoca[i] = CrearRoca(1234 + i * 97);
            }
            return mallasRoca;
        }
    }

    static Mesh CrearRoca(int semilla)
    {
        float phi = (1f + Mathf.Sqrt(5f)) * 0.5f;
        Vector3[] esquinas =
        {
            new Vector3(-1, phi, 0), new Vector3(1, phi, 0), new Vector3(-1, -phi, 0), new Vector3(1, -phi, 0),
            new Vector3(0, -1, phi), new Vector3(0, 1, phi), new Vector3(0, -1, -phi), new Vector3(0, 1, -phi),
            new Vector3(phi, 0, -1), new Vector3(phi, 0, 1), new Vector3(-phi, 0, -1), new Vector3(-phi, 0, 1)
        };
        int[] caras =
        {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
            1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
            4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
        };

        Random.State estadoAnterior = Random.state;
        Random.InitState(semilla);

        Vector3 estiramiento = new Vector3(Random.Range(0.75f, 1.15f), Random.Range(0.6f, 0.95f), Random.Range(0.8f, 1.2f));
        for (int i = 0; i < esquinas.Length; i++)
        {
            Vector3 v = esquinas[i].normalized * Random.Range(0.72f, 1.08f);
            esquinas[i] = Vector3.Scale(v, estiramiento) * 0.5f;
        }

        Vector2 desplazamientoUV = new Vector2(Random.value, Random.value);
        Random.state = estadoAnterior;

        // Caras planas: cada triangulo tiene sus propios vertices para que se vea facetado.
        Vector3[] vertices = new Vector3[caras.Length];
        Vector3[] normales = new Vector3[caras.Length];
        Vector2[] uvs = new Vector2[caras.Length];
        int[] triangulos = new int[caras.Length];

        for (int i = 0; i < caras.Length; i += 3)
        {
            Vector3 a = esquinas[caras[i]];
            Vector3 b = esquinas[caras[i + 1]];
            Vector3 c = esquinas[caras[i + 2]];
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;

            for (int k = 0; k < 3; k++)
            {
                Vector3 v = esquinas[caras[i + k]];
                vertices[i + k] = v;
                normales[i + k] = normal;
                uvs[i + k] = new Vector2(v.x + v.z * 0.5f, v.y + v.z * 0.35f) * 0.6f + desplazamientoUV;
                triangulos[i + k] = i + k;
            }
        }

        Mesh malla = new Mesh { name = "Muerte Roca " + semilla };
        malla.vertices = vertices;
        malla.normals = normales;
        malla.uv = uvs;
        malla.triangles = triangulos;
        malla.RecalculateBounds();
        malla.RecalculateTangents();
        return malla;
    }

    #endregion

    #region Sistemas de particulas

    /// <summary>
    /// Crea un sistema de particulas vacio, en espacio mundo y sin emision propia: los efectos
    /// emiten cada particula a mano para que salga exactamente del punto del cuerpo que toca.
    /// </summary>
    public static ParticleSystem CrearSistema(Transform padre, string nombre, Material material, int maximo, float velocidadSimulacion)
    {
        GameObject objeto = new GameObject(nombre);
        objeto.transform.SetParent(padre, false);

        ParticleSystem sistema = objeto.AddComponent<ParticleSystem>();
        sistema.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule principal = sistema.main;
        principal.duration = 1f;
        principal.loop = true;
        principal.playOnAwake = false;
        principal.simulationSpace = ParticleSystemSimulationSpace.World;
        principal.scalingMode = ParticleSystemScalingMode.Hierarchy;
        principal.maxParticles = maximo;
        principal.startSpeed = 0f;
        principal.startLifetime = 1f;
        principal.startSize = 1f;
        principal.simulationSpeed = velocidadSimulacion;

        ParticleSystem.EmissionModule emision = sistema.emission;
        emision.enabled = false;

        ParticleSystem.ShapeModule forma = sistema.shape;
        forma.enabled = false;

        ParticleSystemRenderer render = objeto.GetComponent<ParticleSystemRenderer>();
        render.sharedMaterial = material;
        render.renderMode = ParticleSystemRenderMode.Billboard;
        render.sortMode = ParticleSystemSortMode.Distance;
        render.shadowCastingMode = ShadowCastingMode.Off;
        render.receiveShadows = false;
        render.lightProbeUsage = LightProbeUsage.Off;
        render.reflectionProbeUsage = ReflectionProbeUsage.Off;
        render.minParticleSize = 0f;
        render.maxParticleSize = 2f;

        sistema.Play();
        return sistema;
    }

    public static ParticleSystem.MinMaxCurve Curva(params float[] tiemposYValores)
    {
        Keyframe[] claves = new Keyframe[tiemposYValores.Length / 2];
        for (int i = 0; i < claves.Length; i++)
            claves[i] = new Keyframe(tiemposYValores[i * 2], tiemposYValores[i * 2 + 1]);

        AnimationCurve curva = new AnimationCurve(claves);
        for (int i = 0; i < claves.Length; i++)
            curva.SmoothTangents(i, 0f);

        return new ParticleSystem.MinMaxCurve(1f, curva);
    }

    public static ParticleSystem.MinMaxGradient Degradado(Color[] colores, float[] tiemposColor, float[] alfas, float[] tiemposAlfa)
    {
        GradientColorKey[] clavesColor = new GradientColorKey[colores.Length];
        for (int i = 0; i < colores.Length; i++)
            clavesColor[i] = new GradientColorKey(colores[i], tiemposColor[i]);

        GradientAlphaKey[] clavesAlfa = new GradientAlphaKey[alfas.Length];
        for (int i = 0; i < alfas.Length; i++)
            clavesAlfa[i] = new GradientAlphaKey(alfas[i], tiemposAlfa[i]);

        Gradient degradado = new Gradient();
        degradado.SetKeys(clavesColor, clavesAlfa);
        return new ParticleSystem.MinMaxGradient(degradado);
    }

    /// <summary>Degradado blanco que solo cambia la opacidad (respeta el color de cada particula).</summary>
    public static ParticleSystem.MinMaxGradient SoloAlfa(params float[] tiemposYAlfas)
    {
        int cantidad = tiemposYAlfas.Length / 2;
        float[] alfas = new float[cantidad];
        float[] tiempos = new float[cantidad];
        for (int i = 0; i < cantidad; i++)
        {
            tiempos[i] = tiemposYAlfas[i * 2];
            alfas[i] = tiemposYAlfas[i * 2 + 1];
        }
        return Degradado(new[] { Color.white, Color.white }, new[] { 0f, 1f }, alfas, tiempos);
    }

    #endregion
}
