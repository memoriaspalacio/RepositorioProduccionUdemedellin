using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Las filas del menu de graficos. Cada fila lee y escribe un campo de AjustesGraficos a
/// traves de GestorGraficos, que lo aplica al momento. Para agregar un ajuste nuevo basta
/// con su campo en AjustesGraficos, su aplicacion en GestorGraficos y una fila aqui.
/// </summary>
public static class OpcionesGraficas
{
    static readonly string[] Niveles = { "Alto", "Medio", "Bajo", "Muy bajo" };

    /// <summary>Nombre corto de la calidad en uso, para botones: "Alto" o "Personalizado".</summary>
    public static string NombreCalidad(AjustesGraficos ajustes)
    {
        return ajustes.CoincideConPreset() ? Niveles[(int)ajustes.nivel] : "Personalizado";
    }

    public static List<OpcionMenu> Crear()
    {
        OpcionMenu calidad = OpcionMenu.DeEnum("Calidad general",
            "Cambia todos los ajustes de abajo a la vez. Alto es el aspecto original del juego; Muy bajo es para equipos y móviles modestos.",
            Niveles,
            () => GestorGraficos.Ajustes.nivel,
            GestorGraficos.UsarNivel, true);
        calidad.textoActual = () =>
        {
            AjustesGraficos ajustes = GestorGraficos.Ajustes;
            string nombre = Niveles[(int)ajustes.nivel];
            return ajustes.CoincideConPreset() ? nombre : "Personalizado (" + nombre + ")";
        };

        OpcionMenu sombras = OpcionMenu.DeEnum("Sombras",
            "Sombras en tiempo real de personajes y objetos. Las sombras horneadas del escenario se ven siempre.",
            new[] { "Altas", "Medias", "Bajas", "Apagadas" },
            () => GestorGraficos.Ajustes.sombras,
            valor => GestorGraficos.Modificar(a => a.sombras = valor), true);
        sombras.disponible = () => GestorGraficos.SombrasDisponibles;
        sombras.textoNoDisponible = "Apagadas (Muy bajo)";

        OpcionMenu oclusion = OpcionMenu.Interruptor("Oclusión ambiental",
            "Oscurece rincones y uniones entre objetos (SSAO). Es de lo más costoso en móviles. Solo existe en calidad Alta y Media.",
            () => GestorGraficos.Ajustes.oclusionAmbiental,
            valor => GestorGraficos.Modificar(a => a.oclusionAmbiental = valor));
        oclusion.disponible = () => GestorGraficos.OclusionDisponible;
        oclusion.textoNoDisponible = "Solo en Alto y Medio";

        return new List<OpcionMenu>
        {
            OpcionMenu.Leyenda("< Más rendimiento", "Más calidad >"),
            calidad,

            OpcionMenu.Seccion("Rendimiento"),
            OpcionMenu.DeEnum("Resolución",
                "Resolución interna del 3D; la interfaz siempre se ve nítida. Es el ajuste que más rinde en móviles y gráficas integradas.",
                new[] { "100 %", "85 %", "70 %", "55 %" },
                () => GestorGraficos.Ajustes.resolucion,
                valor => GestorGraficos.Modificar(a => a.resolucion = valor), true),
            OpcionMenu.DeEnum("FPS máximos",
                "Cuantos más FPS, más trabaja la gráfica: en un monitor de 144 Hz, Pantalla consume más del doble que 60. Sin límite la deja siempre al 100 %, solo sirve para medir.",
                new[] { "30", "60", "120", "Pantalla", "Sin límite" },
                () => GestorGraficos.Ajustes.fps,
                valor => GestorGraficos.Modificar(a => a.fps = valor)),
            OpcionMenu.DeEnum("Texturas",
                "Resolución de las texturas de escenarios y personajes. Bajarla libera memoria de video; la interfaz no cambia.",
                new[] { "Completas", "Medias", "Bajas", "Muy bajas" },
                () => GestorGraficos.Ajustes.texturas,
                valor => GestorGraficos.Modificar(a => a.texturas = valor), true),

            OpcionMenu.Seccion("Iluminación"),
            sombras,
            oclusion,
            OpcionMenu.DeEnum("Luces dinámicas",
                "Luces que se mueven o parpadean: luciérnagas, el fuego de las muertes, el destello del indicador de ritmo y el contorno de los personajes.",
                new[] { "Todas", "Reducidas", "Mínimas" },
                () => GestorGraficos.Ajustes.luces,
                valor => GestorGraficos.Modificar(a => a.luces = valor), true),

            OpcionMenu.Seccion("Efectos"),
            OpcionMenu.DeEnum("Post proceso",
                "Completo: bloom, grano, viñeta y color. Ligero: bloom más barato. Mínimo: sin bloom ni grano, conserva el color. Apagado: nada (cambia bastante el aspecto).",
                new[] { "Completo", "Ligero", "Mínimo", "Apagado" },
                () => GestorGraficos.Ajustes.postProceso,
                valor => GestorGraficos.Modificar(a => a.postProceso = valor), true),
            OpcionMenu.DeEnum("Partículas",
                "Niebla, polvo, luciérnagas y efectos de muerte. Apagadas quita la atmósfera, pero las muertes se siguen viendo.",
                new[] { "Completas", "Reducidas", "Mínimas", "Apagadas" },
                () => GestorGraficos.Ajustes.particulas,
                valor => GestorGraficos.Modificar(a => a.particulas = valor), true),
            OpcionMenu.Interruptor("Haces de luz",
                "Rayos de luz que entran por las ventanas y caen desde el cielo.",
                () => GestorGraficos.Ajustes.hacesDeLuz,
                valor => GestorGraficos.Modificar(a => a.hacesDeLuz = valor)),

            OpcionMenu.Seccion("Herramientas"),
            OpcionMenu.Interruptor("Mostrar FPS",
                "Contador de FPS y milisegundos por fotograma, para medir lo que cuesta cada ajuste en este equipo.",
                () => GestorGraficos.Ajustes.mostrarFps,
                valor => GestorGraficos.Modificar(a => a.mostrarFps = valor)),
        };
    }

    /// <summary>Linea de estado bajo el titulo del menu.</summary>
    public static string Estado()
    {
        AjustesGraficos ajustes = GestorGraficos.Ajustes;
        int ancho = Mathf.RoundToInt(Screen.width * ajustes.FactorResolucion);
        int alto = Mathf.RoundToInt(Screen.height * ajustes.FactorResolucion);
        return "Nivel de Unity: " + GestorGraficos.NombreNivelActivo
            + "   ·   3D a " + ancho + "×" + alto
            + " de " + Screen.width + "×" + Screen.height;
    }
}
