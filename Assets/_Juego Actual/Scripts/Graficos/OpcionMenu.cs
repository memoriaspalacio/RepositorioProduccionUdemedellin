using System;

/// <summary>
/// Una fila del menu de opciones: un nombre, una ayuda y una lista de valores entre los que
/// se cambia con las flechas. No sabe nada de graficos: el menu (MenuOpciones) dibuja
/// cualquier lista de estas, asi que sirve igual para audio, controles, etc.
///
/// Los valores van siempre de izquierda a derecha en el mismo sentido en todas las filas (en
/// graficos: de mas rendimiento a mas calidad), sin dar la vuelta: en un extremo la flecha de
/// ese lado se apaga. Asi las flechas tienen un norte claro.
/// </summary>
public class OpcionMenu
{
    public readonly string nombre;
    public readonly string ayuda;
    public readonly string[] valores;

    /// <summary>Solo para la leyenda: lo que significa ir hacia cada lado.</summary>
    public readonly string izquierda;
    public readonly string derecha;

    readonly Func<int> leer;
    readonly Action<int> escribir;

    /// <summary>Si devuelve false la fila se ve apagada y muestra textoNoDisponible.</summary>
    public Func<bool> disponible;
    public string textoNoDisponible = "No disponible";

    /// <summary>Reemplaza el texto del valor actual (por ejemplo "Personalizado").</summary>
    public Func<string> textoActual;

    /// <summary>Las secciones son solo un titulo entre filas.</summary>
    public bool EsSeccion => valores == null && izquierda == null;

    /// <summary>La leyenda es una linea sobre las flechas que dice hacia donde lleva cada una.</summary>
    public bool EsLeyenda => izquierda != null;

    public bool Disponible => disponible == null || disponible();

    /// <summary>Posicion del valor actual, de 0 (el de mas a la izquierda) a valores.Length - 1.</summary>
    public int Indice => leer != null ? leer() : 0;

    public string Texto
    {
        get
        {
            if (!Disponible)
                return textoNoDisponible;
            if (textoActual != null)
                return textoActual();

            int indice = Indice;
            return indice >= 0 && indice < valores.Length ? valores[indice] : "?";
        }
    }

    OpcionMenu(string nombre, string ayuda, string[] valores, Func<int> leer, Action<int> escribir,
        string izquierda = null, string derecha = null)
    {
        this.nombre = nombre;
        this.ayuda = ayuda;
        this.valores = valores;
        this.leer = leer;
        this.escribir = escribir;
        this.izquierda = izquierda;
        this.derecha = derecha;
    }

    /// <summary>True si hay un valor mas hacia ese lado (-1 izquierda, 1 derecha).</summary>
    public bool PuedeIr(int direccion)
    {
        if (EsSeccion || EsLeyenda || !Disponible || valores.Length == 0)
            return false;

        int siguiente = Indice + direccion;
        return siguiente >= 0 && siguiente < valores.Length;
    }

    /// <summary>Avanza (1) o retrocede (-1) un valor. En los extremos no hace nada.</summary>
    public void Cambiar(int direccion)
    {
        if (PuedeIr(direccion))
            escribir(Indice + direccion);
    }

    public static OpcionMenu Seccion(string titulo)
    {
        return new OpcionMenu(titulo, null, null, null, null);
    }

    /// <summary>Linea fija sobre las flechas: que significa ir a la izquierda y a la derecha.</summary>
    public static OpcionMenu Leyenda(string izquierda, string derecha)
    {
        return new OpcionMenu(string.Empty, null, null, null, null, izquierda, derecha);
    }

    public static OpcionMenu Lista(string nombre, string ayuda, string[] valores, Func<int> leer, Action<int> escribir)
    {
        return new OpcionMenu(nombre, ayuda, valores, leer, escribir);
    }

    /// <summary>
    /// Lista sobre un enum. 'valores' va en el orden del enum; con alReves el menu los muestra al
    /// contrario, para enums que van de mas a menos (Alto, Medio, Bajo...) y que la derecha siga
    /// siendo "mas".
    /// </summary>
    public static OpcionMenu DeEnum<T>(string nombre, string ayuda, string[] valores, Func<T> leer, Action<T> escribir, bool alReves = false) where T : struct, System.Enum
    {
        int cantidad = valores.Length;
        string[] mostrados = (string[])valores.Clone();
        if (alReves)
            Array.Reverse(mostrados);

        return new OpcionMenu(nombre, ayuda, mostrados,
            () =>
            {
                int indice = Convert.ToInt32(leer());
                return alReves ? cantidad - 1 - indice : indice;
            },
            indice => escribir((T)System.Enum.ToObject(typeof(T), alReves ? cantidad - 1 - indice : indice)));
    }

    public static OpcionMenu Interruptor(string nombre, string ayuda, Func<bool> leer, Action<bool> escribir)
    {
        return new OpcionMenu(nombre, ayuda, new[] { "No", "Sí" },
            () => leer() ? 1 : 0,
            indice => escribir(indice == 1));
    }
}
