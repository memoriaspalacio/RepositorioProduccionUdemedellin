using UnityEngine;

namespace UdeM.Characters
{
    /// <summary>
    /// Animaciones de las cruces del sacerdote, separadas de su logica: aparecer creciendo con un
    /// destello, flotar y latir en reposo, cargar girando antes de atacar, volar con estela e
    /// inclinadas, aplastarse al aterrizar, enderezarse, brillar en rojo al invocar al sacerdote
    /// y estallar en astillas y cruces de luz al romperse. MonkCrossBehaviour lo agrega solo.
    /// </summary>
    [DisallowMultipleComponent]
    public class MonkCrossVisual : MonoBehaviour
    {
        [Header("Colores")]
        [SerializeField] private Color colorDivino = new Color(1f, 0.82f, 0.4f, 1f);
        [SerializeField] private Color colorInfernal = new Color(1f, 0.28f, 0.06f, 1f);

        [Header("Reposo")]
        [Min(0f)]
        [SerializeField] private float amplitudFlotar = 0.08f;
        [Min(0f)]
        [SerializeField] private float frecuenciaFlotar = 1.6f;

        [Header("Movimientos")]
        [Min(0.05f)]
        [SerializeField] private float duracionAparicion = 0.7f;
        [Tooltip("Cuanto sube la cruz mientras carga un ataque.")]
        [Min(0f)]
        [SerializeField] private float elevacionCarga = 0.35f;
        [Tooltip("Cuanto sube la cruz mientras invoca al sacerdote.")]
        [Min(0f)]
        [SerializeField] private float elevacionInvocacion = 0.6f;
        [Tooltip("Velocidad con la que se endereza despues de volar.")]
        [Min(0f)]
        [SerializeField] private float velocidadEnderezar = 8f;

        static readonly int IdIntensidad = Shader.PropertyToID("_Intensidad");
        static readonly int IdColor = Shader.PropertyToID("_Color");
        static readonly int IdDensidad = Shader.PropertyToID("_Densidad");
        static readonly int IdVelocidad = Shader.PropertyToID("_Velocidad");

        private MonkCrossBehaviour cruz;
        private Transform modelo;
        private Vector3 modeloLocalBase;
        private Vector3 escalaMundoBase = Vector3.one;
        private Renderer[] renderers = new Renderer[0];
        private Material[][] materialesOriginales;
        private Material brillo;
        private Material materialEstela;
        private Light luz;
        private TrailRenderer estela;

        private float fase;
        private float inicioAparicion = -100f;
        private float inicioAterrizaje = -100f;
        private float inicioCarga = -100f;
        private float finCarga = -100f;
        private float carga;
        private bool invocando;
        private float invocacion;
        private float destello;
        private float elevacion;
        private bool inicializada;

        /// <summary>Multiplica el giro de la cruz mientras esta poseida (carga e invocacion).</summary>
        public float MultiplicadorGiro { get; private set; } = 1f;

        /// <summary>Centro visible de la cruz (de donde sale su enlace con el sacerdote).</summary>
        public Vector3 Centro
        {
            get
            {
                Bounds limites = RecursosSacerdote.Limites(renderers, transform.position);
                return limites.size.sqrMagnitude > 0f ? limites.center : transform.position;
            }
        }

        public void Inicializar(int indice)
        {
            if (inicializada)
                return;
            inicializada = true;

            cruz = GetComponent<MonkCrossBehaviour>();
            fase = indice * 1.7f;
            escalaMundoBase = transform.lossyScale;

            renderers = RecursosSacerdote.RenderersVisibles(transform);
            modelo = BuscarModelo();
            if (modelo != null)
                modeloLocalBase = modelo.localPosition;

            brillo = RecursosSacerdote.NuevoMaterial("BrilloDivino");
            if (brillo != null)
                materialesOriginales = RecursosSacerdote.AgregarCapa(renderers, brillo);

            luz = RecursosSacerdote.CrearLuz(transform, colorDivino, 2.5f);
            CrearEstela();
        }

        // El hijo directo de la cruz que contiene la malla visible: es lo que flota y sube.
        // Si la malla esta en la raiz, no se mueve (la raiz la controla la logica).
        private Transform BuscarModelo()
        {
            if (renderers.Length == 0)
                return null;

            Transform actual = renderers[0].transform;
            if (actual == transform)
                return null;

            while (actual.parent != null && actual.parent != transform)
                actual = actual.parent;

            return actual.parent == transform ? actual : null;
        }

        private void CrearEstela()
        {
            materialEstela = RecursosSacerdote.NuevoMaterial("EnlaceDivino");
            if (materialEstela == null)
                return;

            materialEstela.SetFloat(IdDensidad, 1.2f);
            materialEstela.SetFloat(IdVelocidad, -2f);

            GameObject objeto = new GameObject("Estela");
            objeto.transform.SetParent(transform, false);

            estela = objeto.AddComponent<TrailRenderer>();
            estela.sharedMaterial = materialEstela;
            estela.time = 0.22f;
            estela.minVertexDistance = 0.05f;
            estela.widthMultiplier = 0.22f;
            estela.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
            estela.alignment = LineAlignment.View;
            estela.textureMode = LineTextureMode.Stretch;
            estela.emitting = false;

            Gradient degradado = new Gradient();
            degradado.SetKeys(
                new[] { new GradientColorKey(colorDivino, 0f), new GradientColorKey(colorDivino, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            estela.colorGradient = degradado;
            RecursosSacerdote.ConfigurarRender(estela);
        }

        #region Eventos de la cruz

        /// <summary>Recien creada: crece desde cero con un destello.</summary>
        public void AlAparecer()
        {
            inicioAparicion = Time.time;
            destello = 1f;

            FxTemporalSacerdote fx = FxTemporalSacerdote.Crear("Aparicion cruz", Centro, 0.3f);
            fx.Destello(colorDivino, 3f, 3f, 0.5f);
            ParticleSystem chispas = fx.Sistema("Chispas", RecursosSacerdote.ParticulaChispa, RecursosSacerdote.Cantidad(30));
            int cantidad = RecursosSacerdote.Cantidad(18);
            for (int i = 0; i < cantidad; i++)
            {
                Vector3 salida = Random.onUnitSphere;
                salida.y = Mathf.Abs(salida.y);
                RecursosSacerdote.Emitir(chispas, fx.transform.position, salida * Random.Range(0.8f, 2f), Random.Range(0.03f, 0.07f), Random.Range(0.4f, 0.8f), colorDivino);
            }
        }

        /// <summary>El sacerdote empieza a rezar por esta cruz: sube, gira mas rapido y se enciende.</summary>
        public void PrepararAtaque(float duracion)
        {
            inicioCarga = Time.time;
            finCarga = Time.time + Mathf.Max(0.15f, duracion);
        }

        /// <summary>Sale disparada: estela encendida y destello.</summary>
        public void AlDespegar()
        {
            finCarga = Time.time;
            destello = Mathf.Max(destello, 0.8f);
            if (estela != null)
            {
                estela.Clear();
                estela.emitting = true;
            }
        }

        /// <summary>Termina de volar: se aplasta un instante y apaga la estela.</summary>
        public void AlAterrizar()
        {
            inicioAterrizaje = Time.time;
            DetenerEstela();
        }

        /// <summary>Vuelve volando a rodear al sacerdote.</summary>
        public void AlVolver()
        {
            if (estela != null)
                estela.emitting = true;
        }

        public void AlTerminarVuelta()
        {
            DetenerEstela();
        }

        public void DetenerEstela()
        {
            if (estela != null)
                estela.emitting = false;
        }

        /// <summary>Invoca al sacerdote: se eleva, tiembla y arde en rojo hasta que el sale.</summary>
        public void Invocar()
        {
            invocando = true;
        }

        public void TerminarInvocacion()
        {
            invocando = false;
            destello = 1f;
        }

        /// <summary>Se rompe: estallido de astillas, chispas y cruces de luz.</summary>
        public void AlMorir()
        {
            Vector3 centro = Centro;
            Color color = Color.Lerp(colorDivino, colorInfernal, 0.3f);

            FxTemporalSacerdote fx = FxTemporalSacerdote.Crear("Cruz rota", centro, 0.5f);
            fx.Destello(color, 6f, 4f, 0.5f);

            ParticleSystem cruces = fx.Sistema("Cruces", RecursosSacerdote.ParticulaCruz, RecursosSacerdote.Cantidad(30));
            ParticleSystem.MainModule principalCruces = cruces.main;
            principalCruces.gravityModifier = -0.15f;

            ParticleSystem astillas = fx.Sistema("Astillas", RecursosSacerdote.ParticulaAstilla, RecursosSacerdote.Cantidad(60));
            ParticleSystem.MainModule principalAstillas = astillas.main;
            principalAstillas.gravityModifier = 1.2f;

            ParticleSystem chispas = fx.Sistema("Chispas", RecursosSacerdote.ParticulaChispa, RecursosSacerdote.Cantidad(60));
            ParticleSystem.MainModule principalChispas = chispas.main;
            principalChispas.gravityModifier = 0.5f;

            int cantidadCruces = RecursosSacerdote.Cantidad(10);
            for (int i = 0; i < cantidadCruces; i++)
            {
                Vector3 salida = Random.onUnitSphere;
                salida.y = Mathf.Abs(salida.y) + 0.4f;
                RecursosSacerdote.Emitir(cruces, centro, salida * Random.Range(0.6f, 1.6f), Random.Range(0.2f, 0.35f), Random.Range(0.8f, 1.3f), colorDivino, Random.Range(-20f, 20f), Random.Range(-60f, 60f));
            }

            Color colorMadera = new Color(0.35f, 0.24f, 0.12f, 1f);
            int cantidadAstillas = RecursosSacerdote.Cantidad(34);
            for (int i = 0; i < cantidadAstillas; i++)
            {
                Vector3 salida = Random.onUnitSphere;
                salida.y = Mathf.Abs(salida.y) * 1.2f;
                RecursosSacerdote.Emitir(astillas, centro + Random.insideUnitSphere * 0.25f, salida * Random.Range(1.5f, 3.5f), Random.Range(0.06f, 0.16f), Random.Range(0.7f, 1.2f), colorMadera, Random.Range(0f, 360f), Random.Range(-400f, 400f));
            }

            int cantidadChispas = RecursosSacerdote.Cantidad(40);
            for (int i = 0; i < cantidadChispas; i++)
            {
                RecursosSacerdote.Emitir(chispas, centro, Random.onUnitSphere * Random.Range(1.5f, 4f), Random.Range(0.03f, 0.07f), Random.Range(0.3f, 0.7f), color);
            }

            DetenerEstela();
        }

        #endregion

        private void LateUpdate()
        {
            if (!inicializada)
                return;

            float t = Time.time;
            float dt = Time.deltaTime;

            ActualizarEscala(t);

            // Carga: sube de 0 a 1 mientras dura la oracion y se suelta al despegar.
            float cargaObjetivo = t < finCarga
                ? Mathf.Clamp01((t - inicioCarga) / Mathf.Max(0.01f, finCarga - inicioCarga))
                : 0f;
            carga = Mathf.MoveTowards(carga, cargaObjetivo, dt * (cargaObjetivo > carga ? 6f : 3f));
            invocacion = Mathf.MoveTowards(invocacion, invocando ? 1f : 0f, dt * 2f);
            destello = Mathf.MoveTowards(destello, 0f, dt * 2.5f);

            MultiplicadorGiro = 1f + carga * 5f + invocacion * 3f;

            float elevacionObjetivo = carga * elevacionCarga + invocacion * elevacionInvocacion;
            elevacion = Mathf.Lerp(elevacion, elevacionObjetivo, 1f - Mathf.Exp(-10f * dt));

            MoverModelo(t);
            Enderezar(dt);
            ActualizarBrillo(t);
        }

        // Escala de mundo fija aunque cambie de padre (poseida o suelta), con la aparicion y el aterrizaje encima.
        private void ActualizarEscala(float t)
        {
            float aparicion = Mathf.Clamp01((t - inicioAparicion) / duracionAparicion);
            float crecer = SalidaElastica(aparicion);

            float aterrizaje = (t - inicioAterrizaje) / 0.28f;
            float aplastar = aterrizaje >= 0f && aterrizaje <= 1f
                ? Mathf.Sin(aterrizaje * Mathf.PI) * (1f - aterrizaje) * 0.35f
                : 0f;

            Vector3 factor = new Vector3(1f + aplastar * 0.6f, 1f - aplastar, 1f + aplastar * 0.6f) * crecer;
            Vector3 mundo = Vector3.Scale(escalaMundoBase, factor);

            Vector3 escalaPadre = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
            transform.localScale = new Vector3(
                mundo.x / NoCero(escalaPadre.x),
                mundo.y / NoCero(escalaPadre.y),
                mundo.z / NoCero(escalaPadre.z)
            );
        }

        // Flota, sube al cargar o invocar y tiembla mientras invoca. Solo mueve el modelo hijo.
        private void MoverModelo(float t)
        {
            if (modelo == null)
                return;

            float flotar = Mathf.Sin(t * frecuenciaFlotar + fase) * amplitudFlotar;
            Vector3 temblor = new Vector3(
                Mathf.Sin(t * 41f + fase),
                0f,
                Mathf.Cos(t * 37f + fase)
            ) * 0.025f * invocacion;

            Vector3 desplazamiento = Vector3.up * (flotar + elevacion) + temblor;
            modelo.localPosition = modeloLocalBase + transform.InverseTransformVector(desplazamiento);

            // Mientras invoca y no esta poseida, gira sola sobre su sitio.
            if (invocacion > 0.01f &&
                cruz != null &&
                !cruz.IsPossessed &&
                !cruz.IsAttacking)
            {
                transform.Rotate(Vector3.up, 220f * invocacion * Time.deltaTime, Space.World);
            }
        }

        // Al dejar de volar vuelve a quedar vertical sin perder hacia donde mira.
        private void Enderezar(float dt)
        {
            if (cruz == null || cruz.IsAttacking)
                return;

            Vector3 frente = transform.forward;
            frente.y = 0f;
            if (frente.sqrMagnitude < 0.0001f)
                frente = Vector3.forward;

            Quaternion derecha = Quaternion.LookRotation(frente.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, derecha, 1f - Mathf.Exp(-velocidadEnderezar * dt));
        }

        private void ActualizarBrillo(float t)
        {
            float reposo = 0.35f + 0.12f * Mathf.Sin(t * 2.4f + fase);
            float intensidad = reposo + carga * 1.6f + invocacion * 2.2f + destello * 1.5f;
            Color color = Color.Lerp(colorDivino, colorInfernal, invocacion);

            if (brillo != null)
            {
                brillo.SetFloat(IdIntensidad, intensidad);
                brillo.SetColor(IdColor, color * 3f);
            }

            if (luz != null)
            {
                luz.color = color;
                luz.intensity = 0.4f + carga * 2f + invocacion * 3.5f + destello * 2f;
                luz.range = 2.5f + invocacion * 2f;
            }
        }

        private static float SalidaElastica(float x)
        {
            if (x >= 1f)
                return 1f;

            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float y = x - 1f;
            return 1f + c3 * y * y * y + c1 * y * y;
        }

        private static float NoCero(float valor)
        {
            return Mathf.Abs(valor) < 0.0001f ? 0.0001f : valor;
        }

        private void OnDestroy()
        {
            RecursosSacerdote.QuitarCapa(renderers, materialesOriginales);
            if (brillo != null)
                Destroy(brillo);
            if (materialEstela != null)
                Destroy(materialEstela);
        }
    }
}
