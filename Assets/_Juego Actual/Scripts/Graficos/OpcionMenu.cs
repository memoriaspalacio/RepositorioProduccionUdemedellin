using System;

/// <summary>
/// Una fila del menu de opciones: un nombre, una ayuda y una lista de valores entre los que
/// se cambia con las flechas. No sabe nada de graficos: el menu (MenuOpciones) dibuja
/// cualquier lista de estas, asi que sirve igual para audio, controles, etc.
/// </summary>
public class OpcionMenu
{
    public readonly string nombre;
    public readonly string ayuda;
    public readonly string[] valores;

    readonly Func<int> leer;
    readonly Action<int> escribir;

    /// <summary>Si devuelve false la fila se ve apagada y muestra textoNoDisponible.</summary>
    public Func<bool> disponible;
    public string textoNoDisponible = "No disponible";

    /// <summary>Reemplaza el texto del valor actual (por ejemplo "Personalizado").</summary>
    public Func<string> textoActual;

    /// <summary>Las secciones son solo un titulo entre filas.</summary>
    public bool EsSeccion => valores == null;

    public bool Disponible => disponible == null || disponible();

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

    OpcionMenu(string nombre, string ayuda, string[] valores, Func<int> leer, Action<int> escribir)
    {
        this.nombre = nombre;
        this.ayuda = ayuda;
        this.valores = valores;
        this.leer = leer;
        this.escribir = escribir;
    }

    /// <summary>Avanza (1) o retrocede (-1) un valor, dando la vuelta en los extremos.</summary>
    public void Cambiar(int direccion)
    {
        if (EsSeccion || !Disponible || valores.Length == 0)
            return;

        int siguiente = (Indice + direccion) % valores.Length;
        if (siguiente < 0)
            siguiente += valores.Length;

        escribir(siguiente);
    }

    public static OpcionMenu Seccion(string titulo)
    {
        return new OpcionMenu(titulo, null, null, null, null);
    }

    public static OpcionMenu Lista(string nombre, string ayuda, string[] valores, Func<int> leer, Action<int> escribir)
    {
        return new OpcionMenu(nombre, ayuda, valores, leer, escribir);
    }

    /// <summary>Lista sobre un enum: los valores se escriben en el mismo orden que el enum.</summary>
    public static OpcionMenu DeEnum<T>(string nombre, string ayuda, string[] valores, Func<T> leer, Action<T> escribir) where T : struct, System.Enum
    {
        return new OpcionMenu(nombre, ayuda, valores,
            () => Convert.ToInt32(leer()),
            indice => escribir((T)System.Enum.ToObject(typeof(T), indice)));
    }

    public static OpcionMenu Interruptor(string nombre, string ayuda, Func<bool> leer, Action<bool> escribir)
    {
        return new OpcionMenu(nombre, ayuda, new[] { "No", "Sí" },
            () => leer() ? 1 : 0,
            indice => escribir(indice == 1));
    }
}
