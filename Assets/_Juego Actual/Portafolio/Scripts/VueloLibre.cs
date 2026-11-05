using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UdeM.Portafolio
{
    /// <summary>
    /// Camara para recorrer un nivel como en la Scene view:
    ///   - Clic derecho arrastrando mira alrededor; WASD avanza y se desplaza, Q y E bajan y suben,
    ///     Shift corre y la rueda del raton cambia la velocidad.
    ///   - Recorrido automatico: pasa en bucle por los puntos de 'puntosRecorrido' con una curva
    ///     suave (Catmull-Rom), para grabar el nivel sin tocar nada. Los puntos se mueven en el
    ///     editor como cualquier objeto: su posicion y hacia donde miran es el plano.
    /// Todo va en tiempo real, asi la camara lenta de la vitrina no la frena.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class VueloLibre : MonoBehaviour
    {
        [Header("Vuelo")]
        [Min(0.1f)] public float velocidad = 4f;
        [Min(1f)] public float multiplicadorRapido = 3f;
        [Min(0.01f)] public float sensibilidad = 0.15f;

        [Header("Recorrido automatico")]
        public List<Transform> puntosRecorrido = new List<Transform>();
        [Min(0.5f)] public float segundosPorTramo = 6f;
        public bool recorriendo;

        float yaw;
        float pitch;
        float avance;

        void Start()
        {
            SincronizarAngulos();
        }

        public void AlternarRecorrido()
        {
            recorriendo = !recorriendo && PuntosValidos() >= 2;
            if (!recorriendo)
                SincronizarAngulos();
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;

            if (recorriendo && PuntosValidos() >= 2)
            {
                Recorrer(dt);
                return;
            }

            Mouse raton = Mouse.current;
            if (raton != null)
            {
                if (raton.rightButton.isPressed)
                {
                    Vector2 delta = raton.delta.ReadValue();
                    yaw += delta.x * sensibilidad;
                    pitch = Mathf.Clamp(pitch - delta.y * sensibilidad, -85f, 85f);
                }

                float rueda = raton.scroll.ReadValue().y;
                if (Mathf.Abs(rueda) > 0.01f)
                    velocidad = Mathf.Clamp(velocidad * (rueda > 0f ? 1.15f : 1f / 1.15f), 0.3f, 60f);
            }
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

            Keyboard teclado = Keyboard.current;
            if (teclado == null)
                return;

            Vector3 mover = Vector3.zero;
            if (teclado[Key.W].isPressed) mover += transform.forward;
            if (teclado[Key.S].isPressed) mover -= transform.forward;
            if (teclado[Key.D].isPressed) mover += transform.right;
            if (teclado[Key.A].isPressed) mover -= transform.right;
            if (teclado[Key.E].isPressed) mover += Vector3.up;
            if (teclado[Key.Q].isPressed) mover -= Vector3.up;

            float rapido = teclado[Key.LeftShift].isPressed || teclado[Key.RightShift].isPressed ? multiplicadorRapido : 1f;
            transform.position += mover * (velocidad * rapido * dt);
        }

        void Recorrer(float dt)
        {
            List<Transform> puntos = Validos();
            int cantidad = puntos.Count;
            avance = (avance + dt / segundosPorTramo) % cantidad;

            int i = Mathf.FloorToInt(avance);
            float u = avance - i;
            Vector3 p0 = puntos[(i - 1 + cantidad) % cantidad].position;
            Vector3 p1 = puntos[i].position;
            Vector3 p2 = puntos[(i + 1) % cantidad].position;
            Vector3 p3 = puntos[(i + 2) % cantidad].position;

            transform.position = CatmullRom(p0, p1, p2, p3, u);
            float suave = u * u * (3f - 2f * u);
            transform.rotation = Quaternion.Slerp(puntos[i].rotation, puntos[(i + 1) % cantidad].rotation, suave);
        }

        static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 - 3f * p2 + p3) * t3);
        }

        void SincronizarAngulos()
        {
            Vector3 angulos = transform.eulerAngles;
            yaw = angulos.y;
            pitch = angulos.x > 180f ? angulos.x - 360f : angulos.x;
        }

        int PuntosValidos()
        {
            int cantidad = 0;
            for (int i = 0; i < puntosRecorrido.Count; i++)
            {
                if (puntosRecorrido[i] != null)
                    cantidad++;
            }
            return cantidad;
        }

        List<Transform> Validos()
        {
            List<Transform> validos = new List<Transform>();
            for (int i = 0; i < puntosRecorrido.Count; i++)
            {
                if (puntosRecorrido[i] != null)
                    validos.Add(puntosRecorrido[i]);
            }
            return validos;
        }

        void OnDrawGizmos()
        {
            List<Transform> puntos = Validos();
            if (puntos.Count < 2)
                return;

            Gizmos.color = new Color(1f, 0.8f, 0.4f, 1f);
            for (int i = 0; i < puntos.Count; i++)
            {
                Vector3 p0 = puntos[(i - 1 + puntos.Count) % puntos.Count].position;
                Vector3 p1 = puntos[i].position;
                Vector3 p2 = puntos[(i + 1) % puntos.Count].position;
                Vector3 p3 = puntos[(i + 2) % puntos.Count].position;
                Vector3 anterior = p1;
                for (int k = 1; k <= 12; k++)
                {
                    Vector3 siguiente = CatmullRom(p0, p1, p2, p3, k / 12f);
                    Gizmos.DrawLine(anterior, siguiente);
                    anterior = siguiente;
                }
                Gizmos.DrawRay(p1, puntos[i].forward);
            }
        }
    }
}
