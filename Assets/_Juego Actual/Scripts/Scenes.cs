using UnityEngine;
using UnityEngine.SceneManagement;

public class Scenes : MonoBehaviour
{

    public void IrAEscena(string nombreEscena)
    {

        SceneManager.LoadScene(nombreEscena);
    }
}
