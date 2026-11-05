using System.Collections.Generic;
using UnityEngine;

namespace UdeM.Characters
{
    /// <summary>
    /// Proteccion divina del sacerdote: cada cruz viva le manda un haz de energia y entre todas
    /// sostienen un escudo a su alrededor. Con menos cruces los haces y el escudo se debilitan
    /// (mas finos, mas apagados, con grietas y parpadeo). Al golpear al sacerdote el escudo se
    /// enciende y suelta cruces de luz desde el punto del golpe. MonkBehaviour lo agrega solo.
    /// </summary>
    [DisallowMultipleComponent]
    public class EnlaceDivino : MonoBehaviour
    {
        [Header("Enlaces")]
        [SerializeField] private Color colorEnlace = new Color(1f, 0.82f, 0.45f, 1f);
        [Min(0.005f)]
        [SerializeField] private float anchoMaximo = 0.09f;
        [Min(0.005f)]
        [SerializeField] private float anchoMinimo = 0.025f;
        [Tooltip("Cuanto se curvan los haces hacia arriba.")]
        [Min(0f)]
        [SerializeField] private float arco = 0.35f;

        [Header("Escudo")]
        [Tooltip("Margen del escudo alrededor del cuerpo.")]
        [Min(1f)]
        [SerializeField] private float margenEscudo = 1.2f;
        [SerializeField] private Color colorBloqueo = new Color(1f, 0.85f, 0.4f, 1f);
        [Tooltip("Segundos que dura el brillo del escudo tras un golpe.")]
        [Min(0.05f)]
        [SerializeField] private float duracionImpacto = 0.6f;

        static readonly int IdFuerza = Shader.PropertyToID("_Fuerza");
        static readonly int IdParpadeo = Shader.PropertyToID("_Parpadeo");
        static readonly int IdVisible = Shader.PropertyToID("_Visible");
        static readonly int IdImpacto = Shader.PropertyToID("_Impacto");
        static readonly int IdDireccionImpacto = Shader.PropertyToID("_DireccionImpacto");

        const int PuntosPorEnlace = 12;

        private Transform modelo;
        private float alturaCentro = 1f;
        private float radioEscudo = 0.9f;

        private Transform raiz;
        private Material materialEnlace;
        private Material materialEscudo;
        private MeshRenderer escudo;
        private Light luzEscudo;
        private ParticleSystem cruces;
        private ParticleSystem chispas;

        private readonly Dictionary<MonkCrossBehaviour, LineRenderer> enlaces = new Dictionary<MonkCrossBehaviour, LineRenderer>();
        private readonly List<MonkCrossBehaviour> porQuitar = new List<MonkCrossBehaviour>();
        private readonly Vector3[] puntos = new Vector3[PuntosPorEnlace];

        private IList<MonkCrossBehaviour> crucesSacerdote;
        private float fuerza = 1f;
        private float fuerzaObjetivo = 1f;
        private float impacto;
        private float visibilidad = 1f;
        private float visibilidadObjetivo = 1f;
        private bool roto;

        /// <summary>Centro del cuerpo del sacerdote (de donde salen los haces y el escudo).</summary>
        public Vector3 Centro =>
            modelo != null
                ? modelo.position + Vector3.up * alturaCentro
                : transform.position + Vector3.up * alturaCentro;

        public void Configurar(Transform modeloVisual, IList<MonkCrossBehaviour> crucesDelSacerdote)
        {
            modelo = modeloVisual;
            crucesSacerdote = crucesDelSacerdote;

            Renderer[] renderers = RecursosSacerdote.RenderersVisibles(modelo != null ? modelo : transform);
            Bounds limites = RecursosSacerdote.Limites(renderers, transform.position + Vector3.up);
            Vector3 origen = modelo != null ? modelo.position : transform.position;
            alturaCentro = Mathf.Max(0.3f, limites.center.y - origen.y);
            radioEscudo = Mathf.Max(0.5f, Mathf.Max(limites.extents.x, limites.extents.z, limites.extents.y) * margenEscudo);

            // Vive fuera del sacerdote para no saltar con el ni entrar en su efecto de muerte.
            raiz = new GameObject("Enlace divino " + name).transform;

            materialEnlace = RecursosSacerdote.NuevoMaterial("EnlaceDivino");
            materialEscudo = RecursosSacerdote.NuevoMaterial("EscudoDivino");

            if (materialEscudo != null)
            {
                escudo = RecursosSacerdote.CrearMalla("Escudo", RecursosSacerdote.Esfera, materialEscudo, raiz);
                escudo.transform.localScale = Vector3.one * (radioEscudo * 2f);
                luzEscudo = RecursosSacerdote.CrearLuz(escudo.transform, colorBloqueo, radioEscudo * 4f);
            }

            cruces = RecursosSacerdote.CrearSistema(raiz, "Cruces de luz", RecursosSacerdote.ParticulaCruz, RecursosSacerdote.Cantidad(120));
            chispas = RecursosSacerdote.CrearSistema(raiz, "Chispas", RecursosSacerdote.ParticulaChispa, RecursosSacerdote.Cantidad(200));

            ParticleSystem.MainModule principal = chispas.main;
            principal.gravityModifier = 0.6f;
        }

        /// <summary>Fuerza de la proteccion segun las cruces que siguen vivas.</summary>
        public void ActualizarFuerza(int vivas, int total)
        {
            fuerzaObjetivo = total > 0 ? Mathf.Clamp01((float)vivas / total) : 0f;
        }

        /// <summary>Muestra u oculta el escudo (por ejemplo mientras el sacerdote esta bajo tierra).</summary>
        public void MostrarEscudo(bool visible)
        {
            visibilidadObjetivo = visible ? 1f : 0f;
        }

        /// <summary>El golpe rebota en el escudo: se enciende y suelta cruces desde el impacto.</summary>
        public void Bloquear(Vector3? origen)
        {
            if (roto)
                return;

            Vector3 centro = Centro;
            Vector3 direccion = origen.HasValue ? origen.Value - centro : transform.forward;
            direccion.y *= 0.3f;
            if (direccion.sqrMagnitude < 0.0001f)
                direccion = transform.forward;
            direccion.Normalize();

            impacto = 1f;
            visibilidad = Mathf.Max(visibilidad, 1f);
            if (materialEscudo != null)
                materialEscudo.SetVector(IdDireccionImpacto, direccion);

            Vector3 punto = centro + direccion * radioEscudo;
            float potencia = 0.5f + 0.5f * fuerza;

            // Cruces de luz que salen despedidas del punto del golpe.
            int cantidadCruces = RecursosSacerdote.Cantidad(Mathf.Lerp(6f, 14f, fuerza));
            for (int i = 0; i < cantidadCruces; i++)
            {
                Vector3 salida = (direccion + Random.insideUnitSphere * 0.8f).normalized;
                salida.y = Mathf.Abs(salida.y) * 0.6f + 0.3f;
                RecursosSacerdote.Emitir(
                    cruces,
                    punto + Random.insideUnitSphere * 0.1f,
                    salida * Random.Range(1.2f, 2.6f) * potencia,
                    Random.Range(0.18f, 0.32f),
                    Random.Range(0.5f, 0.9f),
                    colorBloqueo,
                    Random.Range(-15f, 15f),
                    Random.Range(-90f, 90f)
                );
            }

            int cantidadChispas = RecursosSacerdote.Cantidad(24);
            for (int i = 0; i < cantidadChispas; i++)
            {
                Vector3 salida = (direccion + Random.insideUnitSphere).normalized;
                RecursosSacerdote.Emitir(chispas, punto, salida * Random.Range(2f, 4.5f), Random.Range(0.03f, 0.07f), Random.Range(0.25f, 0.5f), colorBloqueo);
            }
        }

        /// <summary>Se rompe toda la proteccion (no quedan cruces): estallan el escudo y los haces.</summary>
        public void Romper()
        {
            if (roto)
                return;

            roto = true;
            Vector3 centro = Centro;

            FxTemporalSacerdote fx = FxTemporalSacerdote.Crear("Escudo roto", centro, 0.5f);
            fx.Destello(colorBloqueo, 8f, radioEscudo * 6f, 0.6f);
            ParticleSystem estallido = fx.Sistema("Cruces", RecursosSacerdote.ParticulaCruz, RecursosSacerdote.Cantidad(60));
            ParticleSystem.MainModule principal = estallido.main;
            principal.gravityModifier = 0.25f;

            int cantidad = RecursosSacerdote.Cantidad(36);
            for (int i = 0; i < cantidad; i++)
            {
                Vector3 salida = Random.onUnitSphere;
                salida.y = Mathf.Abs(salida.y) * 0.8f + 0.1f;
                RecursosSacerdote.Emitir(
                    estallido,
                    centro + salida * radioEscudo,
                    salida * Random.Range(1.5f, 3.5f),
                    Random.Range(0.2f, 0.38f),
                    Random.Range(0.7f, 1.2f),
                    colorBloqueo,
                    Random.Range(-20f, 20f),
                    Random.Range(-120f, 120f)
                );
            }

            foreach (KeyValuePair<MonkCrossBehaviour, LineRenderer> par in enlaces)
            {
                if (par.Value != null)
                    Destroy(par.Value.gameObject);
            }
            enlaces.Clear();

            if (escudo != null)
                Destroy(escudo.gameObject);
        }

        private void LateUpdate()
        {
            if (raiz == null)
                return;

            float dt = Time.deltaTime;
            fuerza = Mathf.MoveTowards(fuerza, fuerzaObjetivo, dt * 1.5f);
            impacto = Mathf.MoveTowards(impacto, 0f, dt / duracionImpacto);
            visibilidad = Mathf.MoveTowards(visibilidad, visibilidadObjetivo, dt * 3f);

            if (roto)
                return;

            Vector3 centro = Centro;
            ActualizarEscudo(centro);
            ActualizarEnlaces(centro);
        }

        private void ActualizarEscudo(Vector3 centro)
        {
            if (escudo == null)
                return;

            escudo.transform.position = centro;

            // Se infla un poco al recibir un golpe.
            float pulso = 1f + 0.08f * impacto;
            escudo.transform.localScale = Vector3.one * (radioEscudo * 2f * pulso);
            escudo.enabled = visibilidad > 0.01f;

            materialEscudo.SetFloat(IdFuerza, fuerza);
            materialEscudo.SetFloat(IdVisible, 0.35f * visibilidad);
            materialEscudo.SetFloat(IdImpacto, impacto);

            if (luzEscudo != null)
                luzEscudo.intensity = 4f * impacto * impacto;
        }

        private void ActualizarEnlaces(Vector3 centro)
        {
            if (crucesSacerdote == null || materialEnlace == null)
                return;

            materialEnlace.SetFloat(IdFuerza, Mathf.Lerp(0.35f, 1f, fuerza));
            materialEnlace.SetFloat(IdParpadeo, 1f - fuerza);

            float ancho = Mathf.Lerp(anchoMinimo, anchoMaximo, fuerza);

            for (int i = 0; i < crucesSacerdote.Count; i++)
            {
                MonkCrossBehaviour cruz = crucesSacerdote[i];
                if (cruz == null || cruz.IsDead || !cruz.gameObject.activeInHierarchy)
                    continue;

                if (!enlaces.TryGetValue(cruz, out LineRenderer linea) || linea == null)
                {
                    linea = CrearEnlace();
                    enlaces[cruz] = linea;
                }

                DibujarEnlace(linea, cruz.CentroVisual, centro, ancho, i);
            }

            // Las cruces que mueren cortan su haz con un chispazo.
            porQuitar.Clear();
            foreach (KeyValuePair<MonkCrossBehaviour, LineRenderer> par in enlaces)
            {
                if (par.Key == null || par.Key.IsDead || !par.Key.gameObject.activeInHierarchy)
                    porQuitar.Add(par.Key);
            }

            for (int i = 0; i < porQuitar.Count; i++)
            {
                LineRenderer linea = enlaces[porQuitar[i]];
                if (linea != null)
                {
                    CortarEnlace(linea);
                    Destroy(linea.gameObject);
                }
                enlaces.Remove(porQuitar[i]);
            }
        }

        private LineRenderer CrearEnlace()
        {
            GameObject objeto = new GameObject("Enlace");
            objeto.transform.SetParent(raiz, false);

            LineRenderer linea = objeto.AddComponent<LineRenderer>();
            linea.sharedMaterial = materialEnlace;
            linea.positionCount = PuntosPorEnlace;
            linea.useWorldSpace = true;
            linea.textureMode = LineTextureMode.Stretch;
            linea.alignment = LineAlignment.View;
            linea.numCapVertices = 2;
            linea.startColor = colorEnlace;
            linea.endColor = colorEnlace;
            // Mas fino en la cruz y mas grueso al llegar al sacerdote: la energia se acumula en el.
            linea.widthCurve = new AnimationCurve(new Keyframe(0f, 0.45f), new Keyframe(0.7f, 1f), new Keyframe(1f, 0.8f));
            RecursosSacerdote.ConfigurarRender(linea);
            return linea;
        }

        // Curva del haz: sale de la cruz, se arquea hacia arriba y ondula un poco.
        private void DibujarEnlace(LineRenderer linea, Vector3 desde, Vector3 hasta, float ancho, int indice)
        {
            float distancia = Vector3.Distance(desde, hasta);
            Vector3 medio = (desde + hasta) * 0.5f + Vector3.up * (arco * distancia * 0.5f + 0.1f);
            Vector3 lateral = Vector3.Cross((hasta - desde).normalized, Vector3.up);

            float t = Time.time;
            for (int i = 0; i < PuntosPorEnlace; i++)
            {
                float k = i / (float)(PuntosPorEnlace - 1);
                Vector3 a = Vector3.Lerp(desde, medio, k);
                Vector3 b = Vector3.Lerp(medio, hasta, k);
                Vector3 punto = Vector3.Lerp(a, b, k);

                float onda = Mathf.Sin(k * Mathf.PI) * Mathf.Sin(t * 3.1f + k * 6f + indice * 1.7f) * 0.06f * (1.5f - fuerza);
                puntos[i] = punto + lateral * onda;
            }

            linea.SetPositions(puntos);
            linea.widthMultiplier = ancho;
        }

        private void CortarEnlace(LineRenderer linea)
        {
            if (chispas == null || linea.positionCount == 0)
                return;

            int cantidad = RecursosSacerdote.Cantidad(18);
            for (int i = 0; i < cantidad; i++)
            {
                Vector3 punto = linea.GetPosition(Random.Range(0, linea.positionCount));
                RecursosSacerdote.Emitir(chispas, punto, Random.insideUnitSphere * 1.5f + Vector3.up * 0.5f, Random.Range(0.03f, 0.07f), Random.Range(0.3f, 0.6f), colorEnlace);
            }
        }

        private void OnDestroy()
        {
            if (raiz != null)
                Destroy(raiz.gameObject);
            if (materialEnlace != null)
                Destroy(materialEnlace);
            if (materialEscudo != null)
                Destroy(materialEscudo);
        }
    }
}
