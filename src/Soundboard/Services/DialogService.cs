using System.Windows;
using Microsoft.Win32;
using Soundboard.Views;

namespace Soundboard.Services;

public sealed class DialogService(Window owner) : IDialogService
{
    /// <summary>
    /// Lo que NAudio sabe leer de serie en Windows: WAV y MP3 directamente, y el resto a través de
    /// Media Foundation. OGG/Opus se quedan fuera salvo que Windows traiga el códec instalado.
    /// </summary>
    const string AudioFilter =
        "Audio (*.wav;*.mp3;*.m4a;*.aac;*.wma;*.flac;*.aiff)|*.wav;*.mp3;*.m4a;*.aac;*.wma;*.flac;*.aiff;*.aif|" +
        "Todos los ficheros (*.*)|*.*";

    public string? AskText(string title, string label, string initialValue)
    {
        var prompt = new TextPromptWindow(title, label, initialValue) { Owner = owner };
        return prompt.ShowDialog() == true ? prompt.Value : null;
    }

    public string[]? AskAudioFiles()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Añadir sonidos",
            Filter = AudioFilter,
            Multiselect = true
        };
        return dialog.ShowDialog(owner) == true ? dialog.FileNames : null;
    }

    public bool Confirm(string title, string message) =>
        MessageBox.Show(owner, message, title, MessageBoxButton.YesNo, MessageBoxImage.Question)
            == MessageBoxResult.Yes;
}
