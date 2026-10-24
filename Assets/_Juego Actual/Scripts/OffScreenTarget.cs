using UnityEngine;

public class OffScreenTarget : MonoBehaviour
{
    private void Start()
    {
        // Registrar este objeto en el manager cuando se active
        if (OffScreenIndicatorManager.Instance != null)
        {
            OffScreenIndicatorManager.Instance.RegisterTarget(transform);
        }
        else
        {
            Debug.LogWarning("No se encontró el OffScreenIndicatorManager en la escena.");
        }
    }

    private void OnDestroy()
    {
        // Quitarlo del manager cuando el objeto muera/desaparezca
        if (OffScreenIndicatorManager.Instance != null)
        {
            OffScreenIndicatorManager.Instance.UnregisterTarget(transform);
        }
    }
}
