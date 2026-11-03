using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace UdeM.Characters
{
    /// <summary>
    /// Recursos compartidos por los efectos del sacerdote: materiales base (en Resources para que
    /// sus shaders entren en la build), la textura de cruz generada por codigo, mallas simples y
    /// los materiales de particula, que se piden a RecursosMuerte para usar el mismo shader.
    /// Todo se crea la primera vez que se pide y se reutiliza.
    /// </summary>
    public static class RecursosSacerdote
    {
        const string Carpeta = "EfectosSacerdote/M_";
        const string PrefijoShader = "UdeM/Efectos/";

        static readonly int IdGlifo = Shader.PropertyToID("_Glifo");

        static readonly Dictionary<string, Material> bases = new Dictionary<string, Material>();
        static Texture2D texturaCruz;
        static Mesh mallaQuad;
        static Mesh mallaEsfera;

        #region Materiales

        /// <summary>
        /// Copia nueva del material de un efecto (nombre del shader sin "UdeM/Efectos/").
        /// Si el shader usa la textura de cruz, ya viene asignada.
        /// </summary>
        public static Material NuevoMaterial(string nombre)
        {
            if (!bases.TryGetValue(nombre, out Material baseMaterial) || baseMaterial == null)
            {
                baseMaterial = Resources.Load<Material>(Carpeta + nombre);

                if (baseMaterial == null)
                {
                    Shader shader = Shader.Find(PrefijoShader + nombre);
                    if (shader != null)
                        baseMaterial = new Material(shader);
                }

                if (baseMaterial == null)
                {
                    Debug.LogWarning($"Efectos del sacerdote: no se encontro el material '{Carpeta + nombre}' ni el shader '{PrefijoShader + nombre}'.");
                    return null;
                }

                bases[nombre] = baseMaterial;
            }

            Material material = new Material(baseMaterial) { name = "Sacerdote " + nombre };
            if (material.HasProperty(IdGlifo))
                material.SetTexture(IdGlifo, Cruz);
            return material;
        }

        /// <summary>Particulas con forma de cruz (bloqueos, invocaciones, cruces rotas).</summary>
        public static Material ParticulaCruz =>
            RecursosMuerte.Particula("Sacerdote Cruz", Cruz, RecursosMuerte.Mezcla.Aditiva, Color.white);

        /// <summary>Chispas y brasas.</summary>
        public static Material ParticulaChispa =>
            RecursosMuerte.Particula("Sacerdote Chispa", RecursosMuerte.Punto, RecursosMuerte.Mezcla.Aditiva, Color.white);

        /// <summary>Humo oscuro del infierno.</summary>
        public static Material ParticulaHumo =>
            RecursosMuerte.Particula("Sacerdote Humo", RecursosMuerte.Humo, RecursosMuerte.Mezcla.Alfa, Color.white);

        /// <summary>Astillas de las cruces al romperse.</summary>
        public static Material ParticulaAstilla =>
            RecursosMuerte.Particula("Sacerdote Astilla", RecursosMuerte.Escama, RecursosMuerte.Mezcla.Alfa, Color.white);

        #endregion

        #region Textura de cruz

        /// <summary>Cruz latina blanca con halo suave (el alfa es la forma).</summary>
        public static Texture2D Cruz
        {
            get
            {
                if (texturaCruz != null)
                    return texturaCruz;

                const int lado = 64;
                texturaCruz = new Texture2D(lado, lado, TextureFormat.RGBA32, false, false)
                {
                    name = "Sacerdote Cruz",
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

                        // Distancia a la cruz: palo vertical y travesano mas arriba del centro.
                        float palo = DistanciaRectangulo(x, y + 0.05f, 0.11f, 0.82f);
                        float travesano = DistanciaRectangulo(x, y - 0.32f, 0.5f, 0.11f);
                        float distancia = Mathf.Min(palo, travesano);

                        float forma = 1f - Suave(0f, 0.05f, distancia);
                        float halo = Mathf.Pow(Mathf.Clamp01(1f - distancia / 0.35f), 2f) * 0.45f;
                        float alfa = Mathf.Clamp01(Mathf.Max(forma, halo));

                        pixeles[j * lado + i] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alfa * 255f));
                    }
                }

                texturaCruz.SetPixels32(pixeles);
                texturaCruz.Apply(false, true);
                return texturaCruz;
            }
        }

        // Distancia (positiva fuera, 0 dentro) a un rectangulo centrado de medio ancho y medio alto.
        static float DistanciaRectangulo(float x, float y, float medioAncho, float medioAlto)
        {
            float dx = Mathf.Max(Mathf.Abs(x) - medioAncho, 0f);
            float dy = Mathf.Max(Mathf.Abs(y) - medioAlto, 0f);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        static float Suave(float desde, float hasta, float valor)
        {
            float t = Mathf.Clamp01((valor - desde) / (hasta - desde));
            return t * t * (3f - 2f * t);
        }

        #endregion

        #region Mallas y objetos

        /// <summary>Quad de 1x1 acostado sobre el suelo (mira hacia arriba), uv de 0 a 1.</summary>
        public static Mesh QuadSuelo
        {
            get
            {
                if (mallaQuad != null)
                    return mallaQuad;

                mallaQuad = new Mesh { name = "Sacerdote Quad Suelo" };
                mallaQuad.vertices = new[]
                {
                    new Vector3(-0.5f, 0f, -0.5f),
                    new Vector3(0.5f, 0f, -0.5f),
                    new Vector3(-0.5f, 0f, 0.5f),
                    new Vector3(0.5f, 0f, 0.5f)
                };
                mallaQuad.uv = new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f)
                };
                mallaQuad.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
                mallaQuad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
                mallaQuad.RecalculateBounds();
                return mallaQuad;
            }
        }

        /// <summary>Esfera de diametro 1 (la de Unity, sin collider).</summary>
        public static Mesh Esfera
        {
            get
            {
                if (mallaEsfera != null)
                    return mallaEsfera;

                GameObject temporal = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                mallaEsfera = temporal.GetComponent<MeshFilter>().sharedMesh;
                Object.DestroyImmediate(temporal);
                return mallaEsfera;
            }
        }

        /// <summary>Objeto con una malla y un material, sin sombras ni sondas.</summary>
        public static MeshRenderer CrearMalla(string nombre, Mesh malla, Material material, Transform padre = null)
        {
            GameObject objeto = new GameObject(nombre);
            if (padre != null)
                objeto.transform.SetParent(padre, false);

            objeto.AddComponent<MeshFilter>().sharedMesh = malla;
            MeshRenderer render = objeto.AddComponent<MeshRenderer>();
            render.sharedMaterial = material;
            ConfigurarRender(render);
            return render;
        }

        public static void ConfigurarRender(Renderer render)
        {
            render.shadowCastingMode = ShadowCastingMode.Off;
            render.receiveShadows = false;
            render.lightProbeUsage = LightProbeUsage.Off;
            render.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        /// <summary>
        /// Renderers que realmente se ven bajo una raiz: activos y habilitados, sin contar
        /// particulas, lineas ni estelas (por ejemplo, la cruz trae un cubo deshabilitado).
        /// </summary>
        public static Renderer[] RenderersVisibles(Transform raiz)
        {
            List<Renderer> visibles = new List<Renderer>();
            if (raiz == null)
                return visibles.ToArray();

            Renderer[] todos = raiz.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < todos.Length; i++)
            {
                Renderer render = todos[i];
                if (!render.enabled)
                    continue;
                if (render is ParticleSystemRenderer || render is TrailRenderer || render is LineRenderer)
                    continue;
                visibles.Add(render);
            }

            return visibles.ToArray();
        }

        /// <summary>Limites combinados de varios renderers.</summary>
        public static Bounds Limites(Renderer[] renderers, Vector3 respaldo)
        {
            Bounds limites = new Bounds(respaldo, Vector3.zero);
            bool primero = true;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                    continue;
                if (primero)
                {
                    limites = renderers[i].bounds;
                    primero = false;
                }
                else
                {
                    limites.Encapsulate(renderers[i].bounds);
                }
            }
            return limites;
        }

        #endregion

        #region Capas de material

        /// <summary>Agrega un material extra encima de cada renderer y devuelve los originales.</summary>
        public static Material[][] AgregarCapa(Renderer[] renderers, Material capa)
        {
            Material[][] originales = new Material[renderers.Length][];
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                    continue;

                Material[] actuales = renderers[i].sharedMaterials;
                originales[i] = actuales;

                Material[] conCapa = new Material[actuales.Length + 1];
                actuales.CopyTo(conCapa, 0);
                conCapa[actuales.Length] = capa;
                renderers[i].sharedMaterials = conCapa;
            }
            return originales;
        }

        /// <summary>Devuelve a cada renderer los materiales que tenia antes de la capa.</summary>
        public static void QuitarCapa(Renderer[] renderers, Material[][] originales)
        {
            if (renderers == null || originales == null)
                return;

            for (int i = 0; i < renderers.Length && i < originales.Length; i++)
            {
                if (renderers[i] != null && originales[i] != null)
                    renderers[i].sharedMaterials = originales[i];
            }
        }

        #endregion

        #region Particulas

        /// <summary>Sistema de particulas en espacio mundo que se emite a mano.</summary>
        public static ParticleSystem CrearSistema(Transform padre, string nombre, Material material, int maximo)
        {
            ParticleSystem sistema = RecursosMuerte.CrearSistema(padre, nombre, material, maximo, 1f);

            // Las particulas se encogen y se apagan al final de su vida.
            ParticleSystem.SizeOverLifetimeModule tamano = sistema.sizeOverLifetime;
            tamano.enabled = true;
            tamano.size = RecursosMuerte.Curva(0f, 0.6f, 0.15f, 1f, 1f, 0f);

            ParticleSystem.ColorOverLifetimeModule color = sistema.colorOverLifetime;
            color.enabled = true;
            color.color = RecursosMuerte.SoloAlfa(0f, 0f, 0.1f, 1f, 0.7f, 0.8f, 1f, 0f);

            return sistema;
        }

        public static void Emitir(ParticleSystem sistema, Vector3 posicion, Vector3 velocidad, float tamano, float vida, Color color, float rotacion = 0f, float giro = 0f)
        {
            if (sistema == null)
                return;

            ParticleSystem.EmitParams parametros = new ParticleSystem.EmitParams
            {
                position = posicion,
                velocity = velocidad,
                startSize = tamano,
                startLifetime = vida,
                startColor = color,
                rotation = rotacion,
                angularVelocity = giro
            };
            sistema.Emit(parametros, 1);
        }

        /// <summary>Cantidad de particulas ajustada a la calidad elegida en el menu de graficos.</summary>
        public static int Cantidad(float normal)
        {
            return Mathf.Max(1, Mathf.RoundToInt(normal * GestorGraficos.MultiplicadorEfectos));
        }

        public static void Ruido(ParticleSystem sistema, float fuerza, float frecuencia)
        {
            ParticleSystem.NoiseModule ruido = sistema.noise;
            ruido.enabled = true;
            ruido.strength = fuerza;
            ruido.frequency = frecuencia;
            ruido.scrollSpeed = 0.6f;
            ruido.damping = true;
            ruido.octaveCount = 1;
            ruido.quality = ParticleSystemNoiseQuality.Medium;
        }

        /// <summary>Luz puntual temporal para destellos (bloqueos, invocaciones, cruces rotas).</summary>
        public static Light CrearLuz(Transform padre, Color color, float alcance)
        {
            GameObject objeto = new GameObject("Luz");
            objeto.transform.SetParent(padre, false);
            Light luz = objeto.AddComponent<Light>();
            luz.type = LightType.Point;
            luz.color = color;
            luz.range = alcance;
            luz.intensity = 0f;
            luz.shadows = LightShadows.None;
            luz.renderMode = LightRenderMode.Auto;
            return luz;
        }

        #endregion
    }
}
