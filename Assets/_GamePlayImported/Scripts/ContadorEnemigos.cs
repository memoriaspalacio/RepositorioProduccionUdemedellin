using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ContadorEnemigos : MonoBehaviour
{

    [SerializeField] private List<GameObject> _enemies = new List<GameObject>();
    [SerializeField] private GameObject _victory;
    // Start is called before the first frame update
    void Start()
    {
        _enemies.AddRange(GameObject.FindGameObjectsWithTag("Enemy"));
    }

    // Update is called once per frame
    void Update()
    {
        EnemyDied();
    }
    
    public void EnemyDied()
    {
        _enemies.RemoveAll(enemy => enemy == null);
        CheckForWinCondition();
    }

    private void CheckForWinCondition()
    {
        if (_enemies.Count == 0)
        {
            Debug.Log("Gané");
            _victory.SetActive(true);
        }
    }
}
