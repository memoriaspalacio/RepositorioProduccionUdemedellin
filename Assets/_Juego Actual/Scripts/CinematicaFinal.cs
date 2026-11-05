using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

/// <summary>
/// Final del juego: al ganar, el corazon funde a blanco, se ve la cinematica final, el corazon
/// funde a negro y se carga la escena de victoria. La crea ControladorContrarreloj.Ganar y se
/// destruye sola al terminar. El video esta en Resources/Transiciones.
/// </summary>
public class CinematicaFinal : MonoBehaviour
{
    private const string RutaVideo = "Transiciones/cinematica_final";
    private const float TiempoParaOmitir = 1f;
    private const float SegundosSilenciar = 2.5f;

    private VideoPlayer video;
    private AudioSource sonido;
    private RenderTexture textura;
    private bool videoTerminado;

    // La que esta en curso: el boton de ganar de Crisantia llama dos veces a TiempoGanar y sin esto
    // salian dos cinematicas a la vez (doble transicion y ningun video).
    private static CinematicaFinal enCurso;

    public static void Reproducir(string escenaSiguiente)
    {
        if (enCurso != null)
            return;

        VideoClip clip = Resources.Load<VideoClip>(RutaVideo);
        if (clip == null)
        {
            Debug.LogWarning("No se encontro la cinematica final en Resources/" + RutaVideo);
            SceneManager.LoadScene(escenaSiguiente);
            return;
        }

        GameObject objeto = new GameObject("CinematicaFinal");
        DontDestroyOnLoad(objeto);
        CinematicaFinal cinematica = objeto.AddComponent<CinematicaFinal>();
        enCurso = cinematica;
        cinematica.StartCoroutine(cinematica.Secuencia(clip, escenaSiguiente));
    }

    IEnumerator Secuencia(VideoClip clip, string escenaSiguiente)
    {
        TransicionCorazon transicion = TransicionCorazon.Instancia;

        // La musica del nivel se apaga mientras el corazon cubre la pantalla
        StartCoroutine(BajarVolumenGeneral());
        yield return transicion.Reproducir(TransicionCorazon.Fundido.Blanco, () => EmpezarVideo(clip));

        float inicio = Time.unscaledTime;
        while (!videoTerminado)
        {
            if (Time.unscaledTime - inicio > TiempoParaOmitir && Input.anyKeyDown)
                videoTerminado = true;
            yield return null;
        }

        StartCoroutine(BajarVolumenVideo());
        yield return transicion.Reproducir(TransicionCorazon.Fundido.Negro, () => IrAEscena(escenaSiguiente));
        Destroy(gameObject);
    }

    IEnumerator EmpezarVideo(VideoClip clip)
    {
        textura = new RenderTexture((int)clip.width, (int)clip.height, 0);

        // El sonido del video ignora la pausa del AudioListener, que calla todo lo del nivel
        sonido = gameObject.AddComponent<AudioSource>();
        sonido.playOnAwake = false;
        sonido.ignoreListenerPause = true;

        video = gameObject.AddComponent<VideoPlayer>();
        video.playOnAwake = false;
        video.isLooping = false;
        video.source = VideoSource.VideoClip;
        video.clip = clip;
        video.renderMode = VideoRenderMode.RenderTexture;
        video.targetTexture = textura;
        video.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
        video.audioOutputMode = VideoAudioOutputMode.AudioSource;
        video.controlledAudioTrackCount = 1;
        video.EnableAudioTrack(0, true);
        video.SetTargetAudioSource(0, sonido);
        video.loopPointReached += vp => videoTerminado = true;
        video.errorReceived += (vp, mensaje) =>
        {
            Debug.LogError("Error de video: " + mensaje);
            videoTerminado = true;
        };

        video.Prepare();
        while (!video.isPrepared && !videoTerminado)
            yield return null;

        AudioListener.pause = true;
        AudioListener.volume = 1f;

        TransicionCorazon.Instancia.MostrarPantalla(textura, (float)clip.width / clip.height);
        video.Play();
    }

    IEnumerator IrAEscena(string escena)
    {
        video.Stop();
        TransicionCorazon.Instancia.MostrarPantalla(null, 1f);
        textura.Release();

        AsyncOperation carga = SceneManager.LoadSceneAsync(escena);
        while (!carga.isDone)
            yield return null;

        AudioListener.pause = false;
        AudioListener.volume = 1f;
    }

    IEnumerator BajarVolumenGeneral()
    {
        for (float t = 0f; t < SegundosSilenciar; t += Time.unscaledDeltaTime)
        {
            AudioListener.volume = 1f - t / SegundosSilenciar;
            yield return null;
        }
        AudioListener.volume = 0f;
    }

    // Si se omite el video, su sonido se apaga mientras el corazon cubre en vez de cortarse
    IEnumerator BajarVolumenVideo()
    {
        float inicial = sonido.volume;
        for (float t = 0f; t < SegundosSilenciar; t += Time.unscaledDeltaTime)
        {
            sonido.volume = inicial * (1f - t / SegundosSilenciar);
            yield return null;
        }
        sonido.volume = 0f;
    }

    void OnDestroy()
    {
        if (enCurso == this)
            enCurso = null;
        if (textura != null)
            Destroy(textura);
    }
}
