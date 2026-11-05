using UnityEngine;

namespace UdeM.Portafolio
{
    /// <summary>
    /// Una imagen colgada en la galeria: pone la textura en el material compartido (por bloque de
    /// propiedades, sin crear materiales) y ajusta el quad a la proporcion de la imagen dentro de
    /// la caja maxima. El marco, si lo tiene, se ajusta con un borde fijo. Funciona tambien en el
    /// editor para poder colgar y mover los cuadros sin dar Play.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshRenderer))]
    public class CuadroGaleria : MonoBehaviour
    {
        static readonly int IdBase = Shader.PropertyToID("_BaseMap");
        static MaterialPropertyBlock bloque;

        public Texture imagen;
        [Tooltip("Ancho y alto maximos del cuadro en unidades.")]
        public Vector2 cajaMaxima = new Vector2(2f, 1.5f);
        public Transform marco;
        [Min(0f)] public float bordeMarco = 0.08f;

        void OnEnable()
        {
            Aplicar();
        }

        void OnValidate()
        {
            Aplicar();
        }

        public void Aplicar()
        {
            if (imagen == null)
                return;

            float aspecto = imagen.height > 0 ? (float)imagen.width / imagen.height : 1f;
            Vector2 tamano = aspecto >= cajaMaxima.x / cajaMaxima.y
                ? new Vector2(cajaMaxima.x, cajaMaxima.x / aspecto)
                : new Vector2(cajaMaxima.y * aspecto, cajaMaxima.y);
            transform.localScale = new Vector3(tamano.x, tamano.y, 1f);

            if (marco != null)
                marco.localScale = new Vector3(tamano.x + bordeMarco * 2f, tamano.y + bordeMarco * 2f, marco.localScale.z);

            if (bloque == null)
                bloque = new MaterialPropertyBlock();
            MeshRenderer render = GetComponent<MeshRenderer>();
            render.GetPropertyBlock(bloque);
            bloque.SetTexture(IdBase, imagen);
            render.SetPropertyBlock(bloque);
        }
    }
}
