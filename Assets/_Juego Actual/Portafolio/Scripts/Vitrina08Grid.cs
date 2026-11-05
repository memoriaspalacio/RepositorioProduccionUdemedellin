using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UdeM.Portafolio
{
    /// <summary>
    /// Vitrina del sistema de grid: el suelo procedural UdeM/Suelo/SueloGrid con el grid dentro del
    /// shader, un personaje que avanza casilla a casilla al ritmo y enemigos que ocupan casillas.
    ///   - 1 a 4: el suelo de cada nivel (sus materiales, copiados: los originales no cambian).
    ///   - Q / E: estilo del grid. Z / X: como se integra en el suelo. C: patron de las piezas.
    ///   - V: revelado alrededor del personaje. B: pulso con el beat. Espacio: fuego de monja en linea.
    /// El ritmo es un metronomo propio (sin BeatManager) que hace lo mismo que GridBeatVisualizer.
    /// </summary>
    public class Vitrina08Grid : VitrinaPortafolio
    {
        static readonly string[] Estilos = { "Lineas", "Discontinuas", "Cruces", "Esquinas", "Puntos", "Marco", "Resplandor", "Tablero" };
        static readonly string[] Mezclas = { "Brillo", "Juntas", "Grabado", "Pintura", "Tinte", "Oculto" };
        static readonly string[] Patrones = { "Losas", "Ladrillo", "Piedras", "Liso", "Parcelas" };
        static readonly Vector2Int[] Vecinos = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        static readonly int IdEstilo = Shader.PropertyToID("_EstiloGrid");
        static readonly int IdMezcla = Shader.PropertyToID("_MezclaGrid");
        static readonly int IdPatron = Shader.PropertyToID("_Patron");
        static readonly int IdTamano = Shader.PropertyToID("_GridSize");
        static readonly int IdOrigen = Shader.PropertyToID("_GridOrigin");
        static readonly int IdPulso = Shader.PropertyToID("_BeatPulse");
        static readonly int IdRadio = Shader.PropertyToID("_RadioRevelado");
        static readonly int IdCentro = Shader.PropertyToID("_CentroRevelado");

        [Header("Suelo")]
        public Renderer suelo;
        public List<Material> suelosNiveles = new List<Material>();
        public List<string> nombresNiveles = new List<string>();
        [Min(0.5f)] public float tamanoCelda = 2f;
        [Tooltip("Celdas a cada lado del centro (el grid va de -x a +x, de -y a +y).")]
        public Vector2Int mitadCeldas = new Vector2Int(4, 3);
        [Min(0.5f)] public float radioRevelado = 5f;

        [Header("Ritmo")]
        [Min(30f)] public float bpm = 100f;
        [Tooltip("Beats entre un paso y el siguiente.")]
        [Min(1)] public int beatsPorPaso = 1;
        public AnimationCurve curvaPulso = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.35f, 0.15f), new Keyframe(1f, 0.05f));

        [Header("Personajes")]
        public GameObject modeloJugador;
        [Tooltip("Personajes de la escena que ocupan una casilla (plantillas).")]
        public List<GameObject> ocupantes = new List<GameObject>();
        public GameObject prefabCeldaFuego;
        public Material materialCasilla;

        Material material;
        int nivel;
        int estilo;
        int mezcla;
        int patron;
        bool revelado;
        bool pulso = true;

        Transform jugador;
        Vector2Int celdaJugador;
        float faseBeat;
        int beatsDesdePaso;
        bool saltando;
        readonly HashSet<Vector2Int> ocupadas = new HashSet<Vector2Int>();
        readonly List<GameObject> marcas = new List<GameObject>();
        readonly List<GameObject> copias = new List<GameObject>();
        Transform marcaDestino;

        protected override void Preparar()
        {
            for (int i = 0; i < suelosNiveles.Count && i < 4; i++)
            {
                int indice = i;
                string nombre = i < nombresNiveles.Count ? nombresNiveles[i] : "Nivel " + (i + 1);
                Registrar(TeclaNumero(i + 1), "Suelo: " + nombre, () => Nivel(indice), () => nivel == indice);
            }
            Registrar(Key.Q, () => "Estilo anterior (" + Estilos[estilo] + ")", () => Estilo(-1));
            Registrar(Key.E, () => "Estilo siguiente", () => Estilo(1));
            Registrar(Key.Z, () => "Integracion anterior (" + Mezclas[mezcla] + ")", () => Mezcla(-1));
            Registrar(Key.X, () => "Integracion siguiente", () => Mezcla(1));
            Registrar(Key.C, () => "Patron (" + Patrones[patron] + ")", Patron);
            Registrar(Key.V, "Revelado alrededor del personaje", () => revelado = !revelado, () => revelado);
            Registrar(Key.B, "Pulso con el beat", () => pulso = !pulso, () => pulso);
            Registrar(Key.Space, "Fuego de monja sobre el grid", () => StartCoroutine(Fuego()));

            CrearPersonajes();
            Nivel(0);
        }

        protected override void Reiniciar()
        {
            StopAllCoroutines();
            saltando = false;
            revelado = false;
            pulso = true;
            ColocarJugador(Vector2Int.zero);
            Nivel(0);
        }

        protected override void PuntosDeFoco(List<Foco> lista)
        {
            if (jugador != null)
                lista.Add(FocoDe("Personaje", jugador.gameObject));
            foreach (GameObject copia in copias)
            {
                if (copia != null)
                    lista.Add(FocoDe(copia.name, copia));
            }
        }

        #region Suelo

        void Nivel(int indice)
        {
            if (suelo == null || indice >= suelosNiveles.Count || suelosNiveles[indice] == null)
                return;

            nivel = indice;
            if (material != null)
                Destroy(material);
            // Copia en memoria: los cambios de la vitrina no tocan el material del nivel.
            material = new Material(suelosNiveles[indice]);
            suelo.sharedMaterial = material;
            estilo = Mathf.RoundToInt(material.GetFloat(IdEstilo));
            mezcla = Mathf.RoundToInt(material.GetFloat(IdMezcla));
            patron = Mathf.RoundToInt(material.GetFloat(IdPatron));

            // El origen cae en una esquina de celda: el centro de la escena es el centro de una casilla.
            material.SetFloat(IdTamano, tamanoCelda);
            material.SetVector(IdOrigen, new Vector4(-tamanoCelda * 0.5f, -tamanoCelda * 0.5f, 0f, 0f));
        }

        void Estilo(int paso)
        {
            estilo = (estilo + paso + Estilos.Length) % Estilos.Length;
            if (material != null)
                material.SetFloat(IdEstilo, estilo);
        }

        void Mezcla(int paso)
        {
            mezcla = (mezcla + paso + Mezclas.Length) % Mezclas.Length;
            if (material != null)
                material.SetFloat(IdMezcla, mezcla);
        }

        void Patron()
        {
            patron = (patron + 1) % Patrones.Length;
            if (material != null)
                material.SetFloat(IdPatron, patron);
        }

        #endregion

        #region Personajes

        void CrearPersonajes()
        {
            foreach (GameObject plantilla in ocupantes)
            {
                if (plantilla == null)
                    continue;

                Transform t = plantilla.transform;
                GameObject copia = UtilVitrina.CopiaVisual(plantilla, t.parent, t.position, t.rotation);
                copia.transform.localScale = t.localScale;
                plantilla.SetActive(false);
                copias.Add(copia);

                Vector2Int celda = CeldaDe(t.position);
                ocupadas.Add(celda);
                marcas.Add(CrearMarca(celda, new Color(1f, 0.25f, 0.2f, 0.45f)).gameObject);
            }

            if (modeloJugador != null)
            {
                Transform t = modeloJugador.transform;
                GameObject copia = UtilVitrina.CopiaVisual(modeloJugador, t.parent, t.position, t.rotation);
                copia.transform.localScale = t.localScale;
                modeloJugador.SetActive(false);
                jugador = copia.transform;
                ColocarJugador(CeldaDe(t.position));
            }

            marcaDestino = CrearMarca(celdaJugador, new Color(0.4f, 0.85f, 1f, 0.45f));
        }

        Transform CrearMarca(Vector2Int celda, Color color)
        {
            GameObject marca = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(marca.GetComponent<Collider>());
            marca.name = "Casilla " + celda;
            marca.transform.SetParent(transform, false);
            marca.transform.SetPositionAndRotation(Centro(celda) + Vector3.up * 0.02f, Quaternion.Euler(90f, 0f, 0f));
            marca.transform.localScale = Vector3.one * (tamanoCelda * 0.92f);

            Renderer render = marca.GetComponent<Renderer>();
            if (materialCasilla != null)
            {
                render.sharedMaterial = materialCasilla;
                MaterialPropertyBlock bloque = new MaterialPropertyBlock();
                bloque.SetColor("_BaseColor", color);
                render.SetPropertyBlock(bloque);
            }
            return marca.transform;
        }

        void ColocarJugador(Vector2Int celda)
        {
            celdaJugador = celda;
            if (jugador != null)
                jugador.position = Centro(celda);
        }

        Vector3 Centro(Vector2Int celda)
        {
            return new Vector3(celda.x * tamanoCelda, 0f, celda.y * tamanoCelda);
        }

        Vector2Int CeldaDe(Vector3 posicion)
        {
            return new Vector2Int(Mathf.RoundToInt(posicion.x / tamanoCelda), Mathf.RoundToInt(posicion.z / tamanoCelda));
        }

        bool Libre(Vector2Int celda)
        {
            return Mathf.Abs(celda.x) <= mitadCeldas.x && Mathf.Abs(celda.y) <= mitadCeldas.y && !ocupadas.Contains(celda);
        }

        // Un paso por casilla, como el movimiento del juego: elige una vecina libre y salta hasta ella.
        void Paso()
        {
            if (jugador == null || saltando)
                return;

            List<Vector2Int> opciones = new List<Vector2Int>();
            foreach (Vector2Int v in Vecinos)
            {
                if (Libre(celdaJugador + v))
                    opciones.Add(celdaJugador + v);
            }
            if (opciones.Count == 0)
                return;

            Vector2Int destino = opciones[Random.Range(0, opciones.Count)];
            StartCoroutine(Saltar(destino, 60f / bpm * 0.45f));
        }

        IEnumerator Saltar(Vector2Int destino, float duracion)
        {
            saltando = true;
            Vector3 desde = Centro(celdaJugador);
            Vector3 hasta = Centro(destino);
            if (marcaDestino != null)
                marcaDestino.position = hasta + Vector3.up * 0.02f;

            Vector3 mirar = hasta - desde;
            Quaternion giroInicio = jugador.rotation;
            Quaternion giroFin = Quaternion.LookRotation(mirar.normalized, Vector3.up);

            for (float t = 0f; t < duracion; t += Time.deltaTime)
            {
                float k = t / duracion;
                jugador.position = Vector3.Lerp(desde, hasta, k * k * (3f - 2f * k)) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.45f);
                jugador.rotation = Quaternion.Slerp(giroInicio, giroFin, Mathf.Clamp01(k * 3f));
                yield return null;
            }

            ColocarJugador(destino);
            jugador.rotation = giroFin;
            saltando = false;
        }

        IEnumerator Fuego()
        {
            if (prefabCeldaFuego == null || copias.Count == 0)
                yield break;

            // Desde la primera ocupante, tres casillas en linea hacia el personaje.
            Vector2Int origen = CeldaDe(copias[0].transform.position);
            Vector2Int direccion = celdaJugador.x != origen.x
                ? new Vector2Int((int)Mathf.Sign(celdaJugador.x - origen.x), 0)
                : new Vector2Int(0, celdaJugador.y >= origen.y ? 1 : -1);

            List<GameObject> celdas = new List<GameObject>();
            for (int i = 1; i <= 3; i++)
            {
                GameObject celda = Instantiate(prefabCeldaFuego, Centro(origen + direccion * i), prefabCeldaFuego.transform.rotation);
                celdas.Add(celda);
                yield return new WaitForSeconds(0.12f);
            }

            yield return new WaitForSeconds(1.6f);
            foreach (GameObject celda in celdas)
                CeldaFuego.Apagar(celda);
        }

        #endregion

        protected override void Update()
        {
            base.Update();
            if (material == null)
                return;

            // Metronomo: un pulso por beat con la curva de GridBeatVisualizer y un paso cada tantos beats.
            float anterior = faseBeat;
            faseBeat += Time.deltaTime * bpm / 60f;
            if (Mathf.Floor(faseBeat) > Mathf.Floor(anterior) && ++beatsDesdePaso >= beatsPorPaso)
            {
                beatsDesdePaso = 0;
                Paso();
            }

            material.SetFloat(IdPulso, pulso ? curvaPulso.Evaluate(faseBeat % 1f) : 1f);
            material.SetFloat(IdRadio, revelado ? radioRevelado : 0f);
            if (jugador != null)
                material.SetVector(IdCentro, jugador.position);
        }

        void OnDestroy()
        {
            if (material != null)
                Destroy(material);
        }
    }
}
