using System.Collections.Generic;
using UnityEngine;

public class OffScreenIndicatorManager : MonoBehaviour
{
    public static OffScreenIndicatorManager Instance;

    [Header("UI Referencias")]
    [Tooltip("El prefab de la flecha que se instanciará (debe tener un RectTransform).")]
    public GameObject indicatorPrefab;
    [Tooltip("El Canvas o Panel donde se colocarán las flechas.")]
    public Transform indicatorContainer;
    
    [Header("Configuracion")]
    [Tooltip("Margen desde el borde de la pantalla donde se detendrá el indicador.")]
    public float padding = 50f;

    private Camera mainCamera;
    
    // Lista de objetivos (enemigos)
    private List<Transform> targets = new List<Transform>();
    
    // Diccionario para relacionar un objetivo con su indicador de UI
    private Dictionary<Transform, RectTransform> targetIndicators = new Dictionary<Transform, RectTransform>();

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    private void Start()
    {
        mainCamera = Camera.main;
    }

    public void RegisterTarget(Transform target)
    {
        if (!targets.Contains(target))
        {
            targets.Add(target);
            
            if (indicatorPrefab != null && indicatorContainer != null)
            {
                GameObject newIndicator = Instantiate(indicatorPrefab, indicatorContainer);
                targetIndicators[target] = newIndicator.GetComponent<RectTransform>();
                newIndicator.SetActive(false); // Oculto al inicio
            }
            else
            {
                Debug.LogWarning("Faltan referencias de UI en el OffScreenIndicatorManager.");
            }
        }
    }

    public void UnregisterTarget(Transform target)
    {
        if (targets.Contains(target))
        {
            targets.Remove(target);
            if (targetIndicators.ContainsKey(target))
            {
                if (targetIndicators[target] != null)
                {
                    Destroy(targetIndicators[target].gameObject);
                }
                targetIndicators.Remove(target);
            }
        }
    }

    private void LateUpdate()
    {
        if (mainCamera == null) return;

        foreach (Transform target in targets)
        {
            if (target == null) continue;

            if (!targetIndicators.ContainsKey(target) || targetIndicators[target] == null)
                continue;

            RectTransform indicator = targetIndicators[target];
            
            // Posición del objetivo en la pantalla
            Vector3 screenPos = mainCamera.WorldToScreenPoint(target.position);

            // Saber si está detrás de la cámara
            bool isBehindCamera = screenPos.z < 0;

            // Saber si está fuera de los límites de la pantalla
            bool isOffScreen = isBehindCamera || screenPos.x <= 0 || screenPos.x >= Screen.width || screenPos.y <= 0 || screenPos.y >= Screen.height;

            if (isOffScreen)
            {
                indicator.gameObject.SetActive(true);

                // Si está detrás de la cámara, invertimos su posición en pantalla
                if (isBehindCamera)
                {
                    screenPos.x = Screen.width - screenPos.x;
                    screenPos.y = Screen.height - screenPos.y;
                }

                // Clampear a los bordes con el padding
                Vector3 clampedPos = screenPos;
                clampedPos.x = Mathf.Clamp(screenPos.x, padding, Screen.width - padding);
                clampedPos.y = Mathf.Clamp(screenPos.y, padding, Screen.height - padding);
                
                // Aplicar posición al UI
                indicator.position = clampedPos;

                // Como es un ícono, nos aseguramos de que siempre esté derecho sin rotar
                indicator.rotation = Quaternion.identity;
            }
            else
            {
                // Si está dentro de la pantalla, ocultamos el indicador
                indicator.gameObject.SetActive(false);
            }
        }
    }
}
