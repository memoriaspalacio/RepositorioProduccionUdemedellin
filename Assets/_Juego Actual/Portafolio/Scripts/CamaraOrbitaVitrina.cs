using UnityEngine;
using UnityEngine.InputSystem;

namespace UdeM.Portafolio
{
    /// <summary>
    /// Camara de las vitrinas: gira en vaiven alrededor del objetivo (o da la vuelta entera con
    /// amplitud 180) para que los clips tengan movimiento sin que nadie mueva la camara.
    ///   - El giro se pausa con 'orbitar' (la vitrina lo pone en la tecla P).
    ///   - Flechas izquierda y derecha o clic derecho arrastrando giran a mano; arriba y abajo o la
    ///     rueda del raton acercan o alejan.
    ///   - Enfocar lleva el encuadre a otro punto (un personaje) y VolverAlPlano al de partida.
    /// El vaiven usa el tiempo escalado: con la camara lenta tambien va mas despacio.
    /// En el editor se coloca sola al cambiar los valores, asi se encuadra sin dar Play.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CamaraOrbitaVitrina : MonoBehaviour
    {
        [Header("Encuadre")]
        public Vector3 objetivo = new Vector3(0f, 1f, 0f);
        [Min(0.5f)] public float distancia = 8f;
        public float altura = 2f;
        [Tooltip("Angulo de partida en grados alrededor del objetivo (0 = mirando hacia +Z).")]
        public float anguloInicial = 0f;

        [Header("Giro")]
        public bool orbitar = true;
        [Tooltip("Grados a cada lado del angulo inicial. 180 = vuelta completa.")]
        [Range(0f, 180f)] public float amplitud = 25f;
        [Tooltip("Segundos de un vaiven completo (o de una vuelta).")]
        [Min(1f)] public float segundosPorCiclo = 16f;

        [Header("Control manual")]
        [Min(0f)] public float velocidadManual = 60f;

        float fase;
        float anguloManual;
        float alturaManual;
        float zoom = 1f;

        // Encuadre al que se esta yendo (Enfocar) y el original para volver.
        Vector3 objetivoDestino;
        float distanciaDestino;
        float alturaDestino;
        Vector3 objetivoBase;
        float distanciaBase;
        float alturaBase;
        bool enfocando;

        // En Awake: las vitrinas pueden pedir VolverAlPlano desde su Start.
        void Awake()
        {
            objetivoBase = objetivoDestino = objetivo;
            distanciaBase = distanciaDestino = distancia;
            alturaBase = alturaDestino = altura;
        }

        /// <summary>Se acerca con suavidad a otro encuadre (por ejemplo un solo personaje).</summary>
        public void Enfocar(Vector3 nuevoObjetivo, float nuevaDistancia, float nuevaAltura)
        {
            objetivoDestino = nuevoObjetivo;
            // El zoom manual se pasa a la distancia para que el cambio de encuadre no salte.
            distancia *= zoom;
            altura *= zoom;
            zoom = 1f;
            distanciaDestino = nuevaDistancia;
            alturaDestino = nuevaAltura;
            enfocando = true;
        }

        /// <summary>Vuelve al encuadre con el que empezo la escena.</summary>
        public void VolverAlPlano()
        {
            Enfocar(objetivoBase, distanciaBase, alturaBase);
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            float real = Time.unscaledDeltaTime;

            // El control manual va en tiempo real: con la camara lenta se sigue moviendo igual.
            Keyboard teclado = Keyboard.current;
            if (teclado != null)
            {
                if (teclado[Key.LeftArrow].isPressed)
                    anguloManual += velocidadManual * real;
                if (teclado[Key.RightArrow].isPressed)
                    anguloManual -= velocidadManual * real;
                if (teclado[Key.UpArrow].isPressed)
                    Acercar(-0.6f * real);
                if (teclado[Key.DownArrow].isPressed)
                    Acercar(0.6f * real);
            }

            Mouse raton = Mouse.current;
            if (raton != null)
            {
                if (raton.rightButton.isPressed)
                {
                    Vector2 delta = raton.delta.ReadValue();
                    anguloManual += delta.x * 0.25f;
                    alturaManual = Mathf.Clamp(alturaManual - delta.y * 0.01f, -1.5f, 4f);
                }

                float rueda = raton.scroll.ReadValue().y;
                if (Mathf.Abs(rueda) > 0.01f)
                    Acercar(-Mathf.Sign(rueda) * 0.08f);
            }

            if (orbitar)
                fase += dt / segundosPorCiclo;

            if (enfocando)
            {
                float k = 1f - Mathf.Exp(-4f * real);
                objetivo = Vector3.Lerp(objetivo, objetivoDestino, k);
                distancia = Mathf.Lerp(distancia, distanciaDestino, k);
                altura = Mathf.Lerp(altura, alturaDestino, k);
                if ((objetivo - objetivoDestino).sqrMagnitude < 0.0001f && Mathf.Abs(distancia - distanciaDestino) < 0.01f)
                    enfocando = false;
            }

            Colocar(AnguloActual());
        }

        void Acercar(float cantidad)
        {
            zoom = Mathf.Clamp(zoom + cantidad, 0.15f, 2.5f);
        }

        float AnguloActual()
        {
            float giro = amplitud >= 180f
                ? fase * 360f
                : Mathf.Sin(fase * Mathf.PI * 2f) * amplitud;
            return anguloInicial + anguloManual + giro;
        }

        /// <summary>Pone la camara en el angulo indicado mirando al objetivo.</summary>
        public void Colocar(float angulo)
        {
            Vector3 direccion = Quaternion.Euler(0f, angulo, 0f) * Vector3.back;
            transform.position = objetivo + direccion * (distancia * zoom) + Vector3.up * (altura * zoom + alturaManual);
            transform.LookAt(objetivo);
        }

        void OnValidate()
        {
            if (!Application.isPlaying)
                Colocar(anguloInicial);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.4f, 1f);
            Gizmos.DrawWireSphere(objetivo, 0.15f);
        }
    }
}
