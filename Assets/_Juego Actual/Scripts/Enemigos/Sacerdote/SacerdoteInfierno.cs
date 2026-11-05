using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UdeM.Characters
{
    /// <summary>
    /// Descenso al infierno y salida del sacerdote. Al hundirse se abre un circulo de fuego bajo
    /// sus pies, el cuerpo se calcina desde el suelo y lo arrastra hacia abajo; al salir, el
    /// circulo (con las cruces de la invocacion si lo llama una cruz) lo devuelve envuelto en
    /// brasas. MonkBehaviour lo agrega solo; agregalo a mano unicamente para ajustar valores.
    /// </summary>
    [DisallowMultipleComponent]
    public class SacerdoteInfierno : MonoBehaviour
    {
        [Tooltip("Cuantos metros baja el modelo al hundirse.")]
        [Min(0.5f)]
        [SerializeField] private float profundidad = 2.6f;

        [Tooltip("Radio del circulo de fuego en el suelo.")]
        [Min(0.3f)]
        [SerializeField] private float radioCirculo = 1.1f;

        [Tooltip("Hasta que altura del cuerpo suben las brasas.")]
        [Min(0.1f)]
        [SerializeField] private float alturaBrasas = 1.4f;

        [Tooltip("Brasas por segundo que suelta un circulo abierto del todo.")]
        [Min(0f)]
        [SerializeField] private float brasasPorSegundo = 45f;

        [SerializeField] private Color colorBrasa = new Color(1f, 0.45f, 0.08f, 1f);
        [SerializeField] private Color colorHumo = new Color(0.08f, 0.02f, 0.02f, 0.55f);
        [SerializeField] private Color colorLuz = new Color(1f, 0.3f, 0.05f, 1f);

        static readonly int IdIntensidad = Shader.PropertyToID("_Intensidad");
        static readonly int IdAlturaSuelo = Shader.PropertyToID("_AlturaSuelo");
        static readonly int IdAlturaBrasa = Shader.PropertyToID("_AlturaBrasa");
        static readonly int IdApertura = Shader.PropertyToID("_Apertura");
        static readonly int IdRunas = Shader.PropertyToID("_Runas");

        // Un circulo abierto en el suelo con su luz; se cierra solo y se borra al terminar.
        private class Circulo
        {
            public MeshRenderer render;
            public Material material;
            public Light luz;
            public Vector3 centro;
            public float apertura;
            public float objetivo;
            public float velocidad;
            public float runas;
            public float acumuladoBrasas;
            public float acumuladoHumo;
        }

        private Transform modelo;
        private Vector3 posicionLocalModelo;
        private Renderer[] renderers = new Renderer[0];
        private Material[][] materialesOriginales;
        private Material capa;
        private bool capaAplicada;
        private float intensidad;

        private Transform raizEfectos;
        private ParticleSystem brasas;
        private ParticleSystem humo;
        private readonly List<Circulo> circulos = new List<Circulo>();
        private Circulo circuloInvocacion;

        /// <summary>Indica si el modelo esta escondido bajo el suelo.</summary>
        public bool Oculto { get; private set; }

        /// <summary>Prepara el efecto para el modelo visual del sacerdote.</summary>
        public void Configurar(Transform modeloVisual)
        {
            modelo = modeloVisual;
            if (modelo == null)
                return;

            posicionLocalModelo = modelo.localPosition;
            renderers = RecursosSacerdote.RenderersVisibles(modelo);

            capa = RecursosSacerdote.NuevoMaterial("SacerdoteInfierno");
            if (capa != null)
                capa.SetFloat(IdAlturaBrasa, alturaBrasas);

            // Los efectos viven fuera del sacerdote: asi no se mueven con sus saltos ni se
            // capturan en su efecto de muerte.
            raizEfectos = new GameObject("Infierno " + name).transform;
            brasas = RecursosSacerdote.CrearSistema(raizEfectos, "Brasas", RecursosSacerdote.ParticulaChispa, RecursosSacerdote.Cantidad(400));
            RecursosSacerdote.Ruido(brasas, 0.6f, 1.2f);
            humo = RecursosSacerdote.CrearSistema(raizEfectos, "Humo", RecursosSacerdote.ParticulaHumo, RecursosSacerdote.Cantidad(80));
        }

        // Altura del suelo bajo el sacerdote: su raiz esta sobre el NavMesh.
        private float Suelo => transform.position.y;

        /// <summary>Abre el circulo de fuego, calcina el cuerpo desde abajo y lo hunde.</summary>
        public IEnumerator Hundirse(float duracion)
        {
            if (modelo == null)
                yield break;

            duracion = Mathf.Max(0.1f, duracion);
            AplicarCapa();

            Circulo circulo = AbrirCirculo(PuntoSuelo(transform.position), 0f, 4f);
            Vector3 inicio = modelo.position;

            float tiempo = 0f;
            while (tiempo < duracion)
            {
                tiempo += Time.deltaTime;
                float k = Mathf.Clamp01(tiempo / duracion);

                intensidad = Mathf.Clamp01(k / 0.2f);

                // Primero arde de pie un instante y luego el infierno lo jala cada vez mas rapido.
                float bajada = Mathf.Clamp01((k - 0.15f) / 0.85f);
                bajada = bajada * bajada;

                // Sacudida que se calma a medida que desaparece.
                Vector3 temblor = new Vector3(
                    Mathf.Sin(tiempo * 47f),
                    0f,
                    Mathf.Cos(tiempo * 39f)
                ) * 0.03f * (1f - bajada);

                modelo.position = inicio + Vector3.down * (profundidad * bajada) + temblor;
                yield return null;
            }

            MostrarModelo(false);
            Oculto = true;
            CerrarCirculo(circulo, 2.5f);
        }

        /// <summary>
        /// Abre en el suelo el circulo de una invocacion (con cruces girando) en el punto
        /// donde va a salir el sacerdote. Emerger lo reutiliza y lo cierra.
        /// </summary>
        public void PrepararInvocacion(Vector3 posicion)
        {
            if (circuloInvocacion != null)
                CerrarCirculo(circuloInvocacion, 3f);

            circuloInvocacion = AbrirCirculo(PuntoSuelo(posicion), 1f, 2.2f);
        }

        /// <summary>Saca al sacerdote del suelo envuelto en fuego; el cuerpo se apaga al final.</summary>
        public IEnumerator Emerger(float duracion)
        {
            if (modelo == null)
                yield break;

            duracion = Mathf.Max(0.1f, duracion);

            modelo.localPosition = posicionLocalModelo;
            MostrarModelo(true);
            Oculto = false;
            AplicarCapa();
            intensidad = 1f;

            Vector3 centro = PuntoSuelo(transform.position);
            Circulo circulo = circuloInvocacion;
            circuloInvocacion = null;

            if (circulo == null ||
                (circulo.centro - centro).sqrMagnitude > 2.25f)
            {
                if (circulo != null)
                    CerrarCirculo(circulo, 3f);
                circulo = AbrirCirculo(centro, 0f, 6f);
            }

            // Estallido de brasas al romper el suelo.
            for (int i = 0; i < RecursosSacerdote.Cantidad(40); i++)
                EmitirBrasa(circulo, 1.6f);

            float tiempo = 0f;
            while (tiempo < duracion)
            {
                tiempo += Time.deltaTime;
                float k = Mathf.Clamp01(tiempo / duracion);
                intensidad = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, k));
                yield return null;
            }

            intensidad = 0f;
            QuitarCapa();
            CerrarCirculo(circulo, 1.6f);
        }

        /// <summary>Deja el modelo visible, en su sitio y sin la capa de fuego.</summary>
        public void Restaurar()
        {
            intensidad = 0f;
            QuitarCapa();

            if (modelo != null)
                modelo.localPosition = posicionLocalModelo;

            MostrarModelo(true);
            Oculto = false;

            if (circuloInvocacion != null)
            {
                CerrarCirculo(circuloInvocacion, 3f);
                circuloInvocacion = null;
            }
        }

        private void AplicarCapa()
        {
            if (capaAplicada || capa == null)
                return;

            materialesOriginales = RecursosSacerdote.AgregarCapa(renderers, capa);
            capaAplicada = true;
        }

        private void QuitarCapa()
        {
            if (!capaAplicada)
                return;

            RecursosSacerdote.QuitarCapa(renderers, materialesOriginales);
            materialesOriginales = null;
            capaAplicada = false;
        }

        private void MostrarModelo(bool visible)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    renderers[i].enabled = visible;
            }
        }

        private Vector3 PuntoSuelo(Vector3 posicion)
        {
            return new Vector3(posicion.x, Suelo + 0.03f, posicion.z);
        }

        private Circulo AbrirCirculo(Vector3 centro, float runas, float velocidad)
        {
            Material material = RecursosSacerdote.NuevoMaterial("CirculoInfernal");
            if (material == null)
                return null;

            material.SetFloat(IdRunas, runas);
            material.SetFloat(IdApertura, 0f);

            MeshRenderer render = RecursosSacerdote.CrearMalla("Circulo infernal", RecursosSacerdote.QuadSuelo, material, raizEfectos);
            render.transform.position = centro;
            // En el shader el anillo de fuego queda al 62% del borde del quad.
            render.transform.localScale = Vector3.one * (radioCirculo * 2f / 0.62f);

            Light luz = RecursosSacerdote.CrearLuz(render.transform, colorLuz, radioCirculo * 3.5f);
            luz.transform.position = centro + Vector3.up * 0.35f;

            Circulo circulo = new Circulo
            {
                render = render,
                material = material,
                luz = luz,
                centro = centro,
                apertura = 0f,
                objetivo = 1f,
                velocidad = velocidad,
                runas = runas
            };
            circulos.Add(circulo);
            return circulo;
        }

        private void CerrarCirculo(Circulo circulo, float velocidad)
        {
            if (circulo == null)
                return;

            circulo.objetivo = 0f;
            circulo.velocidad = velocidad;
        }

        private void LateUpdate()
        {
            if (capaAplicada && capa != null)
            {
                capa.SetFloat(IdAlturaSuelo, Suelo + 0.03f);
                capa.SetFloat(IdIntensidad, intensidad);
            }

            float dt = Time.deltaTime;
            for (int i = circulos.Count - 1; i >= 0; i--)
            {
                Circulo circulo = circulos[i];
                if (circulo.render == null)
                {
                    circulos.RemoveAt(i);
                    continue;
                }

                circulo.apertura = Mathf.MoveTowards(circulo.apertura, circulo.objetivo, circulo.velocidad * dt);
                float abierto = Mathf.SmoothStep(0f, 1f, circulo.apertura);

                circulo.material.SetFloat(IdApertura, abierto);
                circulo.material.SetFloat(IdRunas, circulo.runas * abierto);
                circulo.luz.intensity = 3.2f * abierto * (0.85f + 0.15f * Mathf.PerlinNoise(Time.time * 6f, i));

                EmitirAmbiente(circulo, abierto, dt);

                if (circulo.objetivo <= 0f && circulo.apertura <= 0.001f)
                {
                    Destroy(circulo.render.gameObject);
                    Destroy(circulo.material);
                    circulos.RemoveAt(i);
                }
            }
        }

        // Brasas que suben y humo oscuro que sale del circulo mientras esta abierto.
        private void EmitirAmbiente(Circulo circulo, float abierto, float dt)
        {
            if (abierto <= 0.01f)
                return;

            int cuantas = Cuantas(ref circulo.acumuladoBrasas, brasasPorSegundo * abierto * GestorGraficos.MultiplicadorEfectos, dt);
            for (int i = 0; i < cuantas; i++)
                EmitirBrasa(circulo, 1f);

            int bocanadas = Cuantas(ref circulo.acumuladoHumo, 7f * abierto * GestorGraficos.MultiplicadorEfectos, dt);
            for (int i = 0; i < bocanadas; i++)
            {
                Vector2 disco = Random.insideUnitCircle * radioCirculo * 0.7f * abierto;
                Vector3 posicion = circulo.centro + new Vector3(disco.x, 0.1f, disco.y);
                Vector3 velocidad = new Vector3(Random.Range(-0.15f, 0.15f), Random.Range(0.35f, 0.75f), Random.Range(-0.15f, 0.15f));
                RecursosSacerdote.Emitir(humo, posicion, velocidad, Random.Range(0.6f, 1.15f), Random.Range(1.2f, 1.9f), colorHumo, Random.Range(0f, 360f), Random.Range(-30f, 30f));
            }
        }

        private void EmitirBrasa(Circulo circulo, float fuerza)
        {
            if (circulo == null)
                return;

            Vector2 disco = Random.insideUnitCircle * radioCirculo * 0.75f;
            Vector3 posicion = circulo.centro + new Vector3(disco.x, 0.05f, disco.y);
            Vector3 velocidad = new Vector3(disco.x * 0.4f, Random.Range(1.2f, 2.8f) * fuerza, disco.y * 0.4f);

            Color color = Color.Lerp(colorBrasa, new Color(1f, 0.85f, 0.3f, 1f), Random.value * 0.5f);
            RecursosSacerdote.Emitir(brasas, posicion, velocidad, Random.Range(0.04f, 0.11f), Random.Range(0.7f, 1.4f), color);
        }

        private static int Cuantas(ref float acumulado, float porSegundo, float dt)
        {
            acumulado += Mathf.Max(0f, porSegundo) * dt;
            int cuantas = Mathf.FloorToInt(acumulado);
            acumulado -= cuantas;
            return cuantas;
        }

        private void OnDestroy()
        {
            QuitarCapa();

            if (raizEfectos != null)
                Destroy(raizEfectos.gameObject);

            for (int i = 0; i < circulos.Count; i++)
            {
                if (circulos[i].material != null)
                    Destroy(circulos[i].material);
            }

            if (capa != null)
                Destroy(capa);
        }
    }
}
