using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[ExecuteAlways]
[DisallowMultipleComponent]
public class LuzPersonajes : MonoBehaviour
{
    public const int CapaPersonajes = 1;
    public const uint MascaraPersonajes = 1u << CapaPersonajes;

    [Header("Luces (solo afectan a los personajes)")]
    public Light relleno;
    public Light contorno;

    [Header("Orientacion respecto a la camara")]
    [Tooltip("Grados hacia abajo de la luz de relleno, que viene desde la camara.")]
    [Range(-30f, 80f)] public float elevacionRelleno = 25f;
    [Tooltip("Giro lateral de la luz de relleno.")]
    [Range(-90f, 90f)] public float giroRelleno = -25f;
    [Tooltip("Grados hacia abajo de la luz de contorno, que viene desde detras de los personajes.")]
    [Range(-30f, 80f)] public float elevacionContorno = 20f;
    [Range(-90f, 90f)] public float giroContorno = 30f;

    [Header("Deteccion de personajes")]
    [Tooltip("Cada cuantos segundos se buscan personajes nuevos (enemigos que aparecen, etc.).")]
    public float intervaloBusqueda = 1f;
    [Tooltip("Objetos que tambien deben resaltar aunque no sean personajes animados.")]
    public GameObject[] objetosExtra = { };

    readonly HashSet<int> marcados = new HashSet<int>();
    float siguienteBusqueda;

    void OnEnable()
    {
        marcados.Clear();
        Buscar();
        Orientar();
    }

    void Update()
    {
        if (!Application.isPlaying || Time.unscaledTime < siguienteBusqueda)
            return;

        siguienteBusqueda = Time.unscaledTime + intervaloBusqueda;
        Buscar();
    }

    void LateUpdate()
    {
        Orientar();
    }

    public void Buscar()
    {
        Animator[] animadores = FindObjectsByType<Animator>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < animadores.Length; i++)
        {
            Animator animador = animadores[i];
            if (marcados.Contains(animador.GetInstanceID()))
                continue;

            if (animador.GetComponentInChildren<SkinnedMeshRenderer>(true) == null)
                continue;

            Marcar(animador.gameObject);
            marcados.Add(animador.GetInstanceID());
        }

        for (int i = 0; i < objetosExtra.Length; i++)
        {
            if (objetosExtra[i] != null)
                Marcar(objetosExtra[i]);
        }
    }

    static void Marcar(GameObject objeto)
    {
        Renderer[] renderers = objeto.GetComponentsInChildren<Renderer>(true);
        for (int r = 0; r < renderers.Length; r++)
        {
            Renderer renderer = renderers[r];
            if (renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer)
                continue;

            if ((renderer.renderingLayerMask & MascaraPersonajes) == 0)
                renderer.renderingLayerMask |= MascaraPersonajes;
        }
    }

    void Orientar()
    {
        Camera camara = Camera.main;
        if (camara == null)
            return;

        Vector3 adelante = camara.transform.forward;
        adelante.y = 0f;
        if (adelante.sqrMagnitude < 0.0001f)
            adelante = camara.transform.up;
        adelante.Normalize();

        if (relleno != null)
            relleno.transform.rotation = Direccion(adelante, elevacionRelleno, giroRelleno);

        if (contorno != null)
            contorno.transform.rotation = Direccion(-adelante, elevacionContorno, giroContorno);
    }

    static Quaternion Direccion(Vector3 horizontal, float elevacion, float giro)
    {
        Vector3 girada = Quaternion.AngleAxis(giro, Vector3.up) * horizontal;
        Vector3 eje = Vector3.Cross(Vector3.up, girada);
        Vector3 direccion = Quaternion.AngleAxis(elevacion, eje) * girada;
        return Quaternion.LookRotation(direccion, Vector3.up);
    }

    public static void ConfigurarLuz(Light luz, Color color, float intensidad)
    {
        luz.type = LightType.Directional;
        luz.color = color;
        luz.intensity = intensidad;
        luz.shadows = LightShadows.None;
        luz.renderingLayerMask = (int)MascaraPersonajes;
        UniversalAdditionalLightData datos = luz.GetUniversalAdditionalLightData();
        if (datos != null)
            datos.renderingLayers = MascaraPersonajes;
    }
}
