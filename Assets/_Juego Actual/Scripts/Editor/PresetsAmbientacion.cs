using UnityEditor;
using UnityEngine;

public static class PresetsAmbientacion
{
    const string RutaPerfilCementerio = AplicadorAmbientacion.CarpetaRaiz + "/Cementerio/Perfil_Cementerio.asset";
    const string RutaEscenaCementerio = "Assets/_Juego Actual/Scenes/_ProperGameScenes/cementerio.unity";
    const string RutaPerfilIglesia = AplicadorAmbientacion.CarpetaRaiz + "/Iglesia/Perfil_Iglesia.asset";
    const string RutaEscenaIglesia = "Assets/_Juego Actual/Scenes/_ProperGameScenes/iglesia.unity";
    const string CarpetaMateriales = "Assets/_Juego Actual/Arte/Materials";
    const string CarpetaCrisantia = AplicadorAmbientacion.CarpetaRaiz + "/Crisantia";
    const string RutaPerfilCrisantia = CarpetaCrisantia + "/Perfil_Crisantia.asset";
    const string RutaEscenaCrisantia = "Assets/_Juego Actual/Scenes/_ProperGameScenes/crisantia.unity";

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

    [MenuItem("Herramientas/Ambientacion/Crisantia/Aplicar ambientacion", priority = 13)]
    static void AplicarCrisantia()
    {
        AplicadorAmbientacion.Aplicar(PerfilCrisantia());
    }

    [MenuItem("Herramientas/Ambientacion/Crisantia/Aplicar y hornear", priority = 14)]
    static void AplicarYHornearCrisantia()
    {
        if (AplicadorAmbientacion.Aplicar(PerfilCrisantia()))
            AplicadorAmbientacion.Hornear();
    }

    [MenuItem("Herramientas/Ambientacion/Crisantia/Seleccionar perfil", priority = 15)]
    static void SeleccionarPerfilCrisantia()
    {
        Selection.activeObject = PerfilCrisantia();
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

    public static PerfilAmbientacion PerfilCrisantia()
    {
        PerfilAmbientacion perfil = AssetDatabase.LoadAssetAtPath<PerfilAmbientacion>(RutaPerfilCrisantia);
        if (perfil != null)
            return perfil;

        AplicadorAmbientacion.CrearCarpeta(CarpetaCrisantia);

        Color dorado = new Color(1f, 0.8f, 0.5f);
        Color calido = new Color(1f, 0.62f, 0.3f);

        perfil = ScriptableObject.CreateInstance<PerfilAmbientacion>();
        perfil.rutaEscena = RutaEscenaCrisantia;
        perfil.prefijosLimite = new[] { "pared" };
        perfil.objetosEstaticos = new[] { "crisantia", "piso" };
        perfil.modelosLightmap = new[] { "Assets/_Juego Actual/Arte/Models/Mapas/crisantia.fbx" };

        perfil.skybox = CrearSkyboxDorado(CarpetaCrisantia + "/Skybox_Crisantia.mat");
        perfil.intensidadReflejos = 0.6f;
        perfil.ambienteCielo = new Color(0.42f, 0.33f, 0.22f);
        perfil.ambienteHorizonte = new Color(0.26f, 0.2f, 0.13f);
        perfil.ambienteSuelo = new Color(0.1f, 0.075f, 0.05f);

        perfil.colorLuna = dorado;
        perfil.intensidadLuna = 1.5f;
        perfil.elevacionLuna = 52f;
        perfil.azimutLuna = 165f;
        perfil.fuerzaSombras = 0.8f;

        perfil.colorNiebla = new Color(0.42f, 0.32f, 0.2f);
        perfil.densidadNiebla = 0.035f;

        perfil.lucesAcento = new[]
        {
            new LuzAcento
            {
                prefijoObjeto = "lampara",
                color = calido,
                intensidad = 5f,
                rango = 7f,
                alturaRelativa = 0.5f,
                emision = 1.5f
            },
            CuadroIluminado("cuadro"),
            CuadroIluminado("cuadors"),
            CuadroIluminado("cruzpared")
        };

        perfil.haces = new HacesDeLuz
        {
            activo = true,
            modo = ModoHaces.Cielo,
            color = new Color(1f, 0.82f, 0.5f),
            opacidad = 0.16f,
            apertura = 1.4f,
            cantidadCielo = 9,
            alturaCielo = 11f,
            anchoCielo = new Vector2(1.2f, 3f),
            semilla = 112
        };

        perfil.colorParticulas = new Color(0.85f, 0.68f, 0.45f);
        perfil.alturaNieblaSuelo = 1.2f;
        perfil.particulasSuelo = 50;
        perfil.opacidadSuelo = 0.28f;
        perfil.particulasExterior = 40;
        perfil.tamanoExterior = new Vector2(8f, 14f);
        perfil.opacidadExterior = 0.55f;
        perfil.alturaExterior = 5f;
        perfil.radioExterior = 1.25f;
        perfil.particulasPolvo = 160;
        perfil.colorPolvo = new Color(3f, 2.2f, 1.1f);

        perfil.perfilVolumen = AplicadorAmbientacion.CrearPerfilVolumen(CarpetaCrisantia + "/Volumen_Crisantia.asset", new AjustesVolumen
        {
            exposicion = 0.2f,
            contraste = 18f,
            saturacion = -12f,
            filtro = new Color(1f, 0.9f, 0.75f),
            umbralBloom = 0.85f,
            intensidadBloom = 1.5f,
            tinteBloom = new Color(1f, 0.85f, 0.6f),
            sombras = new Color(0.45f, 0.35f, 0.28f),
            luces = new Color(0.8f, 0.65f, 0.45f),
            balance = 10f,
            colorVineta = new Color(0.08f, 0.04f, 0.01f),
            intensidadVineta = 0.42f,
            grano = 0.2f
        });

        AssetDatabase.CreateAsset(perfil, RutaPerfilCrisantia);
        AssetDatabase.SaveAssets();
        return perfil;
    }

    static LuzAcento CuadroIluminado(string prefijo)
    {
        return new LuzAcento
        {
            prefijoObjeto = prefijo,
            color = new Color(1f, 0.86f, 0.62f),
            intensidad = 3f,
            rango = 5f,
            alturaRelativa = 0.8f,
            separacionInterior = 1.5f,
            emision = 0.35f
        };
    }

    static Material CrearSkyboxDorado(string ruta)
    {
        Material skybox = AplicadorAmbientacion.CrearSkybox(ruta);
        skybox.SetColor("_ColorCenit", new Color(0.35f, 0.25f, 0.15f));
        skybox.SetColor("_ColorHorizonte", new Color(0.85f, 0.62f, 0.35f));
        skybox.SetColor("_ColorLuna", new Color(1f, 0.9f, 0.7f));
        skybox.SetFloat("_TamanoLuna", 0.08f);
        skybox.SetFloat("_BrilloLuna", 8f);
        skybox.SetColor("_ColorHalo", new Color(1f, 0.75f, 0.4f));
        skybox.SetFloat("_TamanoHalo", 6f);
        skybox.SetFloat("_BrilloHalo", 1.5f);
        skybox.SetFloat("_CantidadEstrellas", 0f);
        skybox.SetColor("_ColorNubes", new Color(1f, 0.82f, 0.55f));
        skybox.SetColor("_ColorNubesSombra", new Color(0.3f, 0.2f, 0.12f));
        skybox.SetFloat("_CoberturaNubes", 0.7f);
        EditorUtility.SetDirty(skybox);
        return skybox;
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
