using NAudio.Wave;

namespace Soundboard.Audio;

/// <summary>
/// Formato interno del mezclador. Todo se convierte a esto al cargar, así cualquier sonido puede
/// mezclarse con cualquier otro sin comprobaciones en tiempo real. 48 kHz porque es lo que usa
/// el Elgato Virtual Audio (y casi todo lo demás hoy en día).
/// </summary>
public static class AudioFormat
{
    public const int SampleRate = 48_000;
    public const int Channels = 2;

    public static readonly WaveFormat MixFormat =
        WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, Channels);
}
