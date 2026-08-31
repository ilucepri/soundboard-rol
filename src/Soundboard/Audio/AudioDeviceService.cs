using NAudio.CoreAudioApi;

namespace Soundboard.Audio;

public sealed record AudioDeviceInfo(string Id, string Name, bool IsDefault)
{
    public override string ToString() => Name;
}

/// <summary>Lista los dispositivos de reproducción activos de Windows.</summary>
public sealed class AudioDeviceService : IDisposable
{
    readonly MMDeviceEnumerator _enumerator = new();

    public IReadOnlyList<AudioDeviceInfo> GetOutputDevices()
    {
        string? defaultId = null;
        try
        {
            using var def = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            defaultId = def.ID;
        }
        catch (Exception)
        {
            // Sin dispositivo de salida por defecto (raro, pero pasa si se desconecta todo).
        }

        var devices = new List<AudioDeviceInfo>();
        foreach (var device in _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        {
            using (device)
                devices.Add(new AudioDeviceInfo(device.ID, device.FriendlyName, device.ID == defaultId));
        }

        return devices.OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>Devuelve el dispositivo por id, o null si ya no existe.</summary>
    public MMDevice? TryGetDevice(string id)
    {
        try
        {
            var device = _enumerator.GetDevice(id);
            return device.State == DeviceState.Active ? device : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public void Dispose() => _enumerator.Dispose();
}
