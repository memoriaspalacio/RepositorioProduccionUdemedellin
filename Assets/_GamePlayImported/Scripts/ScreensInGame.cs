using UnityEngine;

public class ScreensInGame : MonoBehaviour
{
    public static ScreensInGame singleton;

    public GameObject screenLose;

    private void Awake()
    {
        singleton = this;
    }

    private void Start()
    {
        ScreenLostDesactive();
    }

    public void ScreenLostActive()
    {
        screenLose.SetActive(true);
    }

    public void ScreenLostDesactive()
    {
        screenLose.SetActive(false);
    }


}
