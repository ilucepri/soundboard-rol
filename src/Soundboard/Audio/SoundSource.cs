using System.IO;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Soundboard.Audio;

/// <summary>Qué trozo del fichero suena y si se repite.</summary>
public readonly record struct SoundSlice(bool Loop, double Start, double End)
{
    public static SoundSlice Once => new(false, 0, 1);

    /// <summary>Deja el recorte en un rango usable aunque el JSON traiga valores raros.</summary>
    public SoundSlice Sanitized()
    {
        double start = Math.Clamp(double.IsFinite(Start) ? Start : 0, 0, 1);
        double end = Math.Clamp(double.IsFinite(End) ? End : 1, 0, 1);
        if (end <= start) (start, end) = (0, 1);
        return new SoundSlice(Loop, start, end);
    }

    public bool IsWholeFile => Start <= 0 && End >= 1;
}

/// <summary>Un stream listo para reproducir, junto con lo que haya que liberar al terminar.</summary>
public readonly record struct SoundStream(ISampleProvider Provider, IDisposable? Owner);

/// <summary>
/// Un fichero de audio preparado para sonar. Un mismo origen puede abrirse varias veces a la vez:
/// una por cada salida (Wave Link + cascos) y una por cada disparo simultáneo del pad.
/// </summary>
public interface ISoundSource
{
    TimeSpan Duration { get; }

    SoundStream Open(SoundSlice slice);

    /// <summary>
    /// Picos normalizados (0..1) del fichero entero, para dibujar la forma de onda del editor.
    /// Se calcula una vez y se guarda.
    /// </summary>
    float[] GetPeaks(int count);
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

    /// <summary>Reparte las muestras en <paramref name="count"/> cubos y se queda con el pico de cada uno.</summary>
    internal static float[] PeaksFrom(ReadOnlySpan<float> data, int count)
    {
        var peaks = new float[count];
        if (data.Length == 0) return peaks;

        double perBucket = (double)data.Length / count;
        float loudest = 0;

        for (int i = 0; i < count; i++)
        {
            int from = (int)(i * perBucket);
            int to = Math.Min(data.Length, (int)((i + 1) * perBucket));
            float peak = 0;
            for (int j = from; j < to; j++)
            {
                float value = Math.Abs(data[j]);
                if (value > peak) peak = value;
            }
            peaks[i] = peak;
            if (peak > loudest) loudest = peak;
        }

        // Normalizamos al pico del propio fichero: si no, un sonido flojo se dibuja plano.
        if (loudest > 0.0001f)
            for (int i = 0; i < count; i++) peaks[i] /= loudest;

        return peaks;
    }

    /// <summary>Índice de muestra a partir de una fracción, alineado a frame para no cruzar los canales.</summary>
    internal static int SampleIndex(int totalSamples, double fraction)
    {
        int index = (int)(totalSamples * Math.Clamp(fraction, 0, 1));
        return Math.Clamp(index - index % AudioFormat.Channels, 0, totalSamples);
    }
}

/// <summary>Efecto corto, decodificado entero en memoria. Dispara sin tocar el disco.</summary>
public sealed class CachedSoundSource(float[] data, TimeSpan duration) : ISoundSource
{
    readonly float[] _data = data;
    float[]? _peaks;

    public TimeSpan Duration { get; } = duration;

    public SoundStream Open(SoundSlice slice)
    {
        slice = slice.Sanitized();
        int start = SoundSourceLoader.SampleIndex(_data.Length, slice.Start);
        int end = SoundSourceLoader.SampleIndex(_data.Length, slice.End);
        if (end <= start) (start, end) = (0, _data.Length);
        return new SoundStream(new CachedProvider(_data, start, end, slice.Loop), null);
    }

    public float[] GetPeaks(int count) =>
        _peaks ??= SoundSourceLoader.PeaksFrom(_data, count);

    sealed class CachedProvider : ISampleProvider
    {
        readonly float[] _data;
        readonly int _start;
        readonly int _end;
        readonly bool _loop;
        int _position;

        public CachedProvider(float[] data, int start, int end, bool loop)
        {
            _data = data;
            _start = start;
            _end = end;
            _loop = loop;
            _position = start;
        }

        public WaveFormat WaveFormat => AudioFormat.MixFormat;

        public int Read(Span<float> buffer)
        {
            if (_end <= _start) return 0;

            int written = 0;
            while (written < buffer.Length)
            {
                if (_position >= _end)
                {
                    if (!_loop) break;
                    _position = _start;
                }

                int chunk = Math.Min(_end - _position, buffer.Length - written);
                _data.AsSpan(_position, chunk).CopyTo(buffer[written..]);
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
    float[]? _peaks;

    public TimeSpan Duration { get; } = duration;

    public SoundStream Open(SoundSlice slice)
    {
        slice = slice.Sanitized();

        var reader = new AudioFileReader(path);
        long startByte = ByteOffset(reader, slice.Start);
        if (startByte > 0) reader.Position = startByte;

        var chain = SoundSourceLoader.BuildChain(reader);

        // El límite se cuenta en muestras de salida: el remuestreo cambia el número respecto al
        // fichero, así que no sirve comparar posiciones del lector.
        long limit = slice.IsWholeFile
            ? -1
            : (long)((slice.End - slice.Start) * Duration.TotalSeconds
                     * AudioFormat.SampleRate * AudioFormat.Channels);

        return new SoundStream(new StreamingProvider(reader, chain, slice.Loop, startByte, limit), reader);
    }

    public float[] GetPeaks(int count)
    {
        if (_peaks is not null) return _peaks;

        using var reader = new AudioFileReader(path);
        var chain = SoundSourceLoader.BuildChain(reader);

        // Un fichero largo no cabe cómodo en memoria, así que acumulamos el pico cubo a cubo
        // mientras leemos, en vez de guardarlo entero.
        long total = Math.Max(1, (long)(Duration.TotalSeconds * AudioFormat.SampleRate * AudioFormat.Channels));
        var peaks = new float[count];
        var scratch = new float[AudioFormat.SampleRate];
        long position = 0;
        float loudest = 0;
        int read;

        while ((read = chain.Read(scratch)) > 0)
        {
            for (int i = 0; i < read; i++)
            {
                int bucket = (int)Math.Min(count - 1, (position + i) * count / total);
                float value = Math.Abs(scratch[i]);
                if (value > peaks[bucket]) peaks[bucket] = value;
                if (value > loudest) loudest = value;
            }
            position += read;
        }

        if (loudest > 0.0001f)
            for (int i = 0; i < count; i++) peaks[i] /= loudest;

        return _peaks = peaks;
    }

    static long ByteOffset(AudioFileReader reader, double fraction)
    {
        if (fraction <= 0) return 0;
        long offset = (long)(reader.Length * Math.Clamp(fraction, 0, 1));
        int align = reader.WaveFormat.BlockAlign;
        return Math.Clamp(offset - offset % align, 0, reader.Length);
    }

    sealed class StreamingProvider(
        AudioFileReader reader, ISampleProvider chain, bool loop, long startByte, long limit) : ISampleProvider
    {
        long _emitted;

        public WaveFormat WaveFormat => chain.WaveFormat;

        public int Read(Span<float> buffer)
        {
            int written = 0;
            while (written < buffer.Length)
            {
                int want = buffer.Length - written;

                if (limit > 0)
                {
                    long remaining = limit - _emitted;
                    if (remaining <= 0)
                    {
                        if (!loop) break;
                        Rewind();
                        continue;
                    }
                    want = (int)Math.Min(want, remaining);
                }

                int read = chain.Read(buffer.Slice(written, want));
                if (read == 0)
                {
                    // Si ya estábamos al principio es que no hay nada que leer: cortamos para no
                    // quedarnos girando en vacío con un fichero ilegible.
                    if (!loop || reader.Position == startByte) break;
                    Rewind();
                    continue;
                }

                written += read;
                _emitted += read;
            }
            return written;
        }

        void Rewind()
        {
            reader.Position = startByte;
            _emitted = 0;
        }
    }
}
