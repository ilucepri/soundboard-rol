using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Soundboard.Audio;

/// <summary>
/// Una salida física: su mezclador, su volumen maestro y el WasapiOut que la alimenta.
/// El soundboard usa dos, una hacia Wave Link y otra hacia tus cascos.
/// </summary>
public sealed class OutputBus : IDisposable
{
    readonly MMDevice _device;
    readonly WasapiPlayer _output;
    readonly MixingSampleProvider _mixer;
    readonly VolumeSampleProvider _master;

    OutputBus(MMDevice device, WasapiPlayer output, MixingSampleProvider mixer, VolumeSampleProvider master)
    {
        _device = device;
        _output = output;
        _mixer = mixer;
        _master = master;
    }

    public string DeviceId => _device.ID;

    public string DeviceName => _device.FriendlyName;

    public float Volume
    {
        get => _master.Volume;
        set => _master.Volume = value;
    }

    /// <summary>Abre la salida. Lanza si el dispositivo ya no está disponible o está en uso exclusivo.</summary>
    public static OutputBus Open(MMDevice device, float volume)
    {
        var mixer = new MixingSampleProvider(AudioFormat.MixFormat)
        {
            // Sin esto el WasapiOut se pararía en cuanto no hubiera nada sonando, y el siguiente
            // disparo tendría que arrancarlo de nuevo (latencia perceptible). Con ReadFully el
            // mezclador entrega silencio y la salida se queda abierta.
            ReadFully = true
        };

        var master = new VolumeSampleProvider(mixer) { Volume = volume };

        // Modo compartido: Wave Link, Discord y el juego tienen que poder usar el mismo dispositivo
        // a la vez. En exclusivo tendríamos menos latencia pero echaríamos a todos los demás.
        var output = new WasapiPlayerBuilder()
            .WithDevice(device)
            .WithSharedMode()
            .WithEventSync()
            .WithLatency(50)
            .Build();

        try
        {
            output.Init(master);
            output.Play();
        }
        catch
        {
            output.Dispose();
            throw;
        }

        return new OutputBus(device, output, mixer, master);
    }

    public void Add(Voice voice) => _mixer.AddMixerInput(voice);

    public void Remove(Voice voice) => _mixer.RemoveMixerInput(voice);

    public void RemoveAll() => _mixer.RemoveAllMixerInputs();

    public void Dispose()
    {
        _mixer.RemoveAllMixerInputs();
        _output.Dispose();
        _device.Dispose();
    }
}
