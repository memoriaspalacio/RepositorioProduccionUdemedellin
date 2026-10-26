using UnityEngine;

public static class VolumenGlobal
{
    const string Clave = "volumen_global";

    public static float Valor => PlayerPrefs.GetFloat(Clave, 1f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void AplicarGuardado()
    {
        AudioListener.volume = Valor;
    }

    public static void Fijar(float valor)
    {
        valor = Mathf.Clamp01(valor);
        AudioListener.volume = valor;
        PlayerPrefs.SetFloat(Clave, valor);
    }
}