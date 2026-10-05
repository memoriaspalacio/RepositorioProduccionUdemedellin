using UnityEngine;
using UnityEngine.UI;

public class ButtonScript : MonoBehaviour
{
    private Button boton;

    private void Start()
    {
        // Forzosamente el objeto debe tener el componente Button en Unity
        boton = GetComponent<Button>();
        boton.onClick.AddListener(SoundButton);
    }

    private void SoundButton()
    {
        SoundManager.singleton.PlaySFX("sonidoBoton");
    }
}
