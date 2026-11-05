using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UdeM.Portafolio
{
    /// <summary>
    /// Lo que comparten las vitrinas montadas sobre una copia de un nivel con su ritmo funcionando:
    ///   - Un bot que pulsa en las notas con la precision elegida, para que el corazon, las
    ///     calificaciones, la pista y el indicador del suelo reaccionen como jugando. Solo pulsa
    ///     cuando el juez tiene una nota abierta, asi nunca provoca un golpe al aire por error.
    ///   - Las acciones de la interfaz: golpe fuera de ritmo, dano, enemigo derrotado, tiempo,
    ///     curacion y transiciones, llamando a los sistemas del juego.
    ///   - Que la vitrina no termine: el jugador no muere y el reloj no baja de un minimo.
    /// </summary>
    public class RitmoVitrina : MonoBehaviour
    {
        public enum Calidad { Apagado, Perfecto, Bueno, Malo, Fallar, Mezcla }

        [Header("Juego")]
        public BeatActionJudge juez;
        public PlayerHealth jugador;
        [Tooltip("Enemigo de la escena (apagado) que se copia para mostrar las recompensas al derrotarlo.")]
        public GameObject plantillaEnemigo;

        [Header("Bot de ritmo")]
        public Calidad bot = Calidad.Mezcla;

        [Header("Que la vitrina no termine")]
        [Tooltip("Por debajo de estos segundos el reloj recupera tiempo.")]
        [Min(1f)] public float tiempoMinimo = 4f;
        [Min(1f)] public float tiempoRecuperado = 60f;
        [Tooltip("Fraccion de vida por debajo de la cual el jugador se cura sin efectos.")]
        [Range(0.05f, 0.9f)] public float vidaMinima = 0.35f;

        // Mezcla pensada para un clip: casi todo bien, con un fallo de cada tipo de vez en cuando.
        static readonly Calidad[] Ciclo =
        {
            Calidad.Perfecto, Calidad.Perfecto, Calidad.Bueno, Calidad.Perfecto, Calidad.Malo,
            Calidad.Perfecto, Calidad.Perfecto, Calidad.Fallar, Calidad.Bueno, Calidad.Perfecto
        };

        // Notas ya decididas (pulsada o dejada pasar) y la calidad que le toco a cada una.
        readonly Dictionary<float, Calidad> plan = new Dictionary<float, Calidad>();
        readonly HashSet<float> hechas = new HashSet<float>();
        readonly List<float> viejas = new List<float>();
        int indiceCiclo;
        bool golpeAlAire;

        void Awake()
        {
            if (juez == null)
                juez = FindFirstObjectByType<BeatActionJudge>();
            if (jugador == null)
                jugador = FindFirstObjectByType<PlayerHealth>();
            if (plantillaEnemigo != null)
                plantillaEnemigo.SetActive(false);
        }

        public void Bot(Calidad calidad)
        {
            bot = calidad;
            // Las notas ya decididas siguen su plan; el cambio vale desde la siguiente.
            plan.Clear();
        }

        void Update()
        {
            MantenerPartida();
            if (golpeAlAire)
                IntentarGolpeAlAire();
            else
                Pulsar();
        }

        #region Bot

        void Pulsar()
        {
            BeatManager ritmo = BeatManager.Instance;
            if (bot == Calidad.Apagado || juez == null || ritmo == null || !ritmo.IsPlaying)
                return;

            double ahora = ritmo.CurrentSongTime;
            float segundosPorBeat = ritmo.SecPerBeat;
            float beatAhora = (float)(ahora / segundosPorBeat);
            float temprano = juez.EarlyTolerance;
            float tarde = juez.LateTolerance;
            const float paso = 1f / BeatActionJudge.SlotsPerBeat;

            // Solo las notas con la ventana abierta ahora: las que ya salieron de ella dejan de estar
            // en el juez y IsBeatResolved las daria por pendientes.
            for (float beat = Mathf.Ceil((beatAhora - tarde) * BeatActionJudge.SlotsPerBeat) * paso; beat <= beatAhora + temprano; beat += paso)
            {
                if (hechas.Contains(beat) || !juez.IsNoteBeat(beat) || juez.IsBeatResolved(beat))
                    continue;

                if (!plan.TryGetValue(beat, out Calidad calidad))
                {
                    calidad = bot == Calidad.Mezcla ? Ciclo[indiceCiclo++ % Ciclo.Length] : bot;
                    plan[beat] = calidad;
                }

                // Dejarla pasar: el juez la marca MISS al salir de la ventana.
                if (calidad == Calidad.Fallar)
                {
                    hechas.Add(beat);
                    continue;
                }

                if (ahora < beat * segundosPorBeat + Desfase(calidad, segundosPorBeat))
                    continue;

                if (juez.GradeIfPressedNow != JudgementResult.Miss)
                    juez.TryConsumeBeat();
                hechas.Add(beat);
            }

            Olvidar(beatAhora - 4f);
        }

        void Olvidar(float antesDe)
        {
            viejas.Clear();
            foreach (float beat in hechas)
            {
                if (beat < antesDe)
                    viejas.Add(beat);
            }
            foreach (float beat in plan.Keys)
            {
                if (beat < antesDe)
                    viejas.Add(beat);
            }
            for (int i = 0; i < viejas.Count; i++)
            {
                hechas.Remove(viejas[i]);
                plan.Remove(viejas[i]);
            }
        }

        // Segundos respecto al beat exacto en los que pulsa el bot para sacar cada calificacion.
        float Desfase(Calidad calidad, float segundosPorBeat)
        {
            float bueno = juez.GoodWindowSeconds;
            float temprano = juez.EarlyTolerance * segundosPorBeat;
            float tarde = juez.LateTolerance * segundosPorBeat;

            switch (calidad)
            {
                case Calidad.Bueno:
                    return Mathf.Min((juez.PerfectWindowSeconds + bueno) * 0.5f, tarde * 0.9f);
                case Calidad.Malo:
                    // Antes de tiempo si cabe (mas visible), si no despues.
                    float antes = Mathf.Min(bueno + 0.04f, temprano * 0.9f);
                    return antes > bueno ? -antes : Mathf.Min(bueno + 0.03f, tarde * 0.95f);
                default:
                    return 0f;
            }
        }

        /// <summary>La proxima vez que no haya ninguna nota abierta pulsa: MISS y castigo de tiempo.</summary>
        public void GolpeAlAire()
        {
            golpeAlAire = true;
        }

        void IntentarGolpeAlAire()
        {
            BeatManager ritmo = BeatManager.Instance;
            if (juez == null || ritmo == null || !ritmo.IsPlaying)
                return;
            if (juez.GradeIfPressedNow != JudgementResult.Miss)
                return;

            juez.TryConsumeBeat();
            golpeAlAire = false;
        }

        #endregion

        #region Acciones

        public void RecibirDano()
        {
            if (jugador == null)
                return;

            Transform t = jugador.transform;
            jugador.Damage(Mathf.Max(1f, jugador.maxLife * 0.15f), t.position + t.forward * 1.5f);
        }

        public void Curar()
        {
            if (jugador != null)
                jugador.Curar(jugador.maxLife * 0.3f);
        }

        public void DerrotarEnemigo()
        {
            StartCoroutine(Derrotar());
        }

        IEnumerator Derrotar()
        {
            if (plantillaEnemigo == null || jugador == null)
                yield break;

            Transform t = jugador.transform;
            Vector3 frente = t.forward;
            frente.y = 0f;
            frente = frente.sqrMagnitude > 0.001f ? frente.normalized : Vector3.forward;

            GameObject enemigo = Instantiate(plantillaEnemigo, t.position + frente * 2f, Quaternion.LookRotation(-frente));
            EnemyHealth vida = enemigo.GetComponent<EnemyHealth>();
            // Sin contador: en esta escena un enemigo muerto no debe dar la victoria.
            if (vida != null)
                vida.contador = null;
            enemigo.SetActive(true);
            StartCoroutine(UtilVitrina.Crecer(enemigo.transform, 0.4f));

            yield return new WaitForSeconds(0.8f);

            if (jugador.anim != null)
                UtilVitrina.Trigger(jugador.anim, "onAttack");
            yield return new WaitForSeconds(0.15f);

            if (enemigo == null)
                yield break;
            if (vida != null)
                vida.Damage(vida.currentLife + 1f, t.position);

            // Death no lo destruye (lo hacia el contador); el efecto ya copio el cuerpo.
            yield return null;
            if (enemigo != null)
                Destroy(enemigo);
        }

        public void GanarTiempo(float segundos)
        {
            ControladorContrarreloj reloj = ControladorContrarreloj.Instancia;
            if (reloj != null)
                reloj.AgregarTiempo(segundos);
        }

        public void PerderTiempo(float segundos)
        {
            ControladorContrarreloj reloj = ControladorContrarreloj.Instancia;
            if (reloj != null)
                reloj.PenalizarTiempo(segundos);
        }

        /// <summary>Deja el reloj en zona critica; MantenerPartida lo recupera al llegar al minimo.</summary>
        public void TiempoCritico()
        {
            ControladorContrarreloj reloj = ControladorContrarreloj.Instancia;
            if (reloj == null || !reloj.EstaContando)
                return;

            float objetivo = tiempoMinimo + 6f;
            if (reloj.TiempoRestante > objetivo)
                reloj.PenalizarTiempo(reloj.TiempoRestante - objetivo);
        }

        public void TransicionCorazon()
        {
            if (!global::TransicionCorazon.EnCurso)
                global::TransicionCorazon.Instancia.Reproducir(global::TransicionCorazon.Fundido.Blanco, () => Esperar(0.6f));
        }

        public void FundidoDeInicio()
        {
            StartCoroutine(global::FundidoInicio.Crear().Desvanecer(1.2f));
        }

        static IEnumerator Esperar(float segundos)
        {
            yield return new WaitForSecondsRealtime(segundos);
        }

        void MantenerPartida()
        {
            ControladorContrarreloj reloj = ControladorContrarreloj.Instancia;
            if (reloj != null && reloj.EstaContando && reloj.TiempoRestante < tiempoMinimo)
                reloj.AgregarTiempo(tiempoRecuperado, false);

            if (jugador != null && !jugador.EstaMuerto && jugador.maxLife > 0f && jugador.currentLife < jugador.maxLife * vidaMinima)
            {
                jugador.currentLife = jugador.maxLife;
                if (jugador.lifeBar != null)
                    jugador.lifeBar.value = jugador.currentLife;
            }
        }

        #endregion
    }
}
