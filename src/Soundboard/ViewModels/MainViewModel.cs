using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Soundboard.Audio;
using Soundboard.Models;
using Soundboard.Services;

namespace Soundboard.ViewModels;

public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    /// <summary>Opción "sin salida" de los desplegables. Id vacío para distinguirla de un dispositivo real.</summary>
    public static readonly AudioDeviceInfo NoDevice = new("", "— ninguna —", false);

    /// <summary>
    /// Cables de audio virtuales, en orden de preferencia. Wave Link publica un dispositivo de salida
    /// por cada canal de su mezclador ("Wave Link SFX", "Wave Link Music"...), así que apuntando la
    /// emisión ahí el sonido entra directamente en ese canal, sin asignar la app a mano. SFX es el
    /// canal pensado justo para esto.
    /// </summary>
    static readonly string[] VirtualCableHints =
        ["wave link sfx", "wave link aux", "wave link", "cable input", "vb-audio", "voicemeeter", "virtual"];

    static readonly string[] PadPalette =
    [
        "#B4654A", "#7C6AA6", "#48788A", "#A2874A",
        "#8A5566", "#4F7A5C", "#5A6C9C", "#9A5D3E"
    ];

    readonly ProfileStore _store;
    readonly AudioDeviceService _deviceService;
    readonly AudioEngine _engine;
    readonly IDialogService _dialogs;
    readonly DispatcherTimer _saveTimer;
    readonly AppSettings _settings;

    bool _suppressDeviceSwitch;

    [ObservableProperty] ProfileViewModel? _selectedProfile;
    [ObservableProperty] AudioDeviceInfo _broadcastDevice = NoDevice;
    [ObservableProperty] AudioDeviceInfo _monitorDevice = NoDevice;
    [ObservableProperty] double _broadcastVolume;
    [ObservableProperty] double _monitorVolume;
    [ObservableProperty] string? _status;

    public MainViewModel(ProfileStore store, AudioDeviceService deviceService, AudioEngine engine, IDialogService dialogs)
    {
        _store = store;
        _deviceService = deviceService;
        _engine = engine;
        _dialogs = dialogs;

        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); SaveProfiles(); };

        Profiles = [];
        Devices = [];

        foreach (var profile in _store.LoadProfiles())
            Profiles.Add(BuildProfile(profile));

        bool firstRun = !_store.HasSavedSettings;
        _settings = _store.LoadSettings();
        _broadcastVolume = _settings.BroadcastVolume;
        _monitorVolume = _settings.MonitorVolume;
        _engine.BroadcastVolume = (float)_broadcastVolume;
        _engine.MonitorVolume = (float)_monitorVolume;

        if (firstRun) ProposeDefaultDevices();
        RefreshDevices();

        SelectedProfile = Profiles.FirstOrDefault(p => p.Model.Id == _settings.LastProfileId)
                          ?? Profiles.FirstOrDefault();
    }

    public ObservableCollection<ProfileViewModel> Profiles { get; }

    public ObservableCollection<AudioDeviceInfo> Devices { get; }

    // ---- Dispositivos ----------------------------------------------------

    [RelayCommand]
    public void RefreshDevices()
    {
        var (broadcastId, monitorId) = (BroadcastDevice.Id, MonitorDevice.Id);

        _suppressDeviceSwitch = true;
        Devices.Clear();
        Devices.Add(NoDevice);
        foreach (var device in _deviceService.GetOutputDevices())
            Devices.Add(device);
        _suppressDeviceSwitch = false;

        // Al arrancar todavía no hay selección: recuperamos la que quedó guardada.
        BroadcastDevice = FindDevice(string.IsNullOrEmpty(broadcastId) ? _settings.BroadcastDeviceId : broadcastId);
        MonitorDevice = FindDevice(string.IsNullOrEmpty(monitorId) ? _settings.MonitorDeviceId : monitorId);
    }

    /// <summary>
    /// Primera ejecución: emisión al cable virtual que encontremos (Wave Link, VB-Cable...) y
    /// monitor al dispositivo por defecto de Windows, para que se oiga algo desde el minuto uno.
    /// Ambas cosas se cambian a mano después.
    /// </summary>
    void ProposeDefaultDevices()
    {
        var devices = _deviceService.GetOutputDevices();

        // Recorremos las pistas en orden, no los dispositivos: queremos el "Wave Link SFX" aunque
        // alfabéticamente vaya después de un "CABLE Input".
        var cable = VirtualCableHints
            .Select(hint => devices.FirstOrDefault(d =>
                d.Name.Contains(hint, StringComparison.CurrentCultureIgnoreCase)))
            .FirstOrDefault(d => d is not null);
        // Si el cable virtual resulta ser también el dispositivo por defecto, no lo pongas en las dos
        // salidas: sonaría el doble de fuerte por el mismo sitio.
        var monitor = devices.FirstOrDefault(d => d.IsDefault && d.Id != cable?.Id);

        _settings.BroadcastDeviceId = cable?.Id;
        _settings.MonitorDeviceId = monitor?.Id;
    }

    AudioDeviceInfo FindDevice(string? id) =>
        string.IsNullOrEmpty(id) ? NoDevice : Devices.FirstOrDefault(d => d.Id == id) ?? NoDevice;

    /// <summary>Sin salida de emisión el soundboard no llega a Discord: hay que decirlo bien claro.</summary>
    public bool NeedsBroadcastDevice => string.IsNullOrEmpty(BroadcastDevice.Id);

    partial void OnBroadcastDeviceChanged(AudioDeviceInfo value)
    {
        OnPropertyChanged(nameof(NeedsBroadcastDevice));
        if (_suppressDeviceSwitch) return;
        var error = _engine.SetBroadcastDevice(string.IsNullOrEmpty(value.Id) ? null : value.Id);
        _settings.BroadcastDeviceId = string.IsNullOrEmpty(value.Id) ? null : value.Id;
        Status = error ?? (string.IsNullOrEmpty(value.Id) ? null : $"Emitiendo por {value.Name}");
        SaveSettings();
    }

    partial void OnMonitorDeviceChanged(AudioDeviceInfo value)
    {
        if (_suppressDeviceSwitch) return;
        var error = _engine.SetMonitorDevice(string.IsNullOrEmpty(value.Id) ? null : value.Id);
        _settings.MonitorDeviceId = string.IsNullOrEmpty(value.Id) ? null : value.Id;
        if (error is not null) Status = error;
        SaveSettings();
    }

    partial void OnBroadcastVolumeChanged(double value)
    {
        _engine.BroadcastVolume = (float)value;
        _settings.BroadcastVolume = value;
    }

    partial void OnMonitorVolumeChanged(double value)
    {
        _engine.MonitorVolume = (float)value;
        _settings.MonitorVolume = value;
    }

    // ---- Perfiles --------------------------------------------------------

    partial void OnSelectedProfileChanged(ProfileViewModel? oldValue, ProfileViewModel? newValue)
    {
        oldValue?.StopAll();

        if (newValue is null) return;
        _settings.LastProfileId = newValue.Model.Id;
        SaveSettings();

        // Descodificamos en segundo plano para que el primer click de cada pad no espere al disco.
        _ = Task.Run(async () =>
        {
            foreach (var pad in newValue.Pads.ToList())
                await pad.PreloadAsync();
        });
    }

    [RelayCommand]
    void AddProfile()
    {
        var name = _dialogs.AskText("Nuevo perfil", "¿Cómo se llama?", "");
        if (string.IsNullOrWhiteSpace(name)) return;

        var profile = BuildProfile(new Profile { Name = name.Trim() });
        Profiles.Add(profile);
        SelectedProfile = profile;
        SaveProfiles();
    }

    [RelayCommand]
    void RenameProfile(ProfileViewModel? profile)
    {
        if (profile is null) return;
        var name = _dialogs.AskText("Renombrar perfil", "Nuevo nombre", profile.Name);
        if (!string.IsNullOrWhiteSpace(name)) profile.Name = name.Trim();
    }

    [RelayCommand]
    void ChangeProfileIcon(ProfileViewModel? profile)
    {
        if (profile is null) return;
        var icon = _dialogs.AskText("Icono del perfil", "Pega aquí un emoji", profile.Icon);
        if (!string.IsNullOrWhiteSpace(icon)) profile.Icon = icon.Trim();
    }

    [RelayCommand]
    void DeleteProfile(ProfileViewModel? profile)
    {
        if (profile is null) return;
        if (!_dialogs.Confirm("Borrar perfil", $"¿Seguro que quieres borrar «{profile.Name}» y sus {profile.Pads.Count} sonidos?\n\nLos ficheros de audio no se tocan."))
            return;

        profile.StopAll();
        int index = Profiles.IndexOf(profile);
        Profiles.Remove(profile);
        SelectedProfile = Profiles.ElementAtOrDefault(index) ?? Profiles.LastOrDefault();
        SaveProfiles();
    }

    // ---- Pads ------------------------------------------------------------

    [RelayCommand]
    void AddSounds()
    {
        if (SelectedProfile is null) return;
        var files = _dialogs.AskAudioFiles();
        if (files is not null) AddFiles(files);
    }

    /// <summary>Alta de pads desde el diálogo o desde arrastrar y soltar sobre la rejilla.</summary>
    public void AddFiles(IEnumerable<string> paths)
    {
        var profile = SelectedProfile;
        if (profile is null) return;

        var added = new List<PadViewModel>();
        foreach (var path in paths.Where(File.Exists))
        {
            var pad = new SoundPad
            {
                Name = Path.GetFileNameWithoutExtension(path),
                FilePath = path,
                Color = PadPalette[(profile.Pads.Count + added.Count) % PadPalette.Length]
            };
            var vm = new PadViewModel(pad, _engine, RequestSave);
            profile.Add(vm);
            added.Add(vm);
        }

        if (added.Count == 0) return;

        Status = added.Count == 1 ? $"Añadido «{added[0].Name}»" : $"Añadidos {added.Count} sonidos";
        SaveProfiles();
        _ = Task.Run(async () =>
        {
            foreach (var pad in added) await pad.PreloadAsync();
        });
    }

    [RelayCommand]
    void RemovePad(PadViewModel? pad)
    {
        if (pad is null || SelectedProfile is null) return;
        SelectedProfile.Remove(pad);
        SaveProfiles();
    }

    [RelayCommand]
    void RenamePad(PadViewModel? pad)
    {
        if (pad is null) return;
        var name = _dialogs.AskText("Renombrar sonido", "Nuevo nombre", pad.Name);
        if (!string.IsNullOrWhiteSpace(name)) pad.Name = name.Trim();
    }

    [RelayCommand]
    void StopAll()
    {
        _engine.StopAll();
        foreach (var profile in Profiles) profile.StopAll();
        Status = "Todo parado";
    }

    // ---- Persistencia ----------------------------------------------------

    ProfileViewModel BuildProfile(Profile model) =>
        new(model, model.Pads.Select(p => new PadViewModel(p, _engine, RequestSave)), RequestSave);

    /// <summary>Guardado con retardo: mover un deslizador no debe escribir en disco en cada píxel.</summary>
    void RequestSave()
    {
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    void SaveProfiles() => _store.SaveProfiles(Profiles.Select(p => p.Model));

    void SaveSettings() => _store.SaveSettings(_settings);

    public void Dispose()
    {
        _saveTimer.Stop();
        SaveProfiles();
        SaveSettings();
    }
}
