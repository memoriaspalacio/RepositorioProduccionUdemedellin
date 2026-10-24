using System.Collections.Generic;
using UnityEngine;

// Administra y reproduce los sonidos disponibles en la escena.
public class SoundManager : MonoBehaviour
{
    // Instancia del SoundManager disponible en la escena actual.
    public static SoundManager singleton { get; private set; }

    // AudioSource utilizado exclusivamente para reproducir música.
    [SerializeField] private AudioSource musicSource;

    // AudioSource utilizado para reproducir efectos de sonido.
    [SerializeField] private AudioSource sfxSource;

    // Lista de sonidos disponibles para reproducir en la escena.
    [SerializeField] private List<Sound> sounds = new List<Sound>();

    // Inicializa la instancia única del SoundManager de la escena.
    private void Awake()
    {
        if (singleton != null && singleton != this)
        {
            Destroy(gameObject);
            return;
        }

        singleton = this;
    }

    // Busca un sonido por su nombre dentro de la lista.
    private Sound GetSound(string soundName)
    {
        return sounds.Find(sound => sound.name == soundName);
    }

    // Reproduce un efecto de sonido identificado por su nombre.
    public void PlaySFX(string soundName)
    {
        Sound sound = GetSound(soundName);

        if (sound == null)
        {
            Debug.LogWarning($"Sound '{soundName}' no existe.");
            return;
        }

        sfxSource.pitch = sound.pitch;
        sfxSource.PlayOneShot(sound.clip, sound.volume);
    }

    // Reproduce una música identificada por su nombre.
    public void PlayMusic(string soundName)
    {
        Sound sound = GetSound(soundName);

        if (sound == null)
        {
            Debug.LogWarning($"Sound '{soundName}' no existe.");
            return;
        }

        musicSource.clip = sound.clip;
        musicSource.volume = sound.volume;
        musicSource.pitch = sound.pitch;
        musicSource.loop = sound.loop;
        musicSource.Play();
    }

    // Detiene la música actual.
    public void StopMusic()
    {
        musicSource.Stop();
    }

    // Pausa la música actual.
    public void PauseMusic()
    {
        musicSource.Pause();
    }

    // Continúa reproduciendo la música pausada.
    public void ResumeMusic()
    {
        musicSource.UnPause();
    }

    // Libera la instancia cuando este SoundManager es destruido.
    private void OnDestroy()
    {
        if (singleton == this)
        {
            singleton = null;
        }
    }
}