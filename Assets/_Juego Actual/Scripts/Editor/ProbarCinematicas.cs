using UnityEditor;
using UnityEngine;

/// <summary>
/// Atajos del editor para ver el final sin jugar la partida entera. Solo funcionan en Play.
/// </summary>
public static class ProbarCinematicas
{
    [MenuItem("Herramientas/Cinematicas/Ganar ahora (cinematica final)")]
    static void GanarAhora()
    {
        if (ControladorContrarreloj.Instancia != null && !ControladorContrarreloj.Instancia.HaTerminado)
            ControladorContrarreloj.Instancia.Ganar();
        else
            CinematicaFinal.Reproducir("Victoria");
    }

    [MenuItem("Herramientas/Cinematicas/Ganar ahora (cinematica final)", true)]
    static bool PuedeGanar()
    {
        return Application.isPlaying;
    }
}
