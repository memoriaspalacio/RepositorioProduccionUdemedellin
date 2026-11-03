using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ponlo en cualquier boton de UI (un "Opciones" del menu principal, una pausa...) para que
/// abra el menu de graficos. Tambien sirve asignar Abrir en el OnClick del boton. Con un texto
/// asignado, el boton muestra la calidad en uso ("Alto", "Personalizado"...).
/// </summary>
[RequireComponent(typeof(Button))]
public class BotonAbrirGraficos : MonoBehaviour
{
    [Tooltip("Opcional: texto donde mostrar la calidad actual.")]
    public TMP_Text textoNivel;

    void Awake()
    {
        GetComponent<Button>().onClick.AddListener(Abrir);
    }

    void OnEnable()
    {
        GestorGraficos.AlCambiar += MostrarNivel;
        MostrarNivel();
    }

    void OnDisable()
    {
        GestorGraficos.AlCambiar -= MostrarNivel;
    }

    void MostrarNivel()
    {
        if (textoNivel != null)
            textoNivel.text = OpcionesGraficas.NombreCalidad(GestorGraficos.Ajustes);
    }

    public void Abrir()
    {
        MenuGraficos.Abrir();
    }
}
