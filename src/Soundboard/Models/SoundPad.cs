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
}
