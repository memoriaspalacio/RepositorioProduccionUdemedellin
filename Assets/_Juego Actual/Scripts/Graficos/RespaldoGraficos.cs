using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Recuerda el valor original de todo lo que GestorGraficos cambia en assets compartidos
/// (pipelines de URP, renderer features, Quality Settings) y lo devuelve al salir del juego.
/// En el editor esos cambios hechos en Play quedarian guardados en los assets del proyecto
/// (y en git); asi Alto sigue siendo exactamente el estado original. En una build no hace
/// falta, pero tampoco estorba.
/// </summary>
public static class RespaldoGraficos
{
    /// <summary>Lo que el gestor cambia en un asset de URP.</summary>
    public class ValoresPipeline
    {
        public float escala;
        public float distanciaSombras;
        public int resolucionSombras;
        public int cascadas;
    }

    static readonly Dictionary<UniversalRenderPipelineAsset, ValoresPipeline> pipelines = new Dictionary<UniversalRenderPipelineAsset, ValoresPipeline>();
    static readonly Dictionary<ScriptableRendererFeature, bool> efectos = new Dictionary<ScriptableRendererFeature, bool>();
    static readonly Dictionary<int, int> mipmapsPorNivel = new Dictionary<int, int>();
    static int nivelOriginal = -1;

    // Los interruptores de AtmosferaEscena viven en PlayerPrefs, que comparten todas las ramas
    // del proyecto en el mismo equipo: si no se devuelven, una rama sin este menu se quedaria
    // con la atmosfera apagada y sin forma de encenderla
    static bool atmosferaGuardada;
    static bool particulasOriginales, postProcesoOriginal, volumetricosOriginales;

    /// <summary>Hay que llamarlo antes del primer cambio de nivel.</summary>
    public static void GuardarNivel()
    {
        if (nivelOriginal < 0)
            nivelOriginal = QualitySettings.GetQualityLevel();

        if (!atmosferaGuardada)
        {
            atmosferaGuardada = true;
            particulasOriginales = AtmosferaEscena.ParticulasPermitidas;
            postProcesoOriginal = AtmosferaEscena.PostProcesoPermitido;
            volumetricosOriginales = AtmosferaEscena.VolumetricosPermitidos;
        }
    }

    /// <summary>Valores originales del asset; los guarda la primera vez que se piden.</summary>
    public static ValoresPipeline Pipeline(UniversalRenderPipelineAsset asset)
    {
        if (!pipelines.TryGetValue(asset, out ValoresPipeline valores))
        {
            valores = new ValoresPipeline
            {
                escala = asset.renderScale,
                distanciaSombras = asset.shadowDistance,
                resolucionSombras = asset.mainLightShadowmapResolution,
                cascadas = asset.shadowCascadeCount
            };
            pipelines.Add(asset, valores);
        }

        return valores;
    }

    public static void GuardarEfecto(ScriptableRendererFeature efecto)
    {
        if (!efectos.ContainsKey(efecto))
            efectos.Add(efecto, efecto.isActive);
    }

    /// <summary>El limite de mipmaps es de cada nivel de Quality Settings: se guarda el del nivel activo.</summary>
    public static void GuardarMipmaps()
    {
        int nivel = QualitySettings.GetQualityLevel();
        if (!mipmapsPorNivel.ContainsKey(nivel))
            mipmapsPorNivel.Add(nivel, QualitySettings.globalTextureMipmapLimit);
    }

    public static void RestaurarTodo()
    {
        foreach (KeyValuePair<UniversalRenderPipelineAsset, ValoresPipeline> par in pipelines)
        {
            UniversalRenderPipelineAsset asset = par.Key;
            if (asset == null)
                continue;

            asset.renderScale = par.Value.escala;
            asset.shadowDistance = par.Value.distanciaSombras;
            asset.mainLightShadowmapResolution = par.Value.resolucionSombras;
            asset.shadowCascadeCount = par.Value.cascadas;
        }

        foreach (KeyValuePair<ScriptableRendererFeature, bool> par in efectos)
        {
            if (par.Key != null)
                par.Key.SetActive(par.Value);
        }

        foreach (KeyValuePair<int, int> par in mipmapsPorNivel)
        {
            QualitySettings.SetQualityLevel(par.Key, false);
            QualitySettings.globalTextureMipmapLimit = par.Value;
        }

        if (nivelOriginal >= 0)
            QualitySettings.SetQualityLevel(nivelOriginal, false);

        if (atmosferaGuardada)
        {
            if (AtmosferaEscena.ParticulasPermitidas != particulasOriginales)
                AtmosferaEscena.ParticulasPermitidas = particulasOriginales;
            if (AtmosferaEscena.PostProcesoPermitido != postProcesoOriginal)
                AtmosferaEscena.PostProcesoPermitido = postProcesoOriginal;
            if (AtmosferaEscena.VolumetricosPermitidos != volumetricosOriginales)
                AtmosferaEscena.VolumetricosPermitidos = volumetricosOriginales;
        }

        Olvidar();
    }

    public static void Olvidar()
    {
        pipelines.Clear();
        efectos.Clear();
        mipmapsPorNivel.Clear();
        nivelOriginal = -1;
        atmosferaGuardada = false;
    }
}
