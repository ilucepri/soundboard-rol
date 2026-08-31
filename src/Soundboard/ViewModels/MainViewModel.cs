using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Soundboard.Audio;
using Soundboard.Models;
using Soundboard.Services;

namespace Soundboard.ViewModels;

public enum MainView
{
    Board,
    Library
}

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
    /// Los canales de Wave Link se pueden renombrar, y al hacerlo pierden el prefijo: un canal SFX
    /// puede aparecer como "Wave Link SFX (Elgato Wave:3)" o simplemente como "SFX (Elgato Wave:3)".
    /// Por eso hay pistas con y sin prefijo. No buscamos "elgato" a secas a propósito: eso también
    /// casaría con la salida de auriculares física del Wave:3, que no es un canal del mezclador.
    static readonly string[] VirtualCableHints =
        ["wave link sfx", "sfx", "wave link aux", "wave link", "cable input", "vb-audio", "voicemeeter", "virtual"];

    public static readonly string[] PadPalette =
    [
        "#B4654A", "#7C6AA6", "#48788A", "#A2874A",
        "#8A5566", "#4F7A5C", "#5A6C9C", "#9A5D3E"
    ];

    /// <summary>El reloj que refresca barras de progreso y el contador de "sonando".</summary>
    static readonly TimeSpan UiTick = TimeSpan.FromMilliseconds(90);

    readonly ProfileStore _store;
    readonly AudioDeviceService _deviceService;
    readonly AudioEngine _engine;
    readonly IDialogService _dialogs;
    readonly DispatcherTimer _saveTimer;
    readonly DispatcherTimer _uiTimer;
    readonly AppSettings _settings;

    bool _suppressDeviceSwitch;

    /// <summary>Último error al abrir una salida, para que no lo tape un mensaje informativo.</summary>
    string? _lastDeviceError;

    [ObservableProperty] ProfileViewModel? _selectedProfile;
    [ObservableProperty] AudioDeviceInfo _broadcastDevice = NoDevice;
    [ObservableProperty] AudioDeviceInfo _monitorDevice = NoDevice;
    [ObservableProperty] double _broadcastVolume;
    [ObservableProperty] double _monitorVolume;
    [ObservableProperty] string? _status;
    [ObservableProperty] string _searchText = "";
    [ObservableProperty] MainView _activeView = MainView.Board;
    [ObservableProperty] string _playingLabel = "Nada sonando";

    public MainViewModel(ProfileStore store, AudioDeviceService deviceService, AudioEngine engine, IDialogService dialogs)
    {
        _store = store;
        _deviceService = deviceService;
        _engine = engine;
        _dialogs = dialogs;

        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); SaveProfiles(); };

        _uiTimer = new DispatcherTimer { Interval = UiTick };
        _uiTimer.Tick += (_, _) => OnUiTick();

        Profiles = [];
        Devices = [];
        VisiblePads = [];
        LibraryEntries = [];

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

    /// <summary>Los pads del perfil activo que pasan el filtro del buscador.</summary>
    public ObservableCollection<PadViewModel> VisiblePads { get; }

    public ObservableCollection<LibraryEntry> LibraryEntries { get; }

    public bool IsBoardView => ActiveView == MainView.Board;

    public bool IsLibraryView => ActiveView == MainView.Library;

    public string BoardSubtitle
    {
        get
        {
            int count = SelectedProfile?.Pads.Count ?? 0;
            string sounds = count == 1 ? "1 sonido" : $"{count} sonidos";
            return $"{sounds} · click para sonar, click otra vez para parar";
        }
    }

    // ---- Dispositivos ----------------------------------------------------

    [RelayCommand]
    public void RefreshDevices()
    {
        _suppressDeviceSwitch = true;
        Devices.Clear();
        Devices.Add(NoDevice);
        foreach (var device in _deviceService.GetOutputDevices())
            Devices.Add(device);
        _suppressDeviceSwitch = false;

        // Los ajustes van siempre al día con la selección actual, así que reconstruirla desde ahí
        // sirve tanto al arrancar como al pulsar «Dispositivos».
        _lastDeviceError = null;
        var broadcast = ResolveBroadcast(out string? notice);
        BroadcastDevice = broadcast;
        MonitorDevice = Resolve(_settings.MonitorDeviceId, _settings.MonitorDeviceName);

        // Se asigna después, porque cambiar el dispositivo pisa Status con su propio mensaje.
        // Un fallo real al abrir manda sobre el aviso informativo: si no, el error se pierde.
        if (_lastDeviceError is not null) Status = _lastDeviceError;
        else if (notice is not null) Status = notice;
    }

    /// <summary>
    /// Recupera un dispositivo guardado. Primero por id, y si ya no existe, por nombre: Wave Link
    /// recrea sus endpoints con id nuevo en cuanto tocas los canales, y entonces el id guardado
    /// apunta al vacío y la app se queda muda sin motivo aparente.
    /// </summary>
    AudioDeviceInfo Resolve(string? id, string? name)
    {
        if (!string.IsNullOrEmpty(id) && Devices.FirstOrDefault(d => d.Id == id) is { } byId)
            return byId;

        if (!string.IsNullOrEmpty(name) &&
            Devices.FirstOrDefault(d => string.Equals(d.Name, name, StringComparison.CurrentCultureIgnoreCase)) is { } byName)
            return byName;

        return NoDevice;
    }

    /// <summary>
    /// Como <see cref="Resolve"/>, pero si la salida de emisión ha desaparecido del todo propone el
    /// cable virtual que encuentre. Quedarse sin emisión es justo el fallo que no se nota: la app
    /// parece funcionar y no la oye nadie.
    /// </summary>
    AudioDeviceInfo ResolveBroadcast(out string? notice)
    {
        notice = null;

        var found = Resolve(_settings.BroadcastDeviceId, _settings.BroadcastDeviceName);
        if (!string.IsNullOrEmpty(found.Id) || string.IsNullOrEmpty(_settings.BroadcastDeviceId))
            return found;

        var lost = _settings.BroadcastDeviceName ?? "La salida de emisión";
        var replacement = FindVirtualCable();

        notice = replacement is null
            ? $"«{lost}» ya no está disponible. Elige otra salida de emisión."
            : $"«{lost}» ya no está; se ha puesto «{replacement.Name}».";

        return replacement ?? NoDevice;
    }

    /// <summary>
    /// Primera ejecución: emisión al cable virtual que encontremos (Wave Link, VB-Cable...) y
    /// monitor al dispositivo por defecto de Windows, para que se oiga algo desde el minuto uno.
    /// Ambas cosas se cambian a mano después.
    /// </summary>
    void ProposeDefaultDevices()
    {
        var cable = FindVirtualCable();

        // Si el cable virtual resulta ser también el dispositivo por defecto, no lo pongas en las dos
        // salidas: sonaría el doble de fuerte por el mismo sitio.
        var monitor = _deviceService.GetOutputDevices().FirstOrDefault(d => d.IsDefault && d.Id != cable?.Id);

        _settings.BroadcastDeviceId = cable?.Id;
        _settings.BroadcastDeviceName = cable?.Name;
        _settings.MonitorDeviceId = monitor?.Id;
        _settings.MonitorDeviceName = monitor?.Name;
    }

    /// <summary>El primer cable virtual que aparezca, en el orden de preferencia de las pistas.</summary>
    AudioDeviceInfo? FindVirtualCable()
    {
        var devices = _deviceService.GetOutputDevices();

        // Recorremos las pistas en orden, no los dispositivos: queremos el canal de Wave Link aunque
        // alfabéticamente vaya después de un "CABLE Input".
        return VirtualCableHints
            .Select(hint => devices.FirstOrDefault(d =>
                d.Name.Contains(hint, StringComparison.CurrentCultureIgnoreCase)))
            .FirstOrDefault(d => d is not null);
    }

    /// <summary>Sin salida de emisión el soundboard no llega a Discord: hay que decirlo bien claro.</summary>
    public bool NeedsBroadcastDevice => string.IsNullOrEmpty(BroadcastDevice.Id);

    partial void OnBroadcastDeviceChanged(AudioDeviceInfo value)
    {
        OnPropertyChanged(nameof(NeedsBroadcastDevice));
        if (_suppressDeviceSwitch) return;
        var error = _engine.SetBroadcastDevice(string.IsNullOrEmpty(value.Id) ? null : value.Id);
        if (error is not null) _lastDeviceError = error;
        bool none = string.IsNullOrEmpty(value.Id);
        _settings.BroadcastDeviceId = none ? null : value.Id;
        _settings.BroadcastDeviceName = none ? null : value.Name;
        Status = error ?? (none ? "Sin salida de emisión" : $"Emitiendo por {value.Name}");
        SaveSettings();
    }

    partial void OnMonitorDeviceChanged(AudioDeviceInfo value)
    {
        if (_suppressDeviceSwitch) return;
        var error = _engine.SetMonitorDevice(string.IsNullOrEmpty(value.Id) ? null : value.Id);
        bool none = string.IsNullOrEmpty(value.Id);
        _settings.MonitorDeviceId = none ? null : value.Id;
        _settings.MonitorDeviceName = none ? null : value.Name;
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

    // ---- Vistas y búsqueda -----------------------------------------------

    partial void OnActiveViewChanged(MainView value)
    {
        OnPropertyChanged(nameof(IsBoardView));
        OnPropertyChanged(nameof(IsLibraryView));
        if (value == MainView.Library) RebuildLibrary();
    }

    [RelayCommand]
    void ShowLibrary() => ActiveView = MainView.Library;

    [RelayCommand]
    void ShowBoard() => ActiveView = MainView.Board;

    partial void OnSearchTextChanged(string value) => RefreshVisiblePads();

    void RefreshVisiblePads()
    {
        VisiblePads.Clear();
        if (SelectedProfile is null) return;

        var term = SearchText?.Trim() ?? "";
        foreach (var pad in SelectedProfile.Pads)
        {
            if (term.Length == 0 || pad.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase))
                VisiblePads.Add(pad);
        }
    }

    // ---- Perfiles --------------------------------------------------------

    partial void OnSelectedProfileChanged(ProfileViewModel? oldValue, ProfileViewModel? newValue)
    {
        oldValue?.StopAll();
        if (oldValue is not null) oldValue.Pads.CollectionChanged -= OnSelectedPadsChanged;

        // Cambiar de perfil vuelve al tablero y limpia el filtro anterior.
        SearchText = "";
        ActiveView = MainView.Board;
        RefreshVisiblePads();
        OnPropertyChanged(nameof(BoardSubtitle));

        if (newValue is null) return;
        newValue.Pads.CollectionChanged += OnSelectedPadsChanged;

        _settings.LastProfileId = newValue.Model.Id;
        SaveSettings();

        // Descodificamos en segundo plano para que el primer click de cada pad no espere al disco.
        _ = Task.Run(async () =>
        {
            foreach (var pad in newValue.Pads.ToList())
                await pad.PreloadAsync();
        });
    }

    void OnSelectedPadsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshVisiblePads();
        OnPropertyChanged(nameof(BoardSubtitle));
    }

    [RelayCommand]
    void AddProfile()
    {
        var edit = _dialogs.EditProfile("Nuevo perfil", "", "🎲");
        if (edit is null) return;

        var profile = BuildProfile(new Profile
        {
            Name = string.IsNullOrWhiteSpace(edit.Name) ? "Nuevo perfil" : edit.Name.Trim(),
            Icon = string.IsNullOrWhiteSpace(edit.Icon) ? "🎲" : edit.Icon.Trim()
        });

        Profiles.Add(profile);
        SelectedProfile = profile;
        SaveProfiles();
    }

    [RelayCommand]
    void EditProfile(ProfileViewModel? profile)
    {
        if (profile is null) return;
        var edit = _dialogs.EditProfile("Editar perfil", profile.Name, profile.Icon);
        if (edit is null) return;

        profile.Name = string.IsNullOrWhiteSpace(edit.Name) ? "Nuevo perfil" : edit.Name.Trim();
        profile.Icon = string.IsNullOrWhiteSpace(edit.Icon) ? "🎲" : edit.Icon.Trim();
        SaveProfiles();
    }

    [RelayCommand]
    void DuplicateProfile(ProfileViewModel? profile)
    {
        if (profile is null) return;

        var copy = new Profile
        {
            Name = $"{profile.Name} (copia)",
            Icon = profile.Icon,
            Pads = [.. profile.Model.Pads.Select(p => new SoundPad
            {
                Name = p.Name,
                FilePath = p.FilePath,
                Volume = p.Volume,
                Loop = p.Loop,
                Color = p.Color,
                TrimStart = p.TrimStart,
                TrimEnd = p.TrimEnd,
                Shortcut = p.Shortcut,
                DurationSeconds = p.DurationSeconds
            })]
        };

        var vm = BuildProfile(copy);
        Profiles.Insert(Profiles.IndexOf(profile) + 1, vm);
        SelectedProfile = vm;
        Status = $"Duplicado «{profile.Name}»";
        SaveProfiles();
    }

    [RelayCommand]
    void DeleteProfile(ProfileViewModel? profile)
    {
        if (profile is null) return;
        if (!_dialogs.Confirm("Borrar perfil",
                $"¿Seguro que quieres borrar «{profile.Name}» y sus {profile.Pads.Count} sonidos?\n\nLos ficheros de audio no se tocan."))
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
            var vm = BuildPad(pad);
            profile.Add(vm);
            added.Add(vm);
        }

        if (added.Count == 0) return;

        ActiveView = MainView.Board;
        Status = added.Count == 1 ? $"Añadido «{added[0].Name}»" : $"Añadidos {added.Count} sonidos";
        SaveProfiles();
        _ = Task.Run(async () =>
        {
            foreach (var pad in added) await pad.PreloadAsync();
        });
    }

    [RelayCommand]
    void EditPad(PadViewModel? pad)
    {
        if (pad is null) return;

        switch (_dialogs.EditPad(pad))
        {
            case PadEditResult.Save:
                Status = $"Guardado «{pad.Name}»";
                RefreshVisiblePads();
                SaveProfiles();
                break;
            case PadEditResult.Remove:
                RemovePad(pad);
                break;
        }
    }

    [RelayCommand]
    void RemovePad(PadViewModel? pad)
    {
        if (pad is null || SelectedProfile is null) return;
        var name = pad.Name;
        SelectedProfile.Remove(pad);
        Status = $"Quitado «{name}»";
        SaveProfiles();
    }

    [RelayCommand]
    void RevealPad(PadViewModel? pad)
    {
        if (pad is not null) _dialogs.RevealInExplorer(pad.FilePath);
    }

    [RelayCommand]
    void ToggleLoop(PadViewModel? pad)
    {
        if (pad is null) return;
        pad.Loop = !pad.Loop;
    }

    [RelayCommand]
    void StopAll()
    {
        _engine.StopAll();
        foreach (var profile in Profiles) profile.StopAll();
        Status = "Todo parado";
    }

    /// <summary>Dispara el pad cuya tecla coincide. Devuelve false si ninguno la tiene asignada.</summary>
    public bool TriggerShortcut(string key)
    {
        var pad = SelectedProfile?.Pads.FirstOrDefault(p =>
            string.Equals(p.Shortcut, key, StringComparison.CurrentCultureIgnoreCase));
        if (pad is null) return false;

        _ = pad.TriggerAsync();
        return true;
    }

    // ---- Biblioteca ------------------------------------------------------

    void RebuildLibrary()
    {
        LibraryEntries.Clear();

        var byPath = new Dictionary<string, (TimeSpan Duration, List<string> UsedBy)>(
            StringComparer.CurrentCultureIgnoreCase);

        foreach (var profile in Profiles)
        foreach (var pad in profile.Pads)
        {
            if (!byPath.TryGetValue(pad.FilePath, out var entry))
                entry = (pad.Duration, []);

            if (entry.Duration <= TimeSpan.Zero) entry = (pad.Duration, entry.UsedBy);
            if (!entry.UsedBy.Contains(profile.Name)) entry.UsedBy.Add(profile.Name);

            byPath[pad.FilePath] = entry;
        }

        foreach (var (path, entry) in byPath.OrderBy(p => Path.GetFileName(p.Key), StringComparer.CurrentCultureIgnoreCase))
            LibraryEntries.Add(new LibraryEntry(path, entry.Duration, entry.UsedBy));
    }

    [RelayCommand]
    void AddFromLibrary(LibraryEntry? entry)
    {
        if (entry is null) return;
        AddFiles([entry.FilePath]);
    }

    // ---- Reloj de la interfaz --------------------------------------------

    void OnPadPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PadViewModel.IsPlaying)) return;

        UpdatePlayingLabel();
        if (!_uiTimer.IsEnabled) _uiTimer.Start();
    }

    void OnUiTick()
    {
        if (SelectedProfile is not null)
            foreach (var pad in SelectedProfile.Pads)
                if (pad.IsPlaying) pad.RefreshProgress();

        // Con nada sonando el reloj no tiene nada que refrescar; se vuelve a arrancar solo
        // en cuanto un pad cambie a "sonando".
        if (CountPlaying() == 0) _uiTimer.Stop();
    }

    int CountPlaying() => Profiles.Sum(p => p.Pads.Count(pad => pad.IsPlaying));

    void UpdatePlayingLabel()
    {
        int count = CountPlaying();
        PlayingLabel = count switch
        {
            0 => "Nada sonando",
            1 => "1 sonando",
            _ => $"{count} sonando"
        };
    }

    // ---- Persistencia ----------------------------------------------------

    ProfileViewModel BuildProfile(Profile model) =>
        new(model, model.Pads.Select(BuildPad), RequestSave);

    PadViewModel BuildPad(SoundPad model)
    {
        var pad = new PadViewModel(model, new PadContext(_engine, RequestSave, message => Status = message));
        pad.PropertyChanged += OnPadPropertyChanged;
        return pad;
    }

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
        _uiTimer.Stop();
        SaveProfiles();
        SaveSettings();
    }
}
