using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Soundboard.Views;

/// <summary>"#B4654A" -> brocha. Cachea porque la rejilla lo pide una vez por pad y por repintado.</summary>
public sealed class HexToBrushConverter : IValueConverter
{
    static readonly Dictionary<string, SolidColorBrush> Cache = [];

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hex = value as string;
        if (string.IsNullOrWhiteSpace(hex)) return Brushes.Transparent;

        if (Cache.TryGetValue(hex, out var cached)) return cached;

        try
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!);
            brush.Freeze();
            Cache[hex] = brush;
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

/// <summary>Una colección vacía (o nula) hace visible el mensaje de "aquí no hay nada todavía".</summary>
public sealed class EmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        int count = value switch
        {
            null => 0,
            System.Collections.ICollection collection => collection.Count,
            System.Collections.IEnumerable items => items.Cast<object>().Count(),
            _ => 1
        };
        return count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
