
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class ControladorContrarreloj : MonoBehaviour
{
    public static ControladorContrarreloj Instancia;

    [Header("Tiempo inicial")]
    [Min(0)]
    [SerializeField] private int minutos = 5;

    [Range(0, 59)]
    [SerializeField] private int segundos = 0;

    [Header("Escenas")]
    [SerializeField] private string escenaDerrota = "Perder";
    [SerializeField] private string escenaVictoria = "Victoria";

    [Header("Interfaz")]
    [SerializeField] private TMP_Text textoTiempo;

    private float tiempoRestante;
    private bool contando = false;
    private bool terminado = false;

    public float TiempoRestante => tiempoRestante;
    public bool EstaContando => contando;
    public bool HaTerminado => terminado;

    void Awake()
    {
        if (Instancia != null && Instancia != this)
        {
            Destroy(gameObject);
            return;
        }

        Instancia = this;
        DontDestroyOnLoad(gameObject);

        tiempoRestante = minutos * 60f + segundos;

        SceneManager.sceneLoaded += EscenaCargada;
    }

    void Start()
    {
        BuscarTextoTiempo();
        ActualizarTexto();
        IniciarContrarreloj();
    }

    void Update()
    {
        if (!contando || terminado)
            return;

        tiempoRestante -= Time.unscaledDeltaTime;

        if (tiempoRestante <= 0f)
        {
            tiempoRestante = 0f;
            ActualizarTexto();
            Perder();
            return;
        }

        ActualizarTexto();
    }

    public void IniciarContrarreloj()
    {
        if (contando || terminado)
            return;

        contando = true;
    }

    public void Ganar()
    {
        if (terminado)
            return;

        terminado = true;
        contando = false;

        SceneManager.LoadScene(escenaVictoria);
    }

    public void Perder()
    {
        if (terminado)
            return;

        terminado = true;
        contando = false;

        SceneManager.LoadScene(escenaDerrota);
    }

    public void ReiniciarContrarreloj()
    {
        tiempoRestante = minutos * 60f + segundos;
        terminado = false;
        contando = true;

        ActualizarTexto();
    }

    void ActualizarTexto()
    {
        if (textoTiempo == null)
            return;

        int totalSegundos =
            Mathf.CeilToInt(tiempoRestante);

        int minutosActuales = totalSegundos / 60;
        int segundosActuales = totalSegundos % 60;

        textoTiempo.text =
            $"{minutosActuales:00}:{segundosActuales:00}";
    }

    void EscenaCargada(Scene escena, LoadSceneMode modo)
    {
        BuscarTextoTiempo();
        ActualizarTexto();
    }

    void BuscarTextoTiempo()
    {
        // Busca el texto del cronometro en la escena actual
        GameObject objetoTexto =
            GameObject.Find("TextoContrarreloj");

        if (objetoTexto != null)
        {
            textoTiempo = objetoTexto.GetComponent<TMP_Text>();
        }
        else
        {
            textoTiempo = null;
        }
    }

    void OnDestroy()
    {
        if (Instancia == this)
        {
            SceneManager.sceneLoaded -= EscenaCargada;
            Instancia = null;
        }
    }
}
