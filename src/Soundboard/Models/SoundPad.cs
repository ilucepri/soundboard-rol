namespace Soundboard.Models;

/// <summary>Un sonido dentro de un perfil: el fichero, cómo se llama y cómo suena.</summary>
public sealed class SoundPad
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "";

    /// <summary>Ruta absoluta al fichero de audio.</summary>
    public string FilePath { get; set; } = "";

    /// <summary>Ganancia del pad, 0..1. Se multiplica por el volumen maestro de cada salida.</summary>
    public double Volume { get; set; } = 0.8;

    /// <summary>Si se repite hasta que lo pares. Pensado para música de ambiente.</summary>
    public bool Loop { get; set; }

    /// <summary>Color del pad en hexadecimal (#RRGGBB).</summary>
    public string Color { get; set; } = "#3F4A63";

    /// <summary>
    /// Recorte, como fracción del fichero (0..1). Sólo suena el trozo entre los dos.
    /// Los valores por defecto son el fichero entero, así que los perfiles antiguos siguen valiendo.
    /// </summary>
    public double TrimStart { get; set; }

    public double TrimEnd { get; set; } = 1;

    /// <summary>Tecla que dispara el pad con la ventana en primer plano. Vacío = sin atajo.</summary>
    public string Shortcut { get; set; } = "";

    /// <summary>
    /// Duración del fichero en segundos, cacheada al descodificarlo. La biblioteca la muestra sin
    /// tener que abrir cada fichero. 0 = todavía no se ha medido.
    /// </summary>
    public double DurationSeconds { get; set; }
}
