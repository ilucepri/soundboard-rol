using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using Soundboard.Models;

namespace Soundboard.Services;

/// <summary>
/// Persistencia en %APPDATA%\SoundboardRol. Dos ficheros JSON sueltos: los perfiles y las
/// preferencias. Legibles y editables a mano a propósito, por si alguna vez quieres tocarlos.
/// </summary>
public sealed class ProfileStore
{
    static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        // Sin esto los acentos y los emoji salen como \uXXXX y el fichero es ilegible.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public string RootPath { get; }

    string ProfilesPath => Path.Combine(RootPath, "profiles.json");
    string SettingsPath => Path.Combine(RootPath, "settings.json");

    public ProfileStore(string? rootPath = null)
    {
        // SOUNDBOARD_DATA_DIR permite llevarse la configuración en un pendrive, o tener un juego de
        // perfiles aparte para pruebas sin pisar el de verdad.
        RootPath = rootPath
            ?? Environment.GetEnvironmentVariable("SOUNDBOARD_DATA_DIR")
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SoundboardRol");
        Directory.CreateDirectory(RootPath);
    }

    public List<Profile> LoadProfiles()
    {
        var profiles = Read<List<Profile>>(ProfilesPath);
        return profiles is { Count: > 0 } ? profiles : CreateStarterProfiles();
    }

    public void SaveProfiles(IEnumerable<Profile> profiles) => Write(ProfilesPath, profiles.ToList());

    /// <summary>False la primera vez que se abre la app: sirve para proponer salidas por defecto.</summary>
    public bool HasSavedSettings => File.Exists(SettingsPath);

    public AppSettings LoadSettings() => Read<AppSettings>(SettingsPath) ?? new AppSettings();

    public void SaveSettings(AppSettings settings) => Write(SettingsPath, settings);

    static List<Profile> CreateStarterProfiles() =>
    [
        new() { Name = "Bardo", Icon = "🎵" },
        new() { Name = "Mago", Icon = "✨" },
        new() { Name = "Ambiente", Icon = "🏰" }
    ];

    static T? Read<T>(string path) where T : class
    {
        try
        {
            if (!File.Exists(path)) return null;
            return JsonDeserialize<T>(File.ReadAllText(path));
        }
        catch (Exception)
        {
            // Fichero corrupto o a medio escribir: mejor arrancar en blanco que no arrancar.
            return null;
        }
    }

    static T? JsonDeserialize<T>(string text) where T : class =>
        string.IsNullOrWhiteSpace(text) ? null : JsonSerializer.Deserialize<T>(text, Json);

    static void Write<T>(string path, T value)
    {
        // Escritura en dos pasos: si se corta la luz a medias, el fichero bueno sigue intacto.
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(value, Json));
        File.Move(temp, path, overwrite: true);
    }
}
