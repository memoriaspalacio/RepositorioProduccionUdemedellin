using UnityEngine;
using UnityEngine.SceneManagement;

public class TiempoManager : MonoBehaviour
{
    public void TiempoGanar()
    {
        // El reloj nace en el cementerio y viaja entre niveles: si se llega a Crisantia sin pasar
        // por ahi no existe, y sin esto el boton de ganar fallaba y no salia la cinematica final.
        if (ControladorContrarreloj.Instancia != null)
            ControladorContrarreloj.Instancia.Ganar();
        else
            CinematicaFinal.Reproducir("Victoria");
    }
    public void TiempoPerder()
    {
        if (ControladorContrarreloj.Instancia != null)
            ControladorContrarreloj.Instancia.Perder();
        else
            SceneManager.LoadScene("Perder");
    }
}
