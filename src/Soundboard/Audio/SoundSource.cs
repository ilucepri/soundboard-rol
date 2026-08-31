using System.IO;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Soundboard.Audio;

/// <summary>Un stream listo para reproducir, junto con lo que haya que liberar al terminar.</summary>
public readonly record struct SoundStream(ISampleProvider Provider, IDisposable? Owner);

/// <summary>
/// Un fichero de audio preparado para sonar. Un mismo origen puede abrirse varias veces a la vez:
/// una por cada salida (Wave Link + cascos) y una por cada disparo simultáneo del pad.
/// </summary>
public interface ISoundSource
{
    TimeSpan Duration { get; }
    SoundStream Open(bool loop);
}

public static class SoundSourceLoader
{
    /// <summary>
    /// Por encima de esta duración no cacheamos en RAM. Un efecto corto cabe de sobra; una pista de
    /// ambiente de cinco minutos ocuparía ~115 MB en float, así que esa se lee del disco.
    /// </summary>
    static readonly TimeSpan CacheLimit = TimeSpan.FromSeconds(20);

    public static ISoundSource Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("No encuentro el fichero de audio.", path);

        using var reader = new AudioFileReader(path);
        var duration = reader.TotalTime;

        if (duration > CacheLimit)
            return new StreamingSoundSource(path, duration);

        var chain = BuildChain(reader);
        var buffer = new List<float>((int)(duration.TotalSeconds * AudioFormat.SampleRate * AudioFormat.Channels) + 1024);
        var scratch = new float[AudioFormat.SampleRate * AudioFormat.Channels];
        int read;
        while ((read = chain.Read(scratch)) > 0)
            buffer.AddRange(scratch.AsSpan(0, read));

        return new CachedSoundSource(buffer.ToArray(), duration);
    }

    /// <summary>Lleva cualquier fichero al formato del mezclador: float 48 kHz estéreo.</summary>
    internal static ISampleProvider BuildChain(AudioFileReader reader)
    {
        // AudioFileReader aplica su propio Volume; lo dejamos a 1 y controlamos la ganancia en el Voice.
        reader.Volume = 1f;

        ISampleProvider provider = reader;

        provider = provider.WaveFormat.Channels switch
        {
            1 => new MonoToStereoSampleProvider(provider),
            2 => provider,
            // Multicanal (5.1 y compañía): nos quedamos con los dos primeros canales.
            _ => new MultiplexingSampleProvider([provider], 2)
        };

        if (provider.WaveFormat.SampleRate != AudioFormat.SampleRate)
            provider = new WdlResamplingSampleProvider(provider, AudioFormat.SampleRate);

        return provider;
    }
}

/// <summary>Efecto corto, decodificado entero en memoria. Dispara sin tocar el disco.</summary>
public sealed class CachedSoundSource(float[] data, TimeSpan duration) : ISoundSource
{
    readonly float[] _data = data;

    public TimeSpan Duration { get; } = duration;

    public SoundStream Open(bool loop) => new(new CachedProvider(_data, loop), null);

    sealed class CachedProvider(float[] data, bool loop) : ISampleProvider
    {
        int _position;

        public WaveFormat WaveFormat => AudioFormat.MixFormat;

        public int Read(Span<float> buffer)
        {
            if (data.Length == 0) return 0;

            int written = 0;
            while (written < buffer.Length)
            {
                if (_position >= data.Length)
                {
                    if (!loop) break;
                    _position = 0;
                }

                int chunk = Math.Min(data.Length - _position, buffer.Length - written);
                data.AsSpan(_position, chunk).CopyTo(buffer[written..]);
                _position += chunk;
                written += chunk;
            }
            return written;
        }
    }
}

/// <summary>Pista larga: se lee del disco en cada reproducción en vez de ocupar RAM.</summary>
public sealed class StreamingSoundSource(string path, TimeSpan duration) : ISoundSource
{
    public TimeSpan Duration { get; } = duration;

    public SoundStream Open(bool loop)
    {
        var reader = new AudioFileReader(path);
        var chain = SoundSourceLoader.BuildChain(reader);
        return new SoundStream(new StreamingProvider(reader, chain, loop), reader);
    }

    sealed class StreamingProvider(AudioFileReader reader, ISampleProvider chain, bool loop) : ISampleProvider
    {
        public WaveFormat WaveFormat => chain.WaveFormat;

        public int Read(Span<float> buffer)
        {
            int written = 0;
            while (written < buffer.Length)
            {
                int read = chain.Read(buffer[written..]);
                if (read == 0)
                {
                    // Position == 0 significa que ni siquiera había nada que leer: cortamos para
                    // no quedarnos girando en vacío con un fichero ilegible.
                    if (!loop || reader.Position == 0) break;
                    reader.Position = 0;
                    continue;
                }
                written += read;
            }
            return written;
        }
    }
}
