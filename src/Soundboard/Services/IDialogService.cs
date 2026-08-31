namespace Soundboard.Services;

/// <summary>Lo que el ViewModel necesita preguntarle al usuario, sin depender de WPF.</summary>
public interface IDialogService
{
    /// <summary>Pide un texto. Devuelve null si se cancela.</summary>
    string? AskText(string title, string label, string initialValue);

    /// <summary>Selector de ficheros de audio. Devuelve null si se cancela.</summary>
    string[]? AskAudioFiles();

    bool Confirm(string title, string message);
}
