using Soundboard.ViewModels;

namespace Soundboard.Services;

public enum PadEditResult
{
    Cancel,
    Save,
    Remove
}

/// <summary>Nombre e icono de un perfil, tal y como los devuelve su diálogo.</summary>
public sealed record ProfileEdit(string Name, string Icon);

/// <summary>Lo que el ViewModel necesita preguntarle al usuario, sin depender de WPF.</summary>
public interface IDialogService
{
    /// <summary>Selector de ficheros de audio. Devuelve null si se cancela.</summary>
    string[]? AskAudioFiles();

    bool Confirm(string title, string message);

    /// <summary>Editor completo del pad. Aplica los cambios sobre el pad si se guarda.</summary>
    PadEditResult EditPad(PadViewModel pad);

    /// <summary>Alta o edición de perfil. Devuelve null si se cancela.</summary>
    ProfileEdit? EditProfile(string title, string name, string icon);

    /// <summary>Abre el explorador con el fichero seleccionado.</summary>
    void RevealInExplorer(string path);
}
