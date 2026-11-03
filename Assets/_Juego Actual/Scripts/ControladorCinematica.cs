
using System.Collections;
using UnityEngine;
using UnityEngine.Video;
using TMPro;

[RequireComponent(typeof(VideoPlayer))]
public class ControladorCinematica : MonoBehaviour
{
    [Header("Menu Principal")]
    [SerializeField] private GameObject menuPrincipal;

    [Header("Configuracion")]
    [SerializeField] private bool permitirOmitir = true;
    [SerializeField] private float tiempoParaOmitir = 1f;
    [Tooltip("Segundos del fundido desde negro al empezar la cinematica.")]
    [Min(0f)]
    [SerializeField] private float duracionEntrada = 1.2f;

    [Header("Interfaz")]
    [SerializeField] private TextMeshProUGUI textoOmitir;

    [Header("Musica del Menu")]
    [SerializeField] private AudioSource audioMenu;
    [SerializeField] private AudioClip musicaMenu;
    [Range(0f, 1f)]
    [SerializeField] private float volumenMenu = 0.7f;
    [Min(0f)]
    [SerializeField] private float duracionFade = 2f;

    private VideoPlayer videoPlayer;
    private float tiempoInicio;
    private bool cinematicaTerminada = false;

    private bool aumentandoVolumen = false;
    private float tiempoFade = 0f;

    void Awake()
    {
        videoPlayer = GetComponent<VideoPlayer>();

        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = false;

        videoPlayer.prepareCompleted += VideoPreparado;
        videoPlayer.loopPointReached += VideoTerminado;
        videoPlayer.errorReceived += ErrorVideo;

        if (menuPrincipal != null)
            menuPrincipal.SetActive(false);

        if (textoOmitir != null)
            textoOmitir.gameObject.SetActive(false);

        if (audioMenu != null)
        {
            audioMenu.playOnAwake = false;
            audioMenu.loop = true;
            audioMenu.Stop();
        }

        // Negro hasta que el video este listo; de ahi un fundido simple hacia la cinematica
        TransicionCorazon.Instancia.CubrirAlInstante(TransicionCorazon.Fundido.Negro);
    }

    void Start()
    {
        tiempoInicio = Time.unscaledTime;
        videoPlayer.Prepare();
    }

    void Update()
    {
        // Controlar el fade de la musica
        if (aumentandoVolumen)
        {
            ActualizarFade();
        }

        if (cinematicaTerminada || !permitirOmitir)
            return;

        if (Time.unscaledTime - tiempoInicio < tiempoParaOmitir)
            return;

        if (textoOmitir != null)
            textoOmitir.gameObject.SetActive(true);

        // Omitir con teclado, raton o mando
        // compatible con el Input Manager clasico
        if (Input.anyKeyDown)
        {
            TerminarCinematica();
        }
    }

    void VideoPreparado(VideoPlayer vp)
    {
        if (!cinematicaTerminada)
        {
            vp.Play();
            TransicionCorazon.Instancia.Descubrir(duracionEntrada);
        }
    }

    void VideoTerminado(VideoPlayer vp)
    {
        TerminarCinematica();
    }

    void ErrorVideo(VideoPlayer vp, string mensaje)
    {
        Debug.LogError("Error de video: " + mensaje);
        TerminarCinematica();
    }

    void TerminarCinematica()
    {
        if (cinematicaTerminada)
            return;

        cinematicaTerminada = true;

        if (textoOmitir != null)
            textoOmitir.gameObject.SetActive(false);

        // El corazon funde a negro y, con la pantalla cubierta, cambia el video por el menu
        TransicionCorazon.Instancia.Reproducir(TransicionCorazon.Fundido.Negro, MostrarMenu);
    }

    IEnumerator MostrarMenu()
    {
        // Detener y ocultar video
        videoPlayer.Stop();
        videoPlayer.enabled = false;

        // Mostrar menu principal
        if (menuPrincipal != null)
            menuPrincipal.SetActive(true);

        // Reproducir musica del menu
        if (audioMenu != null && musicaMenu != null)
        {
            audioMenu.clip = musicaMenu;
            audioMenu.loop = true;
            audioMenu.volume = 0f;
            audioMenu.Play();

            tiempoFade = 0f;

            if (duracionFade <= 0f)
            {
                audioMenu.volume = volumenMenu;
            }
            else
            {
                aumentandoVolumen = true;
            }
        }

        yield break;
    }

    void ActualizarFade()
    {
        if (audioMenu == null)
        {
            aumentandoVolumen = false;
            return;
        }

        tiempoFade += Time.unscaledDeltaTime;

        float progreso = Mathf.Clamp01(
            tiempoFade / duracionFade
        );

        audioMenu.volume = Mathf.Lerp(
            0f,
            volumenMenu,
            progreso
        );

        if (progreso >= 1f)
        {
            audioMenu.volume = volumenMenu;
            aumentandoVolumen = false;
        }
    }

    void OnDestroy()
    {
        if (videoPlayer != null)
        {
            videoPlayer.prepareCompleted -= VideoPreparado;
            videoPlayer.loopPointReached -= VideoTerminado;
            videoPlayer.errorReceived -= ErrorVideo;
        }
    }
}
