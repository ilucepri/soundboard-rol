using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Soundboard.Views;

/// <summary>
/// "#B4654A" -> brocha. Con ConverterParameter (0..1) devuelve el color a esa opacidad, que es como
/// Nocturne pinta el fondo de los pads: color al 16 %, nunca el tono plano.
/// Cachea porque la rejilla lo pide una vez por pad y por repintado.
/// </summary>
public sealed class HexToBrushConverter : IValueConverter
{
    static readonly Dictionary<(string, byte), SolidColorBrush> Cache = [];

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hex = value as string;
        if (string.IsNullOrWhiteSpace(hex)) return Brushes.Transparent;

        byte alpha = 255;
        if (parameter is not null &&
            double.TryParse(parameter.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double fraction))
            alpha = (byte)Math.Clamp(Math.Round(fraction * 255), 0, 255);

        var key = (hex, alpha);
        if (Cache.TryGetValue(key, out var cached)) return cached;

        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex)!;
            var brush = new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
            brush.Freeze();
            Cache[key] = brush;
            return brush;
        }
        catch (Exception)
        {
            return Brushes.Transparent;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// Progreso 0..1 -> anchura en estrellas, para pintar la barra con dos columnas de un Grid sin
/// tener que saber el ancho real del pad. Con parámetro "Rest" devuelve lo que falta.
/// </summary>
public sealed class ProgressToStarConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        double progress = value is double d && double.IsFinite(d) ? Math.Clamp(d, 0, 1) : 0;
        bool rest = string.Equals(parameter as string, "Rest", StringComparison.Ordinal);
        // Un Grid con las dos columnas a cero reparte mal; un mínimo diminuto lo evita.
        return new GridLength(Math.Max(rest ? 1 - progress : progress, 0.0001), GridUnitType.Star);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>0..1 -> "80 %".</summary>
public sealed class PercentConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is double d ? $"{Math.Round(d * 100)} %" : "";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Visible sólo si hay texto. Con parámetro "Invert" se comporta al revés.</summary>
public sealed class TextToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool hasText = !string.IsNullOrWhiteSpace(value as string);
        if (string.Equals(parameter as string, "Invert", StringComparison.Ordinal)) hasText = !hasText;
        return hasText ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Visible cuando el booleano es false.</summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// Cero elementos hace visible el mensaje de "aquí no hay nada todavía".
/// Enlázalo siempre contra <c>Coleccion.Count</c>, no contra la colección: ObservableCollection
/// notifica cambios de Count, pero su propia identidad no cambia nunca y el enlace no se releería.
/// </summary>
public sealed class EmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        int count = value switch
        {
            null => 0,
            int number => number,
            System.Collections.ICollection collection => collection.Count,
            System.Collections.IEnumerable items => items.Cast<object>().Count(),
            _ => 1
        };
        return count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
