
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using static UnityEngine.Rendering.DebugUI;

public class ControladorContrarreloj : MonoBehaviour
{
    public static ControladorContrarreloj Instancia;

    [Header("Tiempo inicial")]
    [Min(0)]
    [SerializeField] private int minutos = 5;

    [Range(0, 59)]
    [SerializeField] private int segundos = 0;

    [Header("Tiempo guardado entre escenas. ")]
    [Min(0)]
    [SerializeField] private int minutosEntreEscena;

    [Range(0, 59)]
    [SerializeField] private int segundosEntreEscena;

    [Header("Escenas")]
    [SerializeField] private string escenaDerrota = "Perder";
    [SerializeField] private string escenaVictoria = "Victoria";

    [Header("Interfaz")]
    [SerializeField] private TMP_Text textoTiempo;

    private float tiempoRestante, tiempoRestanteEntreEscenas;
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

        tiempoRestanteEntreEscenas = tiempoRestante;

        minutosEntreEscena = minutos;
        segundosEntreEscena = segundos;

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

    public void GuardarTiempoEntreEscena() {
        tiempoRestanteEntreEscenas = Mathf.CeilToInt(tiempoRestante);

        minutosEntreEscena = Mathf.FloorToInt(tiempoRestanteEntreEscenas / 60);
        segundosEntreEscena = Mathf.FloorToInt(tiempoRestanteEntreEscenas % 60);
    }

    public void ReiniciarContrarReloj()
    {
        tiempoRestante = minutos * 60f + segundos;

        terminado = false;
        contando = true;

        BuscarTextoTiempo();
        ActualizarTexto();
    }

    public void ReiniciarContrarRelojAlInicioDeEscena() {
        float diff = tiempoRestanteEntreEscenas - tiempoRestante;
        tiempoRestanteEntreEscenas -= diff/2;
        tiempoRestante = tiempoRestanteEntreEscenas;

        // tiempoRestante = tiempoRestante - (minutosEntreEscena * 60f) / 2 + (segundosEntreEscena) / 2;

        terminado = false;
        contando = true;

        BuscarTextoTiempo();
        ActualizarTexto();
    }


    // Reinicia el tiempo y carga el nivel inicial
    public void ReiniciarJuego(string nombreEscena)
    {
        ReiniciarContrarReloj();
        Time.timeScale = 1f;
        
    }

    void ActualizarTexto()
    {
        if (textoTiempo == null)
            return;

        int totalSegundos = Mathf.CeilToInt(tiempoRestante);

        int minutosActuales = totalSegundos / 60;
        int segundosActuales = totalSegundos % 60;

        textoTiempo.text =
            $"{minutosActuales:00}:{segundosActuales:00}";
    }

    void EscenaCargada(Scene escena, LoadSceneMode modo)
    {
        BuscarTextoTiempo();
        ActualizarTexto();

        // No contar en las escenas finales
        if (escena.name == escenaVictoria ||
            escena.name == escenaDerrota)
        {
            contando = false;
            terminado = true;
        }
    }

    void BuscarTextoTiempo()
    {
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

    public void OnDestroy()
    {
        /*
        if (Instancia == this)
        {
            SceneManager.sceneLoaded -= EscenaCargada;
            Instancia = null;
        }
        */

        Destroy(gameObject);
    }
}
