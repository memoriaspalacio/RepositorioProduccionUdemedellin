using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pantalla negra al empezar un nivel que se aclara cuando la música está lista para
/// sonar. La crea BeatManager por su cuenta (no hace falta ponerla en la escena) y se
/// destruye sola al terminar el fundido.
///
/// Va en su propio Canvas por encima de todo y corre en tiempo sin escalar, así que no
/// depende de la UI del nivel ni de Time.timeScale.
/// </summary>
[DisallowMultipleComponent]
public class FundidoInicio : MonoBehaviour
{
    private Image negro;

    /// <summary>Crea el fundido ya en negro opaco.</summary>
    public static FundidoInicio Crear()
    {
        GameObject objeto = new GameObject("FundidoInicio", typeof(RectTransform));

        Canvas canvas = objeto.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        GameObject imagen = new GameObject("Negro", typeof(RectTransform));
        RectTransform rect = (RectTransform)imagen.transform;
        rect.SetParent(objeto.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        FundidoInicio fundido = objeto.AddComponent<FundidoInicio>();
        fundido.negro = imagen.AddComponent<Image>();
        fundido.negro.color = Color.black;
        fundido.negro.raycastTarget = true; // mientras está en negro no se puede pulsar la UI de debajo

        return fundido;
    }

    /// <summary>Pasa de negro a transparente en estos segundos y se destruye.</summary>
    public IEnumerator Desvanecer(float segundos)
    {
        negro.raycastTarget = false;

        for (float t = 0f; t < segundos; t += Time.unscaledDeltaTime)
        {
            // Suave al final para que el nivel "aparezca" en vez de cortarse
            float k = Mathf.Clamp01(t / segundos);
            negro.color = new Color(0f, 0f, 0f, 1f - k * k * (3f - 2f * k));
            yield return null;
        }

        Destroy(gameObject);
    }
}
