using NAudio.Wave;

namespace Soundboard.Audio;

/// <summary>
/// Una reproducción concreta dentro de un mezclador. Aplica la ganancia del pad y, al pararse,
/// hace una rampa muy corta a silencio: cortar en seco un sample deja un "clic" bastante feo.
/// Cuando termina devuelve 0, y MixingSampleProvider la descarta sola.
/// </summary>
public sealed class Voice : ISampleProvider
{
    const int FadeOutMs = 15;

    readonly ISampleProvider _inner;
    readonly IDisposable? _owner;
    readonly int _fadeLength;

    float _volume;
    int _fadeRemaining = -1;
    bool _finished;

    public Voice(SoundStream stream, float volume)
    {
        _inner = stream.Provider;
        _owner = stream.Owner;
        _volume = volume;
        _fadeLength = AudioFormat.SampleRate * FadeOutMs / 1000 * AudioFormat.Channels;
    }

    public WaveFormat WaveFormat => _inner.WaveFormat;

    /// <summary>Se lee desde el hilo de audio, así que nada de bloqueos aquí.</summary>
    public event EventHandler? Ended;

    public float Volume
    {
        get => Volatile.Read(ref _volume);
        set => Volatile.Write(ref _volume, value);
    }

    /// <summary>Pide el final con rampa. Idempotente.</summary>
    public void BeginStop()
    {
        if (_fadeRemaining < 0) _fadeRemaining = _fadeLength;
    }

    public int Read(Span<float> buffer)
    {
        if (_finished) return 0;

        int read = _inner.Read(buffer);
        if (read == 0)
        {
            Finish();
            return 0;
        }

        float volume = Volatile.Read(ref _volume);

        if (_fadeRemaining < 0)
        {
            if (volume != 1f)
                for (int i = 0; i < read; i++)
                    buffer[i] *= volume;
        }
        else
        {
            for (int i = 0; i < read; i++)
            {
                if (_fadeRemaining <= 0)
                {
                    // El resto del bloque ya es silencio; lo recortamos y cerramos.
                    read = i;
                    Finish();
                    break;
                }
                buffer[i] *= volume * _fadeRemaining / _fadeLength;
                _fadeRemaining--;
            }
        }

        if (read == 0) Finish();
        return read;
    }

    void Finish()
    {
        if (_finished) return;
        _finished = true;
        _owner?.Dispose();
        Ended?.Invoke(this, EventArgs.Empty);
    }
}
