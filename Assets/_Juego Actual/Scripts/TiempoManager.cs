using UnityEngine;

public class TiempoManager : MonoBehaviour
{
    public void TiempoGanar() 
    {
        ControladorContrarreloj.Instancia.Ganar();
    }
    public void TiempoPerder()
    {
        ControladorContrarreloj.Instancia.Perder();
    }
}
