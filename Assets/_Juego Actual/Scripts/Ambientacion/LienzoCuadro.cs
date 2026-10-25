using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshRenderer))]
public class LienzoCuadro : MonoBehaviour
{
    static readonly int IdImagen = Shader.PropertyToID("_BaseMap");
    static readonly int IdRecorte = Shader.PropertyToID("_Recorte");
    static readonly int IdRedondo = Shader.PropertyToID("_Redondo");

    static MaterialPropertyBlock bloque;

    public Texture2D imagen;
    public bool redondo;
    [Tooltip("Que parte de la imagen se ve cuando hay que recortarla: 0 = abajo, 1 = arriba.")]
    [Range(0f, 1f)] public float encuadreVertical = 0.7f;
    [Range(0f, 1f)] public float encuadreHorizontal = 0.5f;

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
        MeshRenderer render = GetComponent<MeshRenderer>();
        if (render == null)
            return;

        if (bloque == null)
            bloque = new MaterialPropertyBlock();

        bloque.Clear();
        bloque.SetFloat(IdRedondo, redondo ? 1f : 0f);

        if (imagen != null)
        {
            bloque.SetTexture(IdImagen, imagen);
            bloque.SetVector(IdRecorte, Recorte());
        }

        render.SetPropertyBlock(bloque);
    }

    Vector4 Recorte()
    {
        Vector3 escala = transform.lossyScale;
        float aspectoLienzo = Mathf.Abs(escala.x) / Mathf.Max(0.0001f, Mathf.Abs(escala.y));
        float aspectoImagen = imagen.width / (float)Mathf.Max(1, imagen.height);

        if (aspectoImagen > aspectoLienzo)
        {
            float ancho = aspectoLienzo / aspectoImagen;
            return new Vector4(ancho, 1f, (1f - ancho) * encuadreHorizontal, 0f);
        }

        float alto = aspectoImagen / aspectoLienzo;
        return new Vector4(1f, alto, 0f, (1f - alto) * encuadreVertical);
    }
}
