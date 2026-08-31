using System.IO;

namespace Soundboard.ViewModels;

/// <summary>Una fila de la biblioteca: un fichero que la app ya conoce por estar en algún perfil.</summary>
public sealed class LibraryEntry(string filePath, TimeSpan duration, IReadOnlyList<string> usedBy)
{
    public string FilePath { get; } = filePath;

    public string FileName { get; } = Path.GetFileName(filePath);

    public string Directory { get; } = Path.GetDirectoryName(filePath) ?? "";

    public string DurationText { get; } =
        duration > TimeSpan.Zero ? $"{(int)duration.TotalMinutes}:{duration.Seconds:00}" : "—";

    public string UsedByText { get; } = usedBy.Count == 0 ? "—" : string.Join(", ", usedBy);
}
