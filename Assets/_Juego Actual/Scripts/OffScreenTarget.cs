using System.Collections.Generic;
using UnityEngine;

public class OffScreenTarget : MonoBehaviour
{
    // Los objetivos activos, para quien necesite saber donde estan (la camara los mete en el encuadre)
    public static readonly List<OffScreenTarget> Activos = new List<OffScreenTarget>();

    private void OnEnable()
    {
        Activos.Add(this);
    }

    private void OnDisable()
    {
        Activos.Remove(this);
    }

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
