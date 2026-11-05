using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace UdeM.Portafolio
{
    /// <summary>
    /// Reproduce cualquier AnimationClip en un Animator con un fundido corto, sin pasar por los
    /// estados del controlador. Asi la vitrina de animaciones puede mostrar tambien los clips que
    /// no estan conectados en el Animator (Walk, GetHurt...). Usa un PlayableGraph con un mezclador
    /// de dos entradas: la 0 es el clip que sale y la 1 el que entra.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class ReproductorClips : MonoBehaviour
    {
        [Min(0f)] public float duracionFundido = 0.25f;

        PlayableGraph grafo;
        AnimationMixerPlayable mezcla;
        float peso = 1f;

        public AnimationClip ClipActual { get; private set; }
        public float TiempoActual => mezcla.IsValid() && mezcla.GetInput(1).IsValid() ? (float)mezcla.GetInput(1).GetTime() : 0f;

        void Awake()
        {
            Animator animador = GetComponent<Animator>();
            // El controlador seguiria mandando sus propios estados por encima.
            animador.runtimeAnimatorController = null;
            animador.applyRootMotion = false;

            grafo = PlayableGraph.Create("Vitrina " + name);
            grafo.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            mezcla = AnimationMixerPlayable.Create(grafo, 2);
            AnimationPlayableOutput salida = AnimationPlayableOutput.Create(grafo, "Animacion", animador);
            salida.SetSourcePlayable(mezcla);
            grafo.Play();
        }

        public void Reproducir(AnimationClip clip)
        {
            if (clip == null || !grafo.IsValid())
                return;

            // El clip que sonaba pasa a la entrada 0 para fundirse con el nuevo.
            Playable anterior = mezcla.GetInput(0);
            Playable actual = mezcla.GetInput(1);
            grafo.Disconnect(mezcla, 0);
            grafo.Disconnect(mezcla, 1);
            if (anterior.IsValid())
                anterior.Destroy();
            if (actual.IsValid())
                grafo.Connect(actual, 0, mezcla, 0);

            AnimationClipPlayable nuevo = AnimationClipPlayable.Create(grafo, clip);
            grafo.Connect(nuevo, 0, mezcla, 1);
            ClipActual = clip;
            peso = actual.IsValid() && duracionFundido > 0f ? 0f : 1f;
            Aplicar();
        }

        void Update()
        {
            if (!mezcla.IsValid() || peso >= 1f)
                return;

            peso = Mathf.MoveTowards(peso, 1f, Time.deltaTime / duracionFundido);
            Aplicar();
        }

        void Aplicar()
        {
            mezcla.SetInputWeight(0, 1f - peso);
            mezcla.SetInputWeight(1, peso);
        }

        void OnDestroy()
        {
            if (grafo.IsValid())
                grafo.Destroy();
        }
    }
}
