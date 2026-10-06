using UnityEngine;

// Representa un sonido y su configuración de reproducción.
[System.Serializable]
public class Sound
{
    // Nombre utilizado para identificar el sonido.
    public string name;

    // Archivo de audio asociado al sonido.
    public AudioClip clip;

    // Volumen con el que se reproducirá el sonido.
    [Range(0f, 1f)]
    public float volume = 1f;

    // Velocidad y tono con el que se reproducirá el sonido.
    [Range(0.1f, 3f)]
    public float pitch = 1f;

    // Indica si el sonido debe reproducirse en bucle.
    public bool loop = false;
}