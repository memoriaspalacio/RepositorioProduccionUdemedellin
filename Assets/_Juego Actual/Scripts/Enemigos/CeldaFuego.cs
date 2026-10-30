using UnityEngine;

/// <summary>
/// Una celda del fuego de las monjas (SisterAttackCell). Le dice al shader UdeM/Efectos/Fuego
/// cuando se encendio, para que la celda crezca desde el centro, y la apaga desvaneciendola en
/// vez de quitarla de golpe. Con pocos efectos (calidad grafica baja) deja solo las brasas y
/// una llama: el area del ataque se sigue viendo entera.
/// </summary>
public class CeldaFuego : MonoBehaviour
{
    static readonly int IdInicio = Shader.PropertyToID("_Inicio");
    static readonly int IdFin = Shader.PropertyToID("_Fin");

    [Tooltip("Segundos que tarda en desvanecerse al apagarse. Igual que _DuracionApagado del material.")]
    [SerializeField, Min(0f)] private float duracionApagado = 0.3f;

    [Tooltip("Llamas que sobran con pocos efectos (graficos en Minimas o Apagadas).")]
    [SerializeField] private GameObject[] llamasExtra;

    Renderer[] renderers;
    MaterialPropertyBlock bloque;
    bool apagandose;

    void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        bloque = new MaterialPropertyBlock();
        Fijar(IdInicio, Time.timeSinceLevelLoad);

        if (GestorGraficos.MultiplicadorEfectos < 0.4f && llamasExtra != null)
        {
            foreach (GameObject llama in llamasExtra)
            {
                if (llama != null)
                    llama.SetActive(false);
            }
        }
    }

    /// <summary>Apaga la celda con su desvanecimiento y la destruye al terminar.</summary>
    public void Apagar()
    {
        if (apagandose)
            return;

        apagandose = true;
        Fijar(IdFin, Time.timeSinceLevelLoad);
        Destroy(gameObject, duracionApagado);
    }

    /// <summary>Apaga una celda de fuego; si no lo es (otro prefab), la destruye sin mas.</summary>
    public static void Apagar(GameObject celda)
    {
        if (celda == null)
            return;

        if (celda.TryGetComponent(out CeldaFuego fuego))
            fuego.Apagar();
        else
            Destroy(celda);
    }

    void Fijar(int propiedad, float valor)
    {
        foreach (Renderer render in renderers)
        {
            render.GetPropertyBlock(bloque);
            bloque.SetFloat(propiedad, valor);
            render.SetPropertyBlock(bloque);
        }
    }
}
