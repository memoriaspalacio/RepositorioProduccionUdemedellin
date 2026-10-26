using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ponlo en cualquier boton de UI (un "Opciones" del menu principal, una pausa...) para que
/// abra el menu de graficos. Tambien sirve asignar Abrir en el OnClick del boton.
/// </summary>
[RequireComponent(typeof(Button))]
public class BotonAbrirGraficos : MonoBehaviour
{
    void Awake()
    {
        GetComponent<Button>().onClick.AddListener(Abrir);
    }

    public void Abrir()
    {
        MenuGraficos.Abrir();
    }
}
