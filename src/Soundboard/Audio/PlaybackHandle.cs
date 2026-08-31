namespace Soundboard.Audio;

/// <summary>
/// Un disparo del pad. Como el mismo sonido va a dos salidas a la vez, agrupa las dos voces
/// para poder pararlas y ajustarlas juntas.
/// </summary>
public sealed class PlaybackHandle
{
    readonly Voice[] _voices;
    readonly long _totalSamples;
    readonly bool _loop;
    int _alive;

    internal PlaybackHandle(IReadOnlyList<Voice> voices, long totalSamples, bool loop)
    {
        _voices = [.. voices];
        _totalSamples = totalSamples;
        _loop = loop;
        _alive = _voices.Length;

        foreach (var voice in _voices)
            voice.Ended += OnVoiceEnded;
    }

    /// <summary>Salta cuando ya no queda ninguna voz sonando. Se dispara desde el hilo de audio.</summary>
    public event EventHandler? Ended;

    public bool IsStopping { get; private set; }

    /// <summary>
    /// Cuánto se ha reproducido, 0..1. Un pad en bucle no tiene final, así que se queda lleno
    /// mientras suene, que es lo que pide el diseño.
    /// </summary>
    public double Progress
    {
        get
        {
            if (_loop) return 1;
            if (_totalSamples <= 0 || _voices.Length == 0) return 0;
            return Math.Clamp((double)_voices[0].SamplesRead / _totalSamples, 0, 1);
        }
    }

    /// <summary>Ganancia del pad, 0..1. Se puede mover mientras suena.</summary>
    public float Volume
    {
        set
        {
            foreach (var voice in _voices)
                voice.Volume = value;
        }
    }

    public void Stop()
    {
        IsStopping = true;
        foreach (var voice in _voices)
            voice.BeginStop();
    }

    void OnVoiceEnded(object? sender, EventArgs e)
    {
        if (Interlocked.Decrement(ref _alive) == 0)
            Ended?.Invoke(this, EventArgs.Empty);
    }
}
