using System.Collections.Generic;

/// <summary>
/// El menu de filtros de color: un MenuOpciones con el filtro y su intensidad. Se abre con F2,
/// con la fila "Filtros" de los paneles de opciones (FilaGraficosOpciones), con el boton
/// "Filtros de color" del menu de graficos o desde cualquier boton con BotonAbrirFiltros.
/// </summary>
public static class MenuFiltros
{
    static readonly string[] Nombres = { "Ninguno", "Óleo barroco", "Vitral nocturno", "Juicio final", "Noir" };

    static readonly string[] Descripciones =
    {
        "Los colores originales de cada escena.",
        "Óleo barroco: claroscuro de Caravaggio. Sombras hondas y cálidas, luces doradas y color contenido, como un barniz viejo.",
        "Vitral nocturno: luna fría atravesando vitrales. Sombras azules y luces violeta y magenta.",
        "Juicio final: el apocalipsis. Sombras carmesí, luces de fuego y contraste duro.",
        "Noir: blanco y negro de cine clásico. Negros profundos, luces limpias, viñeta y grano de película."
    };

    static MenuOpciones abierto;

    public static bool EstaAbierto => abierto != null;

    /// <summary>Nombre corto del filtro en uso, para botones.</summary>
    public static string NombreActual => Nombres[(int)GestorFiltros.Filtro];

    public static void Abrir()
    {
        if (abierto != null)
            return;

        MenuGraficos.Cerrar();

        abierto = MenuOpciones.Crear("Filtros de color", Opciones(), Estado,
            ("Gráficos", () =>
            {
                Cerrar();
                MenuGraficos.Abrir();
            }));

        GestorFiltros.AlCambiar += Refrescar;
        GestorGraficos.AlCambiar += Refrescar;
        abierto.AlCerrar += () =>
        {
            GestorFiltros.AlCambiar -= Refrescar;
            GestorGraficos.AlCambiar -= Refrescar;
            abierto = null;
        };
    }

    public static void Cerrar()
    {
        if (abierto != null)
            abierto.Cerrar();
    }

    public static void Alternar()
    {
        if (EstaAbierto)
            Cerrar();
        else
            Abrir();
    }

    static void Refrescar()
    {
        if (abierto != null)
            abierto.Refrescar();
    }

    static List<OpcionMenu> Opciones()
    {
        OpcionMenu filtro = OpcionMenu.DeEnum("Filtro",
            "Cambia el tono de todo el juego. Se ve detrás de este menú mientras eliges.",
            Nombres,
            () => GestorFiltros.Filtro,
            GestorFiltros.Usar);
        filtro.disponible = () => GestorFiltros.Disponible;
        filtro.textoNoDisponible = "Requiere post proceso";

        OpcionMenu intensidad = OpcionMenu.DeEnum("Intensidad",
            "Cuánto se mezcla el filtro con los colores de la escena.",
            new[] { "Suave", "Media", "Completa" },
            () => GestorFiltros.Intensidad,
            GestorFiltros.UsarIntensidad);
        intensidad.disponible = () => GestorFiltros.Disponible && GestorFiltros.Filtro != FiltroColor.Ninguno;
        intensidad.textoNoDisponible = "—";

        VistaPreviaFiltros vista = null;
        OpcionMenu muestra = OpcionMenu.Personalizada("Vista previa", 340f,
            bloque => vista = VistaPreviaFiltros.Crear(bloque),
            () =>
            {
                if (vista != null)
                    vista.Refrescar();
            });

        return new List<OpcionMenu>
        {
            OpcionMenu.Seccion("Tono"),
            filtro,
            intensidad,
            OpcionMenu.Seccion("Así se verá"),
            muestra,
        };
    }

    /// <summary>Linea bajo el titulo: que hace el filtro elegido, o por que no se ve.</summary>
    static string Estado()
    {
        if (!GestorFiltros.Disponible)
            return "Con \"Post proceso: Apagado\" en Gráficos los filtros no se ven.";

        return Descripciones[(int)GestorFiltros.Filtro];
    }
}
