
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

    [Header("Interfaz")]
    [SerializeField] private TextMeshProUGUI textoOmitir;

    private VideoPlayer videoPlayer;
    private float tiempoInicio;
    private bool cinematicaTerminada = false;

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
    }

    void Start()
    {
        tiempoInicio = Time.unscaledTime;
        videoPlayer.Prepare();
    }

    void Update()
    {
        if (cinematicaTerminada || !permitirOmitir)
            return;

        if (Time.unscaledTime - tiempoInicio < tiempoParaOmitir)
            return;

        if (textoOmitir != null)
            textoOmitir.gameObject.SetActive(true);

        if (Input.anyKeyDown)
        {
            TerminarCinematica();
        }
    }

    void VideoPreparado(VideoPlayer vp)
    {
        if (!cinematicaTerminada)
            vp.Play();
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

        videoPlayer.Stop();

        if (textoOmitir != null)
            textoOmitir.gameObject.SetActive(false);

        if (menuPrincipal != null)
            menuPrincipal.SetActive(true);
        else
            Debug.LogWarning("No se asigno el menu principal.");

        gameObject.SetActive(false);
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
