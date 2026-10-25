using UnityEngine;
using UnityEngine.SceneManagement;

public class Scenes : MonoBehaviour
{

    public void IrAEscena(string nombreEscena)
    {
        if (ControladorContrarreloj.Instancia != null) {
            ControladorContrarreloj.Instancia.GuardarTiempoEntreEscena();
        }
        SceneManager.LoadScene(nombreEscena);
    }

    public void ReiniciarEscena()
    {
        ControladorContrarreloj.Instancia.ReiniciarContrarRelojAlInicioDeEscena();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void FinDelJuego(string nombreEscena) {
        ControladorContrarreloj.Instancia.OnDestroy();
        SceneManager.LoadScene(nombreEscena);
    }
}
