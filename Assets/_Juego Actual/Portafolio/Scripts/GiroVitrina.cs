using UnityEngine;

namespace UdeM.Portafolio
{
    /// <summary>Gira el objeto sobre su eje vertical, como un plato giratorio de exposicion.</summary>
    public class GiroVitrina : MonoBehaviour
    {
        [Tooltip("Grados por segundo.")]
        public float velocidad = 18f;

        void Update()
        {
            transform.Rotate(Vector3.up, velocidad * Time.deltaTime, Space.World);
        }
    }
}
