using System;
using System.Collections;
using System.Collections.Generic;
using UdeM.Characters;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UdeM.Portafolio
{
    /// <summary>
    /// Vitrina del kit del sacerdote, sin su logica de combate: las mismas piezas visuales que usa
    /// MonkBehaviour (SacerdoteInfierno, EnlaceDivino, MonkCrossVisual) llamadas a mano.
    ///   - Cruces que rodean al sacerdote y le mandan sus haces; el escudo se debilita al romperlas.
    ///   - Golpe al escudo, ataque de una cruz, descenso al infierno e invocacion por la cruz suelta,
    ///     las monjas lanzando su fuego y, sin cruces, el remate con la muerte de ceniza blanca.
    /// MonkCrossBehaviour se usa solo como marca para el enlace: no tiene Awake ni Update, asi que
    /// sin Initialize no hace nada de juego.
    /// </summary>
    public class Vitrina02Sacerdote : VitrinaPortafolio
    {
        [Header("Modelos")]
        [Tooltip("Sacerdote de la escena (plantilla). Se apaga al empezar y se juega con una copia visual.")]
        public GameObject modeloSacerdote;
        [Tooltip("Prefab de la cruz (Cross). Se le quita la logica de juego.")]
        public GameObject prefabCruz;
        [Tooltip("Prefab de la celda de fuego de las monjas (SisterAttackCell).")]
        public GameObject prefabCeldaFuego;

        [Header("Cruces")]
        [Range(1, 6)] public int cantidadCruces = 3;
        [Min(0.5f)] public float radioCruces = 1.5f;
        [Tooltip("Grados por segundo que gira el anillo de cruces.")]
        public float giroAnillo = 18f;
        [Tooltip("Donde espera la cruz suelta que invoca al sacerdote.")]
        public Vector3 puntoInvocacion = new Vector3(3.4f, 0f, 1.4f);

        [Header("Fuego de las monjas")]
        [Tooltip("Monjas de la escena (plantillas). Atacan y su fuego se enciende al terminar el conjuro.")]
        public List<GameObject> modelosMonja = new List<GameObject>();
        [Tooltip("Segundos desde que la monja empieza a atacar hasta que se enciende el fuego.")]
        [Min(0f)] public float retrasoConjuro = 0.55f;
        public Vector3 centroFuego = new Vector3(-3.6f, 0f, 0.8f);
        [Min(0.2f)] public float tamanoCelda = 1f;
        [Min(0.1f)] public float duracionFuego = 1.8f;

        [Header("Tiempos")]
        [Min(0.1f)] public float duracionHundirse = 1f;
        [Min(0f)] public float esperaInvocacion = 0.35f;
        [Min(0.1f)] public float duracionEmerger = 1.2f;
        [Min(0.1f)] public float cargaAtaque = 0.7f;
        [Min(0.1f)] public float vueloAtaque = 0.45f;
        [Min(0f)] public float reconstruirTras = 3.5f;

        // Celdas del fuego en el orden en que se encienden: el centro, la cruz y las esquinas.
        static readonly Vector2Int[] CeldasFuego =
        {
            new Vector2Int(0, 0),
            new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1),
            new Vector2Int(1, 1), new Vector2Int(-1, 1), new Vector2Int(1, -1), new Vector2Int(-1, -1)
        };

        Vector3 posicionInicial;
        Quaternion rotacionInicial;
        Vector3 escalaModelo;

        // Lo que se hunde, se teletransporta y muere; el modelo visual va dentro.
        Transform raiz;
        Transform modelo;
        Animator animador;
        SacerdoteInfierno infierno;
        EnlaceDivino enlace;

        // Centro de las cruces poseidas: sigue al sacerdote con retraso, asi vuelan tras el.
        Transform anillo;
        Vector3 velocidadAnillo;

        // Listas nuevas en cada construccion: el EnlaceDivino viejo puede seguir leyendo la suya
        // hasta que se destruye al final del frame.
        List<MonkCrossBehaviour> cruces = new List<MonkCrossBehaviour>();
        List<MonkCrossVisual> visuales = new List<MonkCrossVisual>();
        List<Transform> puestos = new List<Transform>();
        readonly HashSet<int> volando = new HashSet<int>();
        readonly List<GameObject> extras = new List<GameObject>();
        readonly List<Animator> monjas = new List<Animator>();
        MonkCrossBehaviour cruzSuelta;

        bool ocupado;
        bool expuesto;

        protected override void Preparar()
        {
            if (modeloSacerdote != null)
            {
                posicionInicial = modeloSacerdote.transform.position;
                rotacionInicial = modeloSacerdote.transform.rotation;
                escalaModelo = modeloSacerdote.transform.lossyScale;
                modeloSacerdote.SetActive(false);
            }

            for (int i = 0; i < modelosMonja.Count; i++)
            {
                if (modelosMonja[i] != null)
                    modelosMonja[i].SetActive(false);
            }

            Registrar(TeclaNumero(1), "Golpe al escudo", GolpearEscudo);
            Registrar(TeclaNumero(2), "Ataque de una cruz", () => StartCoroutine(AtaqueCruz()));
            Registrar(TeclaNumero(3), "Romper una cruz", RomperCruz);
            Registrar(TeclaNumero(4), "Infierno e invocacion", () => StartCoroutine(Invocacion()));
            Registrar(TeclaNumero(5), "Fuego de las monjas", () => StartCoroutine(FuegoMonjas()));
            Registrar(TeclaNumero(6), "Rematar (sin cruces)", () => StartCoroutine(Rematar()));
            Registrar(Key.Space, "Secuencia completa", () => { StopAllCoroutines(); StartCoroutine(Secuencia()); });

            Construir();
        }

        protected override void Reiniciar()
        {
            StopAllCoroutines();
            Reconstruir();
        }

        public override List<Key> AccionesMiniatura => new List<Key> { TeclaNumero(3), TeclaNumero(1) };
        public override float EsperaMiniatura => 0.25f;

        protected override void PuntosDeFoco(List<Foco> lista)
        {
            if (raiz != null)
            {
                Foco sacerdote = FocoDe("Sacerdote y cruces", raiz.gameObject);
                // Un poco mas alto que el cuerpo para que entren las cruces de alrededor.
                sacerdote.alto = Mathf.Max(sacerdote.alto, radioCruces * 1.4f);
                lista.Add(sacerdote);
            }
            lista.Add(new Foco { nombre = "Monjas y fuego", apoyo = centroFuego, alto = tamanoCelda * 2.6f });
            if (cruzSuelta != null)
                lista.Add(FocoDe("Cruz que invoca", cruzSuelta.gameObject, 1.2f));
        }

        protected override void Update()
        {
            base.Update();

            if (anillo != null && raiz != null)
            {
                anillo.position = Vector3.SmoothDamp(anillo.position, raiz.position, ref velocidadAnillo, 0.3f);
                anillo.Rotate(Vector3.up, giroAnillo * Time.deltaTime, Space.World);
            }
        }

        #region Construccion

        void Construir()
        {
            if (modeloSacerdote == null || prefabCruz == null)
            {
                Debug.LogWarning("Vitrina02Sacerdote: faltan el modelo del sacerdote o el prefab de la cruz.", this);
                return;
            }

            raiz = new GameObject("Sacerdote (vitrina)").transform;
            raiz.SetPositionAndRotation(posicionInicial, rotacionInicial);
            modelo = UtilVitrina.CopiaVisual(modeloSacerdote, raiz, posicionInicial, rotacionInicial).transform;
            modelo.localScale = escalaModelo;
            animador = modelo.GetComponentInChildren<Animator>();

            anillo = new GameObject("Anillo de cruces").transform;
            anillo.position = raiz.position;
            velocidadAnillo = Vector3.zero;

            cruces = new List<MonkCrossBehaviour>();
            visuales = new List<MonkCrossVisual>();
            puestos = new List<Transform>();

            for (int i = 0; i < cantidadCruces; i++)
            {
                Transform puesto = new GameObject("Puesto " + (i + 1)).transform;
                puesto.SetParent(anillo, false);
                puesto.localPosition = Quaternion.Euler(0f, i * 360f / cantidadCruces, 0f) * Vector3.forward * radioCruces;

                MonkCrossBehaviour cruz = CrearCruz(puesto.position, i);
                cruz.transform.SetParent(puesto, true);
                cruces.Add(cruz);
                visuales.Add(cruz.GetComponent<MonkCrossVisual>());
                puestos.Add(puesto);
            }

            cruzSuelta = CrearCruz(puntoInvocacion, cantidadCruces);

            // Mismo orden que MonkBehaviour.SetupEffects: el modelo ya esta en su sitio.
            infierno = raiz.gameObject.AddComponent<SacerdoteInfierno>();
            infierno.Configurar(modelo);
            enlace = raiz.gameObject.AddComponent<EnlaceDivino>();
            enlace.Configurar(modelo, cruces);
            raiz.gameObject.AddComponent<MinecraftHitFeedback>();

            monjas.Clear();
            for (int i = 0; i < modelosMonja.Count; i++)
            {
                GameObject plantilla = modelosMonja[i];
                if (plantilla == null)
                    continue;

                Transform t = plantilla.transform;
                GameObject monja = UtilVitrina.CopiaVisual(plantilla, t.parent, t.position, t.rotation);
                monja.transform.localScale = t.localScale;
                extras.Add(monja);
                monjas.Add(monja.GetComponentInChildren<Animator>());
            }

            ocupado = false;
            expuesto = false;
            volando.Clear();
        }

        MonkCrossBehaviour CrearCruz(Vector3 posicion, int indice)
        {
            Vector3 mirar = posicion - posicionInicial;
            mirar.y = 0f;
            Quaternion giro = mirar.sqrMagnitude > 0.001f ? Quaternion.LookRotation(mirar.normalized) : Quaternion.identity;

            GameObject objeto = UtilVitrina.CopiaVisual(prefabCruz, null, posicion, giro * prefabCruz.transform.rotation);
            MonkCrossBehaviour cruz = objeto.AddComponent<MonkCrossBehaviour>();
            MonkCrossVisual visual = objeto.AddComponent<MonkCrossVisual>();
            visual.Inicializar(indice);
            visual.AlAparecer();
            return cruz;
        }

        void Reconstruir()
        {
            if (raiz != null)
                Destroy(raiz.gameObject);
            if (anillo != null)
                Destroy(anillo.gameObject);
            for (int i = 0; i < cruces.Count; i++)
            {
                if (cruces[i] != null)
                    Destroy(cruces[i].gameObject);
            }
            if (cruzSuelta != null)
                Destroy(cruzSuelta.gameObject);
            for (int i = 0; i < extras.Count; i++)
            {
                if (extras[i] != null)
                    Destroy(extras[i]);
            }
            extras.Clear();

            raiz = null;
            Construir();
        }

        int Vivas()
        {
            int vivas = 0;
            for (int i = 0; i < cruces.Count; i++)
            {
                if (cruces[i] != null && cruces[i].gameObject.activeSelf)
                    vivas++;
            }
            return vivas;
        }

        #endregion

        #region Acciones

        void GolpearEscudo()
        {
            if (raiz == null || expuesto || infierno.Oculto)
                return;

            enlace.Bloquear(PuntoAtacante(enlace.Centro));
        }

        void RomperCruz()
        {
            if (raiz == null || expuesto)
                return;

            for (int i = cruces.Count - 1; i >= 0; i--)
            {
                if (cruces[i] == null || !cruces[i].gameObject.activeSelf || volando.Contains(i))
                    continue;

                // El enlace ve la cruz apagada y corta su haz con un chispazo.
                visuales[i].AlMorir();
                cruces[i].gameObject.SetActive(false);
                break;
            }

            int vivas = Vivas();
            enlace.ActualizarFuerza(vivas, cruces.Count);
            if (vivas == 0)
                StartCoroutine(Exponer());
        }

        IEnumerator Exponer()
        {
            yield return new WaitForSeconds(0.35f);
            if (raiz == null || expuesto)
                yield break;

            // Como MonkBehaviour.ExposeMonk: se rompe la proteccion y el cuerpo queda limpio.
            expuesto = true;
            enlace.Romper();
            infierno.Restaurar();
        }

        IEnumerator AtaqueCruz()
        {
            if (raiz == null || ocupado)
                yield break;

            int indice = -1;
            for (int i = 0; i < cruces.Count; i++)
            {
                if (cruces[i] != null && cruces[i].gameObject.activeSelf && !volando.Contains(i))
                {
                    indice = i;
                    break;
                }
            }
            if (indice < 0)
                yield break;

            volando.Add(indice);
            Transform cruz = cruces[indice].transform;
            MonkCrossVisual visual = visuales[indice];
            Transform puesto = puestos[indice];

            // El sacerdote reza: la cruz sube, gira mas rapido y se enciende.
            UtilVitrina.Bool(animador, "Attack", true);
            visual.PrepararAtaque(cargaAtaque);
            yield return new WaitForSeconds(cargaAtaque);
            UtilVitrina.Bool(animador, "Attack", false);

            if (cruz == null)
                yield break;

            cruz.SetParent(null, true);
            Vector3 hacia = DestinoAtaque(cruz.position);
            visual.AlDespegar();
            yield return Volar(cruz, cruz.position, () => hacia, vueloAtaque, 1.1f);
            visual.AlAterrizar();

            yield return new WaitForSeconds(0.6f);

            if (cruz == null || puesto == null)
                yield break;

            visual.AlVolver();
            yield return Volar(cruz, cruz.position, () => puesto.position, 0.55f, 0.7f);
            cruz.SetParent(puesto, true);
            cruz.localPosition = Vector3.zero;
            visual.AlTerminarVuelta();
            volando.Remove(indice);
        }

        // Hacia la camara y un poco hacia el lado de la cruz, para que el vuelo se vea entero.
        Vector3 DestinoAtaque(Vector3 desde)
        {
            Camera camara = Camera.main;
            Vector3 frente = camara != null ? camara.transform.position - raiz.position : Vector3.back;
            frente.y = 0f;
            frente = frente.sqrMagnitude > 0.001f ? frente.normalized : Vector3.back;

            Vector3 lado = desde - raiz.position;
            lado.y = 0f;
            Vector3 destino = raiz.position + frente * 2.6f + Vector3.ProjectOnPlane(lado, frente) * 0.6f;
            destino.y = raiz.position.y;
            return destino;
        }

        IEnumerator Volar(Transform objeto, Vector3 desde, Func<Vector3> hacia, float duracion, float arco)
        {
            float tiempo = 0f;
            while (tiempo < duracion)
            {
                if (objeto == null)
                    yield break;

                tiempo += Time.deltaTime;
                float k = Mathf.Clamp01(tiempo / duracion);
                float suave = k * k * (3f - 2f * k);
                objeto.position = Vector3.Lerp(desde, hacia(), suave) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * arco);
                yield return null;
            }
            if (objeto != null)
                objeto.position = hacia();
        }

        IEnumerator Invocacion()
        {
            if (raiz == null || ocupado || expuesto || cruzSuelta == null)
                yield break;
            ocupado = true;

            MonkCrossVisual invocadora = cruzSuelta.GetComponent<MonkCrossVisual>();
            Vector3 anterior = raiz.position;
            Vector3 haciaCentro = anterior - cruzSuelta.transform.position;
            haciaCentro.y = 0f;
            Vector3 destino = cruzSuelta.transform.position + haciaCentro.normalized * 1.1f;
            destino.y = anterior.y;

            // Mismo orden que MonkBehaviour al reubicarse: la cruz invoca mientras el se hunde.
            enlace.MostrarEscudo(false);
            invocadora.Invocar();
            infierno.PrepararInvocacion(destino);
            yield return infierno.Hundirse(duracionHundirse);

            if (esperaInvocacion > 0f)
                yield return new WaitForSeconds(esperaInvocacion);

            raiz.position = destino;
            for (int i = 0; i < visuales.Count; i++)
            {
                if (visuales[i] != null && !volando.Contains(i))
                    visuales[i].AlVolver();
            }

            UtilVitrina.Trigger(animador, "onHurt");
            invocadora.TerminarInvocacion();
            enlace.MostrarEscudo(true);
            yield return infierno.Emerger(duracionEmerger);

            for (int i = 0; i < visuales.Count; i++)
            {
                if (visuales[i] != null && !volando.Contains(i))
                    visuales[i].AlTerminarVuelta();
            }

            // La cruz suelta reaparece donde estaba el sacerdote, lista para la siguiente invocacion.
            if (cruzSuelta != null)
            {
                cruzSuelta.transform.position = anterior;
                invocadora.AlAparecer();
            }

            ocupado = false;
        }

        IEnumerator FuegoMonjas()
        {
            if (prefabCeldaFuego == null)
                yield break;

            // Las monjas conjuran (su animacion de ataque) y el fuego sale al terminar.
            for (int i = 0; i < monjas.Count; i++)
                UtilVitrina.Trigger(monjas[i], "onAttack");
            if (monjas.Count > 0 && retrasoConjuro > 0f)
                yield return new WaitForSeconds(retrasoConjuro);

            List<GameObject> celdas = new List<GameObject>();
            for (int i = 0; i < CeldasFuego.Length; i++)
            {
                Vector3 posicion = centroFuego + new Vector3(CeldasFuego[i].x, 0f, CeldasFuego[i].y) * tamanoCelda;
                GameObject celda = Instantiate(prefabCeldaFuego, posicion, prefabCeldaFuego.transform.rotation);
                celdas.Add(celda);
                extras.Add(celda);

                // Se enciende en ondas desde el centro.
                if (i == 0 || i == 4)
                    yield return new WaitForSeconds(0.12f);
            }

            yield return new WaitForSeconds(duracionFuego);

            for (int i = 0; i < celdas.Count; i++)
                CeldaFuego.Apagar(celdas[i]);
            for (int i = 0; i < monjas.Count; i++)
                UtilVitrina.Trigger(monjas[i], "onIdle");
        }

        IEnumerator Rematar()
        {
            if (raiz == null || !expuesto || ocupado)
                yield break;
            ocupado = true;

            Vector3 origen = PuntoAtacante(raiz.position + Vector3.up);
            MinecraftHitFeedback golpe = raiz.GetComponent<MinecraftHitFeedback>();
            if (golpe != null)
                golpe.PlayHitFeedbackFromAttacker(origen);

            yield return new WaitForSeconds(0.18f);

            EfectoMuerte muerte = raiz.gameObject.AddComponent<EfectoMuerte>();
            muerte.estilo = EfectoMuerte.Estilo.ArderCenizaBlanca;
            muerte.Reproducir(origen);
            Destroy(raiz.gameObject);
            raiz = null;

            if (reconstruirTras <= 0f)
                yield break;

            yield return new WaitForSeconds(reconstruirTras);
            Reconstruir();
        }

        IEnumerator Secuencia()
        {
            Reconstruir();
            yield return new WaitForSeconds(1.4f);
            GolpearEscudo();
            yield return new WaitForSeconds(1f);
            yield return AtaqueCruz();
            yield return new WaitForSeconds(0.5f);
            RomperCruz();
            yield return new WaitForSeconds(1f);
            GolpearEscudo();
            yield return new WaitForSeconds(0.9f);
            yield return Invocacion();
            yield return new WaitForSeconds(0.6f);
            StartCoroutine(FuegoMonjas());
            yield return new WaitForSeconds(1.2f);
            while (Vivas() > 0)
            {
                RomperCruz();
                yield return new WaitForSeconds(0.9f);
            }
            yield return new WaitForSeconds(0.8f);
            yield return Rematar();
        }

        #endregion
    }
}
