using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Soundboard.Audio;
using Soundboard.Models;

namespace Soundboard.ViewModels;

/// <summary>Lo que un pad necesita del resto de la app, para no arrastrar un constructor enorme.</summary>
public sealed record PadContext(AudioEngine Engine, Action OnChanged, Action<string> SetStatus);

/// <summary>Un pad de la rejilla. Click para sonar, click otra vez para parar.</summary>
public sealed partial class PadViewModel : ObservableObject
{
    readonly PadContext _context;
    readonly SemaphoreSlim _loadGate = new(1, 1);

    ISoundSource? _source;
    PlaybackHandle? _handle;

    [ObservableProperty] string _name;
    [ObservableProperty] double _volume;
    [ObservableProperty] bool _loop;
    [ObservableProperty] string _color;
    [ObservableProperty] double _trimStart;
    [ObservableProperty] double _trimEnd;
    [ObservableProperty] string _shortcut;
    [ObservableProperty] bool _isPlaying;
    [ObservableProperty] bool _isLoading;
    [ObservableProperty] double _progress;
    [ObservableProperty] string? _error;

    public PadViewModel(SoundPad model, PadContext context)
    {
        Model = model;
        _context = context;

        _name = model.Name;
        _volume = model.Volume;
        _loop = model.Loop;
        _color = model.Color;
        _trimStart = model.TrimStart;
        _trimEnd = model.TrimEnd;
        _shortcut = model.Shortcut;
    }

    public SoundPad Model { get; }

    public string FilePath => Model.FilePath;

    public string FileName => Path.GetFileName(Model.FilePath);

    public string? Directory => Path.GetDirectoryName(Model.FilePath);

    /// <summary>Duración del fichero. Sale del modelo hasta que se descodifica de verdad.</summary>
    public TimeSpan Duration =>
        _source?.Duration ?? TimeSpan.FromSeconds(Model.DurationSeconds);

    public bool HasShortcut => !string.IsNullOrEmpty(Shortcut);

    public SoundSlice Slice => new(Loop, TrimStart, TrimEnd);

    /// <summary>Descodifica el fichero. Se llama al abrir el perfil, para que el primer click ya sea instantáneo.</summary>
    public async Task PreloadAsync()
    {
        if (_source is not null || Error is not null) return;

        await _loadGate.WaitAsync();
        try
        {
            if (_source is not null) return;
            IsLoading = true;
            var path = Model.FilePath;
            _source = await Task.Run(() => SoundSourceLoader.Load(path));
            Model.DurationSeconds = _source.Duration.TotalSeconds;
            OnPropertyChanged(nameof(Duration));
            Error = null;
        }
        catch (Exception ex)
        {
            Error = ex is FileNotFoundException
                ? "No encuentro el fichero"
                : $"No se puede leer: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            _loadGate.Release();
        }
    }

    /// <summary>Los picos para la forma de onda del editor. Null si el fichero no se pudo leer.</summary>
    public async Task<float[]?> GetPeaksAsync(int count)
    {
        await PreloadAsync();
        if (_source is null) return null;
        var source = _source;
        return await Task.Run(() => source.GetPeaks(count));
    }

    [RelayCommand]
    public async Task TriggerAsync()
    {
        if (_handle is not null)
        {
            Stop();
            _context.SetStatus($"Parado «{Name}»");
            return;
        }

        await PreloadAsync();
        if (_source is null)
        {
            if (Error is not null) _context.SetStatus(Error);
            return;
        }

        var handle = _context.Engine.Play(_source, (float)Volume, Slice);
        if (handle is null)
        {
            _context.SetStatus("Elige primero una salida de emisión");
            return;
        }

        _handle = handle;
        IsPlaying = true;
        Progress = 0;
        _context.SetStatus($"Sonando «{Name}»");

        handle.Ended += (_, _) => OnUiThread(() =>
        {
            if (!ReferenceEquals(_handle, handle)) return;
            _handle = null;
            IsPlaying = false;
            Progress = 0;
        });
    }

    [RelayCommand]
    public void Stop()
    {
        var handle = _handle;
        _handle = null;
        IsPlaying = false;
        Progress = 0;
        handle?.Stop();
    }

    /// <summary>La llama el reloj de la UI mientras algo suena.</summary>
    public void RefreshProgress()
    {
        if (_handle is { } handle) Progress = handle.Progress;
    }

    partial void OnNameChanged(string value)
    {
        Model.Name = value;
        _context.OnChanged();
    }

    partial void OnVolumeChanged(double value)
    {
        Model.Volume = value;
        if (_handle is not null) _handle.Volume = (float)value;
        _context.OnChanged();
    }

    partial void OnLoopChanged(bool value)
    {
        Model.Loop = value;
        // El bucle se decide al abrir el stream, así que un cambio en caliente no afecta a lo que ya suena.
        _context.OnChanged();
    }

    partial void OnColorChanged(string value)
    {
        Model.Color = value;
        _context.OnChanged();
    }

    partial void OnTrimStartChanged(double value)
    {
        Model.TrimStart = value;
        _context.OnChanged();
    }

    partial void OnTrimEndChanged(double value)
    {
        Model.TrimEnd = value;
        _context.OnChanged();
    }

    partial void OnShortcutChanged(string value)
    {
        Model.Shortcut = value;
        OnPropertyChanged(nameof(HasShortcut));
        _context.OnChanged();
    }

    static void OnUiThread(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess()) action();
        else dispatcher.BeginInvoke(action);
    }
}
