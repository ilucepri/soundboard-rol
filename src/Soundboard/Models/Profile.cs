namespace Soundboard.Models;

/// <summary>Un conjunto de sonidos: "Bardo", "Mago", "Taberna"...</summary>
public sealed class Profile
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "Nuevo perfil";

    /// <summary>Emoji que se muestra junto al nombre en la lista lateral.</summary>
    public string Icon { get; set; } = "🎲";

    public List<SoundPad> Pads { get; set; } = [];
}
