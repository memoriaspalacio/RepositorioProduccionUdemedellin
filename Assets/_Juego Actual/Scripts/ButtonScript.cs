using UnityEngine;
using UnityEngine.UI;

public class ButtonScript : MonoBehaviour
{
    private Button boton;

    private void Start()
    {
        // Sin componente Button (p. ej. el contenedor BotonesMenu) no hay clic que sonar
        if (!TryGetComponent(out boton))
            return;

        boton.onClick.AddListener(SoundButton);
    }

    private void SoundButton()
    {
        SoundManager.singleton.PlaySFX("sonidoBoton");
    }
}
