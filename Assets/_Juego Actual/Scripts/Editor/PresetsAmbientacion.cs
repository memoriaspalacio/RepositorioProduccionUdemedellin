using UnityEditor;
using UnityEngine;

public static class PresetsAmbientacion
{
    const string RutaPerfilCementerio = AplicadorAmbientacion.CarpetaRaiz + "/Cementerio/Perfil_Cementerio.asset";
    const string RutaEscenaCementerio = "Assets/_Juego Actual/Scenes/_ProperGameScenes/cementerio.unity";
    const string RutaPerfilIglesia = AplicadorAmbientacion.CarpetaRaiz + "/Iglesia/Perfil_Iglesia.asset";
    const string RutaEscenaIglesia = "Assets/_Juego Actual/Scenes/_ProperGameScenes/iglesia.unity";
    const string CarpetaMateriales = "Assets/_Juego Actual/Arte/Materials";

    [MenuItem("Herramientas/Ambientacion/Cementerio/Aplicar ambientacion", priority = 0)]
    static void AplicarCementerio()
    {
        AplicadorAmbientacion.Aplicar(PerfilCementerio());
    }

    [MenuItem("Herramientas/Ambientacion/Cementerio/Aplicar y hornear", priority = 1)]
    static void AplicarYHornearCementerio()
    {
        if (AplicadorAmbientacion.Aplicar(PerfilCementerio()))
            AplicadorAmbientacion.Hornear();
    }

    [MenuItem("Herramientas/Ambientacion/Cementerio/Seleccionar perfil", priority = 2)]
    static void SeleccionarPerfilCementerio()
    {
        Selection.activeObject = PerfilCementerio();
        EditorGUIUtility.PingObject(Selection.activeObject);
    }

    [MenuItem("Herramientas/Ambientacion/Iglesia/Aplicar ambientacion", priority = 10)]
    static void AplicarIglesia()
    {
        AplicadorAmbientacion.Aplicar(PerfilIglesia());
    }

    [MenuItem("Herramientas/Ambientacion/Iglesia/Aplicar y hornear", priority = 11)]
    static void AplicarYHornearIglesia()
    {
        if (AplicadorAmbientacion.Aplicar(PerfilIglesia()))
            AplicadorAmbientacion.Hornear();
    }

    [MenuItem("Herramientas/Ambientacion/Iglesia/Seleccionar perfil", priority = 12)]
    static void SeleccionarPerfilIglesia()
    {
        Selection.activeObject = PerfilIglesia();
        EditorGUIUtility.PingObject(Selection.activeObject);
    }

    [MenuItem("Herramientas/Ambientacion/Hornear escena activa", priority = 20)]
    static void HornearEscenaActiva()
    {
        AplicadorAmbientacion.Hornear();
    }

    public static PerfilAmbientacion PerfilCementerio()
    {
        PerfilAmbientacion perfil = AssetDatabase.LoadAssetAtPath<PerfilAmbientacion>(RutaPerfilCementerio);
        if (perfil != null)
            return perfil;

        AplicadorAmbientacion.CrearCarpeta(AplicadorAmbientacion.CarpetaRaiz + "/Cementerio");

        perfil = ScriptableObject.CreateInstance<PerfilAmbientacion>();
        perfil.rutaEscena = RutaEscenaCementerio;
        perfil.prefijosLimite = new[] { "valla", "vaya" };
        perfil.lucesAcento = new[]
        {
            new LuzAcento
            {
                prefijoObjeto = "lampara",
                color = new Color(1f, 0.16f, 0.72f),
                intensidad = 6f,
                rango = 8f,
                emision = 2.5f
            },
            new LuzAcento
            {
                prefijoObjeto = "pilar",
                color = new Color(1f, 0.12f, 0.08f),
                intensidad = 2.5f,
                rango = 3.5f,
                alturaRelativa = 1.1f
            }
        };

        AssetDatabase.CreateAsset(perfil, RutaPerfilCementerio);
        AssetDatabase.SaveAssets();
        return perfil;
    }

    public static PerfilAmbientacion PerfilIglesia()
    {
        PerfilAmbientacion perfil = AssetDatabase.LoadAssetAtPath<PerfilAmbientacion>(RutaPerfilIglesia);
        if (perfil != null)
            return perfil;

        AplicadorAmbientacion.CrearCarpeta(AplicadorAmbientacion.CarpetaRaiz + "/Iglesia");

        Material ventanas = AssetDatabase.LoadAssetAtPath<Material>(CarpetaMateriales + "/Ventanas.mat");
        Material fuego = AssetDatabase.LoadAssetAtPath<Material>(CarpetaMateriales + "/Fuego.mat");
        Color calido = new Color(1f, 0.55f, 0.22f);

        perfil = ScriptableObject.CreateInstance<PerfilAmbientacion>();
        perfil.rutaEscena = RutaEscenaIglesia;
        perfil.prefijosLimite = new[] { "Ventanal" };
        perfil.desactivarObjetos = new[] { "Luz Nave", "Luz Altar", "Luz Velas", "Probes Iglesia" };

        perfil.intensidadReflejos = 0.35f;
        perfil.ambienteCielo = new Color(0.06f, 0.08f, 0.13f);
        perfil.ambienteHorizonte = new Color(0.04f, 0.05f, 0.07f);
        perfil.ambienteSuelo = new Color(0.025f, 0.02f, 0.018f);

        perfil.colorLuna = new Color(0.5f, 0.62f, 0.95f);
        perfil.intensidadLuna = 0.35f;
        perfil.elevacionLuna = 65f;
        perfil.azimutLuna = 60f;
        perfil.fuerzaSombras = 0.6f;

        perfil.colorNiebla = new Color(0.045f, 0.06f, 0.085f);
        perfil.densidadNiebla = 0.018f;

        perfil.materialesEmisivos = new[]
        {
            new MaterialEmisivo { material = ventanas, color = new Color(0.55f, 0.72f, 1f) * 1.6f },
            new MaterialEmisivo { material = fuego, color = new Color(1f, 0.5f, 0.15f) * 4f }
        };

        perfil.lucesAcento = new[]
        {
            new LuzAcento
            {
                prefijoObjeto = "Vela",
                color = calido,
                intensidad = 1.2f,
                rango = 3.5f,
                alturaRelativa = 1.05f
            },
            new LuzAcento
            {
                prefijoObjeto = "Mesa",
                color = new Color(1f, 0.5f, 0.18f),
                intensidad = 8f,
                rango = 7f,
                alturaRelativa = 1.4f
            },
            new LuzAcento
            {
                prefijoObjeto = "Pilar",
                excluirPrefijos = new[] { "PilarCurvo" },
                color = calido,
                intensidad = 3f,
                rango = 4.5f,
                alturaRelativa = 0.4f,
                separacionInterior = 0.8f
            }
        };

        perfil.haces = new HacesDeLuz
        {
            activo = true,
            materialVentanas = ventanas,
            color = new Color(0.55f, 0.7f, 1f),
            opacidad = 0.12f,
            largo = 9f,
            inclinacion = 38f,
            apertura = 1.8f,
            crearLuces = true,
            intensidadLuz = 30f,
            rangoLuz = 13f,
            anguloLuz = 50f
        };

        perfil.colorParticulas = new Color(0.4f, 0.48f, 0.62f);
        perfil.alturaNieblaSuelo = 1f;
        perfil.particulasSuelo = 60;
        perfil.opacidadSuelo = 0.18f;
        perfil.particulasExterior = 0;

        AssetDatabase.CreateAsset(perfil, RutaPerfilIglesia);
        AssetDatabase.SaveAssets();
        return perfil;
    }
}
