using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ponlo en cualquier boton de UI para que abra el menu de filtros de color. Con un texto
/// asignado, el boton muestra el filtro en uso.
/// </summary>
[RequireComponent(typeof(Button))]
public class BotonAbrirFiltros : MonoBehaviour
{
    [Tooltip("Opcional: texto donde mostrar el filtro actual.")]
    public TMP_Text textoFiltro;

    void Awake()
    {
        GetComponent<Button>().onClick.AddListener(Abrir);
    }

    void OnEnable()
    {
        GestorFiltros.AlCambiar += MostrarFiltro;
        MostrarFiltro();
    }

    void OnDisable()
    {
        GestorFiltros.AlCambiar -= MostrarFiltro;
    }

    void MostrarFiltro()
    {
        if (textoFiltro != null)
            textoFiltro.text = MenuFiltros.NombreActual;
    }

    public void Abrir()
    {
        MenuFiltros.Abrir();
    }
}
