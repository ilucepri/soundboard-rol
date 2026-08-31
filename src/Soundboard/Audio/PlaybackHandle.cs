namespace Soundboard.Audio;

/// <summary>
/// Un disparo del pad. Como el mismo sonido va a dos salidas a la vez, agrupa las dos voces
/// para poder pararlas y ajustarlas juntas.
/// </summary>
public sealed class PlaybackHandle
{
    readonly Voice[] _voices;
    int _alive;

    internal PlaybackHandle(IReadOnlyList<Voice> voices)
    {
        _voices = [.. voices];
        _alive = _voices.Length;

        foreach (var voice in _voices)
            voice.Ended += OnVoiceEnded;
    }

    /// <summary>Salta cuando ya no queda ninguna voz sonando. Se dispara desde el hilo de audio.</summary>
    public event EventHandler? Ended;

    public bool IsStopping { get; private set; }

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
