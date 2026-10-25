using System;
using UnityEngine;

/// <summary>
/// Niveles de calidad. Alto es el aspecto original del juego (el nivel "PC" de Quality
/// Settings, sin tocar); los demas tienen su propio nivel y su propio asset de URP.
/// </summary>
public enum NivelGrafico
{
    Alto,
    Medio,
    Bajo,
    MuyBajo
}

public enum EscalaResolucion { Completa, Alta, Media, Baja }
public enum LimiteFps { Fps30, Fps60, Fps120, SinLimite }
public enum CalidadTexturas { Completas, Medias, Bajas, MuyBajas }
public enum CalidadSombras { Altas, Medias, Bajas, Apagadas }
public enum CalidadPostProceso { Completo, Ligero, Minimo, Apagado }
public enum CalidadParticulas { Completas, Reducidas, Minimas, Apagadas }
public enum CalidadLuces { Todas, Reducidas, Minimas }

/// <summary>
/// Todo lo que el jugador puede elegir en el menu de graficos. Se guarda como JSON en
/// PlayerPrefs. Para agregar un ajuste nuevo: un campo aqui, su valor en DePreset, su
/// aplicacion en GestorGraficos y su fila en OpcionesGraficas.
/// </summary>
[Serializable]
public class AjustesGraficos
{
    /// <summary>Sube cuando cambie el significado de algun campo, para descartar lo guardado.</summary>
    public const int VersionActual = 1;

    public int version = VersionActual;

    [Tooltip("Nivel de Quality Settings del que se parte. Decide lo que no se puede cambiar en juego sin romper variantes de shader (SSAO del renderer, sombras suaves, Forward+).")]
    public NivelGrafico nivel = NivelGrafico.Alto;

    public EscalaResolucion resolucion = EscalaResolucion.Completa;
    public LimiteFps fps = LimiteFps.SinLimite;
    public CalidadTexturas texturas = CalidadTexturas.Completas;
    public CalidadSombras sombras = CalidadSombras.Altas;
    public CalidadPostProceso postProceso = CalidadPostProceso.Completo;
    public CalidadParticulas particulas = CalidadParticulas.Completas;
    public CalidadLuces luces = CalidadLuces.Todas;
    public bool hacesDeLuz = true;
    public bool oclusionAmbiental = true;

    [Tooltip("Contador de FPS en pantalla. No forma parte de los niveles.")]
    public bool mostrarFps;

    /// <summary>Los valores de cada nivel. Alto reproduce exactamente el estado original.</summary>
    public static AjustesGraficos DePreset(NivelGrafico nivel)
    {
        AjustesGraficos ajustes = new AjustesGraficos { nivel = nivel };

        switch (nivel)
        {
            case NivelGrafico.Alto:
                ajustes.resolucion = EscalaResolucion.Completa;
                ajustes.fps = LimiteFps.SinLimite;
                ajustes.texturas = CalidadTexturas.Completas;
                ajustes.sombras = CalidadSombras.Altas;
                ajustes.postProceso = CalidadPostProceso.Completo;
                ajustes.particulas = CalidadParticulas.Completas;
                ajustes.luces = CalidadLuces.Todas;
                ajustes.hacesDeLuz = true;
                ajustes.oclusionAmbiental = true;
                break;

            case NivelGrafico.Medio:
                ajustes.resolucion = EscalaResolucion.Completa;
                ajustes.fps = LimiteFps.Fps60;
                ajustes.texturas = CalidadTexturas.Completas;
                ajustes.sombras = CalidadSombras.Medias;
                ajustes.postProceso = CalidadPostProceso.Ligero;
                ajustes.particulas = CalidadParticulas.Reducidas;
                ajustes.luces = CalidadLuces.Todas;
                ajustes.hacesDeLuz = true;
                ajustes.oclusionAmbiental = false;
                break;

            case NivelGrafico.Bajo:
                ajustes.resolucion = EscalaResolucion.Alta;
                ajustes.fps = LimiteFps.Fps60;
                ajustes.texturas = CalidadTexturas.Medias;
                ajustes.sombras = CalidadSombras.Bajas;
                ajustes.postProceso = CalidadPostProceso.Minimo;
                ajustes.particulas = CalidadParticulas.Minimas;
                ajustes.luces = CalidadLuces.Reducidas;
                ajustes.hacesDeLuz = false;
                ajustes.oclusionAmbiental = false;
                break;

            default:
                ajustes.resolucion = EscalaResolucion.Media;
                ajustes.fps = LimiteFps.Fps30;
                ajustes.texturas = CalidadTexturas.Bajas;
                ajustes.sombras = CalidadSombras.Apagadas;
                ajustes.postProceso = CalidadPostProceso.Minimo;
                ajustes.particulas = CalidadParticulas.Apagadas;
                ajustes.luces = CalidadLuces.Minimas;
                ajustes.hacesDeLuz = false;
                ajustes.oclusionAmbiental = false;
                break;
        }

        return ajustes;
    }

    /// <summary>True si todo coincide con su nivel; si no, el menu lo muestra como Personalizado.</summary>
    public bool CoincideConPreset()
    {
        AjustesGraficos preset = DePreset(nivel);
        return resolucion == preset.resolucion
            && fps == preset.fps
            && texturas == preset.texturas
            && sombras == preset.sombras
            && postProceso == preset.postProceso
            && particulas == preset.particulas
            && luces == preset.luces
            && hacesDeLuz == preset.hacesDeLuz
            && oclusionAmbiental == preset.oclusionAmbiental;
    }

    public AjustesGraficos Copia()
    {
        return (AjustesGraficos)MemberwiseClone();
    }

    #region Valores concretos

    /// <summary>Escala de render de URP: la UI se sigue dibujando a resolucion completa.</summary>
    public float FactorResolucion
    {
        get
        {
            switch (resolucion)
            {
                case EscalaResolucion.Alta: return 0.85f;
                case EscalaResolucion.Media: return 0.7f;
                case EscalaResolucion.Baja: return 0.55f;
                default: return 1f;
            }
        }
    }

    /// <summary>Application.targetFrameRate. Sin limite en movil es la frecuencia de la pantalla (si no, Android se queda en 30).</summary>
    public int FotogramasObjetivo
    {
        get
        {
            switch (fps)
            {
                case LimiteFps.Fps30: return 30;
                case LimiteFps.Fps60: return 60;
                case LimiteFps.Fps120: return 120;
                default:
                    if (!Application.isMobilePlatform)
                        return -1;
                    return Mathf.Max(30, Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value));
            }
        }
    }

    /// <summary>Mipmaps que se saltan todas las texturas 3D (las de UI no tienen mipmaps y no cambian).</summary>
    public int LimiteMipmaps => (int)texturas;

    /// <summary>
    /// Multiplica la emision de las particulas de la escena y de los efectos de muerte. Con
    /// Apagadas la atmosfera (niebla, polvo, luciernagas) se apaga entera, pero el resto nunca
    /// llega a cero: las muertes son la confirmacion de que el golpe mato al enemigo.
    /// </summary>
    public float MultiplicadorParticulas
    {
        get
        {
            switch (particulas)
            {
                case CalidadParticulas.Reducidas: return 0.6f;
                case CalidadParticulas.Minimas: return 0.35f;
                case CalidadParticulas.Apagadas: return 0.2f;
                default: return 1f;
            }
        }
    }

    #endregion
}
