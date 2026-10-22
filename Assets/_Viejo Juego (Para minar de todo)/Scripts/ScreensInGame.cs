using UnityEngine;
using UnityEngine.SceneManagement;
public class ScreensInGame : MonoBehaviour
{
    public static ScreensInGame singleton;
    public GameObject soundMusicBeat;

    public GameObject screenLose;
    public GameObject screenWin;
    private void Awake()
    {
        singleton = this;
    }

    private void Start()
    {
        ScreenLostDesactive();
        ScreenWinDesactive();
    }

    public void ScreenLostActive()
    {
        screenLose.SetActive(true);
        soundMusicBeat.SetActive(false);
        PausarContrarreloj();
    }

    public void ScreenLostDesactive()
    {
        screenLose.SetActive(false);
    }

    public void ScreenWinActive()
    {
        screenWin.SetActive(true);
        PausarContrarreloj();
    }

    // El nivel termino: el cronometro deja de correr y el ritmo deja de cobrar penalizaciones
    private void PausarContrarreloj()
    {
        if (ControladorContrarreloj.Instancia != null)
            ControladorContrarreloj.Instancia.PausarContrarreloj();
    }

    public void ScreenWinDesactive()
    {
        screenWin.SetActive(false);
    }

    public void ResetScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }


}
