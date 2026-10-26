using UnityEngine;
using UnityEngine.UI;

public class ControlVolumen : MonoBehaviour
{
    [SerializeField] private Slider slider;

    void Start()
    {
        slider.SetValueWithoutNotify(VolumenGlobal.Valor);
        slider.onValueChanged.AddListener(VolumenGlobal.Fijar);
    }
}