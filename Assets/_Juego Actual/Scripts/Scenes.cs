using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Scenes : MonoBehaviour
{

    public void IrAEscena(string nombreEscena)
    {
        // Un segundo clic durante la transicion no la reinicia
        if (TransicionCorazon.EnCurso)
            return;

        if (ControladorContrarreloj.Instancia != null) {
            ControladorContrarreloj.Instancia.GuardarTiempoEntreEscena();
        }

        // El corazon funde a negro y la escena se carga con la pantalla cubierta
        TransicionCorazon.Instancia.Reproducir(TransicionCorazon.Fundido.Negro, () => Cargar(nombreEscena));
    }

    static IEnumerator Cargar(string nombreEscena)
    {
        AsyncOperation carga = SceneManager.LoadSceneAsync(nombreEscena);
        while (!carga.isDone)
            yield return null;
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
