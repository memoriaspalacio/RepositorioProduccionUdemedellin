using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Menu de pausa de los niveles (el prefab Pausa de Jacob: boton arriba a la izquierda y panel
/// con Continuar, Menu principal y Salir). Se pone solo en cada escena con BeatManager, en su
/// propio Canvas, y se abre con el boton o con Escape.
///
/// Pausar detiene el juego de verdad: Time.timeScale a 0 (enemigos, animaciones), el reloj del
/// ritmo (BeatManager, que va por dspTime), todo el audio, el cronometro (que corre sin escalar)
/// y la entrada del jugador (GridPlayerMovement mira EnPausa).
/// </summary>
[DisallowMultipleComponent]
public class ControladorPausa : MonoBehaviour
{
    private const string RecursoPrefab = "Pausa";
    private const string EscenaMenu = "menu";

    /// <summary>El juego esta en pausa (o saliendo al menu desde ella).</summary>
    public static bool EnPausa { get; private set; }

    private GameObject panel;
    private float escalaAntes = 1f;
    private bool cronometroContaba;
    private bool saliendo;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Iniciar()
    {
        SceneManager.sceneLoaded += (escena, modo) => PonerEnEscena();
        PonerEnEscena(); // la escena con la que arranca el Play
    }

    static void PonerEnEscena()
    {
        // Solo en los niveles: los que tienen pista de ritmo
        if (FindAnyObjectByType<BeatManager>() == null || FindAnyObjectByType<ControladorPausa>() != null)
            return;

        GameObject prefab = Resources.Load<GameObject>(RecursoPrefab);
        if (prefab == null)
        {
            Debug.LogWarning("ControladorPausa: no se encontro Resources/" + RecursoPrefab);
            return;
        }

        GameObject raiz = new GameObject("MenuPausa (Canvas)", typeof(RectTransform));
        Canvas canvas = raiz.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000; // sobre el HUD, debajo de las transiciones y fundidos

        // El prefab esta hecho para el HUD de los niveles: 800x450 a 16:9
        CanvasScaler escalador = raiz.AddComponent<CanvasScaler>();
        escalador.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escalador.referenceResolution = new Vector2(800f, 450f);
        escalador.matchWidthOrHeight = 0.5f;
        raiz.AddComponent<GraphicRaycaster>();

        GameObject pausa = Instantiate(prefab, raiz.transform, false);
        pausa.name = prefab.name;
        RectTransform rect = (RectTransform)pausa.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        raiz.AddComponent<ControladorPausa>().Configurar(pausa);
    }

    void Configurar(GameObject pausa)
    {
        // La raiz del prefab es una imagen invisible del tamano de la pantalla que se tragaba
        // los clics del resto de la interfaz (el boton de siguiente nivel, por ejemplo)
        if (pausa.TryGetComponent(out Image fondoInvisible))
            fondoInvisible.raycastTarget = false;

        Transform menu = Buscar(pausa.transform, "MenuPausa");
        panel = menu != null ? menu.gameObject : null;
        if (panel != null)
            panel.SetActive(false);

        // Los botones traian llamadas que no pausaban, cargaban una escena "Menu" que no existe
        // o apuntaban a nada: se apagan y se conectan aqui
        Conectar(pausa.transform, "BotonPausa", Pausar);
        Conectar(pausa.transform, "Continuar", Reanudar);
        Conectar(pausa.transform, "Menu principal", IrAlMenu);
        Conectar(pausa.transform, "Salir", Salir);
    }

    static void Conectar(Transform raiz, string nombre, UnityAction accion)
    {
        Transform t = Buscar(raiz, nombre);
        if (t == null || !t.TryGetComponent(out Button boton))
        {
            Debug.LogWarning("ControladorPausa: falta el boton " + nombre);
            return;
        }

        for (int i = 0; i < boton.onClick.GetPersistentEventCount(); i++)
            boton.onClick.SetPersistentListenerState(i, UnityEventCallState.Off);

        boton.onClick.AddListener(accion);
    }

    static Transform Buscar(Transform raiz, string nombre)
    {
        foreach (Transform hijo in raiz.GetComponentsInChildren<Transform>(true))
            if (hijo.name == nombre)
                return hijo;
        return null;
    }

    void Update()
    {
        if (saliendo || !Input.GetKeyDown(KeyCode.Escape))
            return;

        if (EnPausa)
            Reanudar();
        else
            Pausar();
    }

    public void Pausar()
    {
        if (EnPausa || saliendo || !SePuedePausar())
            return;

        EnPausa = true;

        escalaAntes = Time.timeScale;
        Time.timeScale = 0f;
        AudioListener.pause = true;

        if (BeatManager.Instance != null)
            BeatManager.Instance.PauseClock();

        ControladorContrarreloj reloj = ControladorContrarreloj.Instancia;
        cronometroContaba = reloj != null && reloj.EstaContando;
        if (cronometroContaba)
            reloj.PausarContrarreloj();

        if (panel != null)
            panel.SetActive(true);
    }

    public void Reanudar()
    {
        if (!EnPausa || saliendo)
            return;

        if (panel != null)
            panel.SetActive(false);

        if (BeatManager.Instance != null)
            BeatManager.Instance.ResumeClock();

        AudioListener.pause = false;
        Time.timeScale = escalaAntes;

        if (cronometroContaba && ControladorContrarreloj.Instancia != null)
            ControladorContrarreloj.Instancia.ReanudarContrarreloj();

        EnPausa = false;
    }

    // Antes de que empiece la musica (el fundido de entrada del nivel) o durante una
    // transicion no hay nada que pausar
    static bool SePuedePausar()
    {
        return !TransicionCorazon.EnCurso &&
               BeatManager.Instance != null && BeatManager.Instance.IsPlaying;
    }

    void IrAlMenu()
    {
        if (!EnPausa || saliendo)
            return;

        // El juego sigue congelado mientras el corazon cubre la pantalla; se suelta al cargar el menu
        saliendo = true;
        TransicionCorazon.Instancia.Reproducir(TransicionCorazon.Fundido.Negro, CargarMenu);
    }

    static IEnumerator CargarMenu()
    {
        AsyncOperation carga = SceneManager.LoadSceneAsync(EscenaMenu);
        while (!carga.isDone)
            yield return null;
    }

    void Salir()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void OnDestroy()
    {
        // Al cambiar de escena desde la pausa (o al salir) el juego no se queda congelado
        if (EnPausa)
        {
            EnPausa = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }
    }
}
