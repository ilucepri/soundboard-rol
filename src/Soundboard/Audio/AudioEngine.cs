using System.Collections.Concurrent;

namespace Soundboard.Audio;

/// <summary>
/// Reproduce cada sonido por dos salidas independientes:
///
///   emisión  -> el dispositivo que Wave Link recoge -> lo oyen los demás en Discord
///   monitor  -> tus cascos -> lo oyes tú, a tu propio volumen
///
/// Son dos WasapiOut separados con su propio mezclador, así que puedes bajarte el efecto a ti
/// sin tocar lo que llega a la mesa. Cualquiera de las dos puede estar desactivada.
/// </summary>
public sealed class AudioEngine : IDisposable
{
    readonly AudioDeviceService _devices;
    readonly object _gate = new();
    readonly ConcurrentDictionary<PlaybackHandle, byte> _active = new();

    OutputBus? _broadcast;
    OutputBus? _monitor;
    float _broadcastVolume = 0.8f;
    float _monitorVolume = 0.5f;

    public AudioEngine(AudioDeviceService devices) => _devices = devices;

    public string? BroadcastDeviceId { get { lock (_gate) return _broadcast?.DeviceId; } }

    public string? MonitorDeviceId { get { lock (_gate) return _monitor?.DeviceId; } }

    public float BroadcastVolume
    {
        get => _broadcastVolume;
        set
        {
            _broadcastVolume = Math.Clamp(value, 0f, 1f);
            lock (_gate)
                if (_broadcast is not null) _broadcast.Volume = _broadcastVolume;
        }
    }

    public float MonitorVolume
    {
        get => _monitorVolume;
        set
        {
            _monitorVolume = Math.Clamp(value, 0f, 1f);
            lock (_gate)
                if (_monitor is not null) _monitor.Volume = _monitorVolume;
        }
    }

    /// <summary>Cambia la salida de emisión. Devuelve el error si no se pudo abrir, o null si fue bien.</summary>
    public string? SetBroadcastDevice(string? deviceId) => SetDevice(deviceId, isBroadcast: true);

    /// <summary>Cambia la salida de monitor. Devuelve el error si no se pudo abrir, o null si fue bien.</summary>
    public string? SetMonitorDevice(string? deviceId) => SetDevice(deviceId, isBroadcast: false);

    string? SetDevice(string? deviceId, bool isBroadcast)
    {
        OutputBus? opened = null;

        if (deviceId is not null)
        {
            var device = _devices.TryGetDevice(deviceId);
            if (device is null)
                return "Ese dispositivo ya no está disponible.";

            try
            {
                opened = OutputBus.Open(device, isBroadcast ? _broadcastVolume : _monitorVolume);
            }
            catch (Exception ex)
            {
                device.Dispose();
                return $"No se pudo abrir el dispositivo: {ex.Message}";
            }
        }

        OutputBus? previous;
        lock (_gate)
        {
            if (isBroadcast) (previous, _broadcast) = (_broadcast, opened);
            else (previous, _monitor) = (_monitor, opened);
        }

        // Las voces vivas apuntaban al mezclador viejo, así que se van con él.
        previous?.Dispose();
        PruneHandles();
        return null;
    }

    /// <summary>Dispara un sonido. Devuelve null si no hay ninguna salida configurada.</summary>
    public PlaybackHandle? Play(ISoundSource source, float volume, bool loop)
    {
        OutputBus? broadcast, monitor;
        lock (_gate) (broadcast, monitor) = (_broadcast, _monitor);

        var voices = new List<Voice>(2);
        AttachTo(broadcast);
        AttachTo(monitor);

        if (voices.Count == 0) return null;

        var handle = new PlaybackHandle(voices);
        _active[handle] = 0;
        handle.Ended += (_, _) => _active.TryRemove(handle, out _);
        return handle;

        void AttachTo(OutputBus? bus)
        {
            if (bus is null) return;
            try
            {
                var voice = new Voice(source.Open(loop), volume);
                bus.Add(voice);
                voices.Add(voice);
            }
            catch (Exception)
            {
                // El dispositivo se cambió o se desconectó justo ahora: la otra salida sigue valiendo.
            }
        }
    }

    /// <summary>Para todo con la misma rampa corta que un pad suelto, para que no cruja.</summary>
    public void StopAll()
    {
        foreach (var handle in _active.Keys)
            handle.Stop();
    }

    void PruneHandles()
    {
        foreach (var handle in _active.Keys)
            _active.TryRemove(handle, out _);
    }

    public void Dispose()
    {
        OutputBus? broadcast, monitor;
        lock (_gate)
        {
            (broadcast, _broadcast) = (_broadcast, null);
            (monitor, _monitor) = (_monitor, null);
        }
        broadcast?.Dispose();
        monitor?.Dispose();
        _active.Clear();
    }
}
