
using UnityEngine;
using UnityEngine.SceneManagement;

public class BotonReiniciar : MonoBehaviour
{
    [SerializeField] private string escenaInicial = "cementerio";

    public void Reiniciar()
    {
        
       ControladorContrarreloj.Instancia.ReiniciarJuego(escenaInicial);
        
    }
}
