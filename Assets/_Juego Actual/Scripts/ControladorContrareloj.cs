
using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

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
    [Tooltip("Al entrar al menu se da por terminada la partida en curso; la siguiente empieza con el tiempo completo.")]
    [SerializeField] private string escenaMenu = "menu";

    [Header("Interfaz")]
    [SerializeField] private TMP_Text textoTiempo;

    private float tiempoRestante, tiempoRestanteEntreEscenas;
    private bool contando = false;
    private bool terminado = false;

    // Lo que se ve del cronometro (estilo, latido, efectos al ganar o perder tiempo)
    private CronometroVivo cronometroVivo;

    /// <summary>
    /// Cada vez que el tiempo sube o baja por algo del juego (no por el paso del tiempo): positivo al
    /// ganar segundos, negativo al perderlos. El bool dice si hay que mostrar el texto "+X"/"-X"
    /// (false cuando quien lo da ya lo mostro, como el "+3" que vuela desde un enemigo).
    /// CronometroVivo lo usa para sus efectos.
    /// </summary>
    public static event Action<float, bool> AlCambiarTiempo;

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

    // Resta tiempo por fallar el ritmo (lo llama BeatActionJudge) y lo muestra en el cronometro
    public void PenalizarTiempo(float restar)
    {
        // Con el cronometro detenido (nivel ganado o perdido) no hay tiempo que perder
        if (!contando || terminado || restar <= 0f)
            return;

        // Si llega a cero, Update se encarga de Perder() en el siguiente fotograma
        float antes = tiempoRestante;
        tiempoRestante = Mathf.Max(0f, tiempoRestante - restar);

        ActualizarTexto();
        AlCambiarTiempo?.Invoke(tiempoRestante - antes, true);
    }

    // Suma tiempo como recompensa (enemigos derrotados, ver RecompensasRitmo) y lo muestra en el cronometro.
    // Con mostrarTexto en false el cronometro reacciona sin su propio "+X".
    public void AgregarTiempo(float sumar, bool mostrarTexto = true)
    {
        if (!contando || terminado || sumar <= 0f)
            return;

        tiempoRestante += sumar;

        ActualizarTexto();
        AlCambiarTiempo?.Invoke(sumar, mostrarTexto);
    }

    // Detiene el cronometro sin terminar la partida (pantallas de ganar/perder del nivel).
    // Se reanuda al cargar el siguiente nivel o al reiniciar la escena.
    public void PausarContrarreloj()
    {
        contando = false;
    }

    public void GuardarTiempoEntreEscena() {
        tiempoRestanteEntreEscenas = Mathf.CeilToInt(tiempoRestante);

        minutosEntreEscena = Mathf.FloorToInt(tiempoRestanteEntreEscenas / 60);
        segundosEntreEscena = Mathf.FloorToInt(tiempoRestanteEntreEscenas % 60);
    }

    public void ReiniciarContrarReloj()
    {
        tiempoRestante = minutos * 60f + segundos;

        // Partida nueva: el tiempo guardado para reintentar un nivel tambien vuelve al inicial
        tiempoRestanteEntreEscenas = tiempoRestante;
        minutosEntreEscena = minutos;
        segundosEntreEscena = segundos;

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
        // Con CronometroVivo el texto lo escribe el (con los numeros rodando y decimas al final)
        if (textoTiempo == null || cronometroVivo != null)
            return;

        textoTiempo.text = CronometroVivo.Formatear(tiempoRestante, false);
    }

    void EscenaCargada(Scene escena, LoadSceneMode modo)
    {
        BuscarTextoTiempo();
        ActualizarTexto();

        // No contar en las escenas finales ni en el menu: la partida en curso termina ahi
        if (escena.name == escenaVictoria ||
            escena.name == escenaDerrota ||
            escena.name == escenaMenu)
        {
            contando = false;
            terminado = true;
        }
        // Volver a jugar despues de ganar o perder: partida nueva con el tiempo completo.
        // Sin esto el reloj seguia "terminado" y quedaban congelados el tiempo, las
        // penalizaciones y el dano.
        else if (terminado)
        {
            ReiniciarContrarReloj();
        }
        // En un nivel nuevo se reanuda lo que pauso la pantalla de ganar del anterior
        else
        {
            contando = true;
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

        cronometroVivo = null;
        if (textoTiempo != null && !textoTiempo.TryGetComponent(out cronometroVivo))
            cronometroVivo = textoTiempo.gameObject.AddComponent<CronometroVivo>();
    }

    public void OnDestroy()
    {
        // Sin desuscribirse, un reloj destruido (Scenes.FinDelJuego) seguia recibiendo
        // EscenaCargada y escribiendo en el texto del cronometro de la partida siguiente
        if (Instancia == this)
        {
            SceneManager.sceneLoaded -= EscenaCargada;
            Instancia = null;
        }

        Destroy(gameObject);
    }
}
