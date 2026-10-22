
using System.Collections;
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

    [Header("Interfaz")]
    [SerializeField] private TMP_Text textoTiempo;

    [Header("Penalizacion (fallar el ritmo)")]
    [Tooltip("Color al que destella el cronometro, y color del texto flotante, al perder tiempo.")]
    [SerializeField] private Color colorPenalizacion = new Color(1f, 0.22f, 0.18f, 1f);

    [Tooltip("Segundos que dura el destello, el golpe de escala y el temblor del cronometro.")]
    [Min(0.05f)]
    [SerializeField] private float duracionPenalizacion = 0.6f;

    [Tooltip("Cuanto crece el cronometro en el golpe (1 = nada).")]
    [Range(1f, 2f)]
    [SerializeField] private float escalaGolpePenalizacion = 1.3f;

    [Tooltip("Amplitud del temblor del cronometro, en pixeles de UI.")]
    [Range(0f, 30f)]
    [SerializeField] private float temblorPenalizacion = 6f;

    [Tooltip("Muestra un texto flotante con los segundos perdidos (p. ej. \"-5\") junto al cronometro.")]
    [SerializeField] private bool mostrarTextoPenalizacion = true;

    [Tooltip("Donde aparece el texto flotante, relativo al cronometro (pixeles de UI).")]
    [SerializeField] private Vector2 desplazamientoTextoPenalizacion = new Vector2(0f, -40f);

    [Tooltip("Cuanto se desliza el texto flotante mientras se desvanece (pixeles de UI).")]
    [SerializeField] private Vector2 recorridoTextoPenalizacion = new Vector2(0f, -30f);

    [Tooltip("Segundos que tarda el texto flotante en desvanecerse.")]
    [Min(0.1f)]
    [SerializeField] private float duracionTextoPenalizacion = 1.2f;

    private float tiempoRestante, tiempoRestanteEntreEscenas;
    private bool contando = false;
    private bool terminado = false;

    // Estado del cronometro antes del destello, para devolverlo intacto al terminar
    private Coroutine animacionPenalizacion;
    private TMP_Text textoAnimado;
    private Color colorBaseTexto;
    private Vector3 escalaBaseTexto;
    private Vector2 posicionBaseTexto;

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
        if (terminado || restar <= 0f)
            return;

        // Si llega a cero, Update se encarga de Perder() en el siguiente fotograma
        tiempoRestante = Mathf.Max(0f, tiempoRestante - restar);

        ActualizarTexto();
        MostrarPenalizacion(restar);
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

    // Destello rojo, golpe de escala y temblor del cronometro, mas un texto flotante "-X"
    void MostrarPenalizacion(float segundosPerdidos)
    {
        // Si llega otra penalizacion a mitad del destello, se parte del estado original
        RestaurarTextoAnimado();

        if (textoTiempo == null)
            return;

        if (mostrarTextoPenalizacion)
            StartCoroutine(AnimarTextoFlotante(textoTiempo, segundosPerdidos));

        textoAnimado = textoTiempo;
        colorBaseTexto = textoAnimado.color;
        escalaBaseTexto = textoAnimado.rectTransform.localScale;
        posicionBaseTexto = textoAnimado.rectTransform.anchoredPosition;

        animacionPenalizacion = StartCoroutine(AnimarCronometroPenalizado());
    }

    IEnumerator AnimarCronometroPenalizado()
    {
        RectTransform rect = textoAnimado.rectTransform;
        float tiempo = 0f;

        while (tiempo < duracionPenalizacion)
        {
            // La escena cambio y el texto ya no existe
            if (textoAnimado == null)
                break;

            tiempo += Time.unscaledDeltaTime;

            // 1 al recibir la penalizacion, 0 al terminar
            float fuerza = 1f - Mathf.Clamp01(tiempo / duracionPenalizacion);

            // El rojo se sostiene un poco antes de apagarse; el golpe y el temblor caen rapido
            textoAnimado.color = Color.Lerp(colorBaseTexto, colorPenalizacion, Mathf.Sqrt(fuerza));
            rect.localScale = escalaBaseTexto * (1f + (escalaGolpePenalizacion - 1f) * fuerza * fuerza);
            rect.anchoredPosition = posicionBaseTexto + Random.insideUnitCircle * (temblorPenalizacion * fuerza);

            yield return null;
        }

        animacionPenalizacion = null;
        RestaurarTextoAnimado();
    }

    void RestaurarTextoAnimado()
    {
        if (animacionPenalizacion != null)
        {
            StopCoroutine(animacionPenalizacion);
            animacionPenalizacion = null;
        }

        if (textoAnimado != null)
        {
            textoAnimado.color = colorBaseTexto;
            textoAnimado.rectTransform.localScale = escalaBaseTexto;
            textoAnimado.rectTransform.anchoredPosition = posicionBaseTexto;
        }

        textoAnimado = null;
    }

    // Copia del cronometro con los segundos perdidos, que se desliza y se desvanece
    IEnumerator AnimarTextoFlotante(TMP_Text origen, float segundosPerdidos)
    {
        TMP_Text flotante = Instantiate(origen, origen.transform.parent);
        flotante.name = "PenalizacionContrarreloj"; // para que BuscarTextoTiempo no la confunda
        flotante.text = $"-{segundosPerdidos:0.#}";
        flotante.raycastTarget = false;

        RectTransform rect = flotante.rectTransform;
        Vector2 inicio = origen.rectTransform.anchoredPosition + desplazamientoTextoPenalizacion;
        float tiempo = 0f;

        while (tiempo < duracionTextoPenalizacion)
        {
            // La escena cambio y la copia ya no existe
            if (flotante == null)
                yield break;

            tiempo += Time.unscaledDeltaTime;
            float progreso = Mathf.Clamp01(tiempo / duracionTextoPenalizacion);

            // Arranca rapido y frena; se mantiene visible y se apaga al final
            float deslizamiento = 1f - (1f - progreso) * (1f - progreso);
            rect.anchoredPosition = inicio + recorridoTextoPenalizacion * deslizamiento;

            Color color = colorPenalizacion;
            color.a *= 1f - progreso * progreso;
            flotante.color = color;

            yield return null;
        }

        if (flotante != null)
            Destroy(flotante.gameObject);
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
