using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ContadorEnemigos : MonoBehaviour
{

    [SerializeField] private List<GameObject> _enemies = new List<GameObject>(); //lista para almacenar enemigos
    [SerializeField] private TextMeshProUGUI totalEnemies;
    
    
    //[SerializeField] private GameObject _victory;
    // Start is called before the first frame update

    
    void Start()
    {
        _enemies.AddRange(GameObject.FindGameObjectsWithTag("Enemy"));//al comenzar la escena busca todos los objetos con tag Enemy
        totalEnemies.text = _enemies.Count.ToString();
        
    }

    

    public void EnemyDied(GameObject enemy)
    {
        _enemies.Remove(enemy);
        CheckForWinCondition();
    }

    private void CheckForWinCondition()
    {
        if (_enemies.Count == 0)
        {
            Debug.Log("Gané");
            ActiveExit();
        }
    }

    private void ActiveExit()
    {
        
        
        Debug.Log("Salida Activada");
        ScreensInGame.singleton.ScreenWinActive();
    }
}
