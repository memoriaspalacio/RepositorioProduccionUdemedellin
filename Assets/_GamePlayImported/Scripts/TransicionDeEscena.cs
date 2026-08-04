using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TransicionDeEscena : MonoBehaviour
{
    public void Comenzar()
    {
        SceneManager.LoadScene("In Game");
    }

    public void MenuPrincipal()
    {
        SceneManager.LoadScene("Menu");
    }
}
