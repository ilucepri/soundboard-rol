using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Soundboard.Audio;
using Soundboard.Models;

namespace Soundboard.ViewModels;

/// <summary>Un pad de la rejilla. Click para sonar, click otra vez para parar.</summary>
public sealed partial class PadViewModel : ObservableObject
{
    readonly AudioEngine _engine;
    readonly Action _onChanged;
    readonly SemaphoreSlim _loadGate = new(1, 1);

    ISoundSource? _source;
    PlaybackHandle? _handle;

    [ObservableProperty] string _name;
    [ObservableProperty] double _volume;
    [ObservableProperty] bool _loop;
    [ObservableProperty] string _color;
    [ObservableProperty] bool _isPlaying;
    [ObservableProperty] bool _isLoading;
    [ObservableProperty] string? _error;

    public PadViewModel(SoundPad model, AudioEngine engine, Action onChanged)
    {
        Model = model;
        _engine = engine;
        _onChanged = onChanged;

        _name = model.Name;
        _volume = model.Volume;
        _loop = model.Loop;
        _color = model.Color;
    }

    public SoundPad Model { get; }

    public string FilePath => Model.FilePath;

    public string FileName => Path.GetFileName(Model.FilePath);

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

    [RelayCommand]
    async Task TriggerAsync()
    {
        if (_handle is not null)
        {
            Stop();
            return;
        }

        await PreloadAsync();
        if (_source is null) return;

        var handle = _engine.Play(_source, (float)Volume, Loop);
        if (handle is null)
        {
            Error = "Elige primero una salida de audio";
            return;
        }

        Error = null;
        _handle = handle;
        IsPlaying = true;

        handle.Ended += (_, _) => OnUiThread(() =>
        {
            if (!ReferenceEquals(_handle, handle)) return;
            _handle = null;
            IsPlaying = false;
        });
    }

    [RelayCommand]
    void SetColor(string? color)
    {
        if (!string.IsNullOrWhiteSpace(color)) Color = color;
    }

    [RelayCommand]
    public void Stop()
    {
        var handle = _handle;
        _handle = null;
        IsPlaying = false;
        handle?.Stop();
    }

    partial void OnNameChanged(string value)
    {
        Model.Name = value;
        _onChanged();
    }

    partial void OnVolumeChanged(double value)
    {
        Model.Volume = value;
        if (_handle is not null) _handle.Volume = (float)value;
        _onChanged();
    }

    partial void OnLoopChanged(bool value)
    {
        Model.Loop = value;
        // El bucle se decide al abrir el stream, así que un cambio en caliente no afecta a lo que ya suena.
        _onChanged();
    }

    partial void OnColorChanged(string value)
    {
        Model.Color = value;
        _onChanged();
    }

    static void OnUiThread(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess()) action();
        else dispatcher.BeginInvoke(action);
    }
}
