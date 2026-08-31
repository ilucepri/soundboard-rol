using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using Soundboard.ViewModels;
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

    public PadEditResult EditPad(PadViewModel pad)
    {
        var window = new EditSoundWindow(pad) { Owner = owner };
        window.ShowDialog();
        return window.Result;
    }

    public ProfileEdit? EditProfile(string title, string name, string icon)
    {
        var window = new ProfileWindow(title, name, icon) { Owner = owner };
        return window.ShowDialog() == true
            ? new ProfileEdit(window.ProfileName, window.ProfileIcon)
            : null;
    }

    public void RevealInExplorer(string path)
    {
        try
        {
            // Con el fichero delante si existe; si se ha movido, al menos abrimos la carpeta.
            if (File.Exists(path))
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
            else if (Path.GetDirectoryName(path) is { } folder && Directory.Exists(folder))
                Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
        }
        catch (Exception)
        {
            // Abrir el explorador no es crítico: si falla, no pasa nada.
        }
    }
}
