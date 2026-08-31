using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Soundboard.Services;
using Soundboard.ViewModels;

namespace Soundboard.Views;

public partial class EditSoundWindow : Window
{
    /// <summary>Barras de la forma de onda. El número lo fija el diseño.</summary>
    const int BarCount = 56;

    /// <summary>Separación mínima entre inicio y fin del recorte, para que siempre quede algo que sonar.</summary>
    const double MinSpan = 0.06;

    readonly PadViewModel _pad;
    readonly Border[] _bars = new Border[BarCount];

    bool _syncingTrim;

    public EditSoundWindow(PadViewModel pad)
    {
        InitializeComponent();
        _pad = pad;

        PathText.Text = pad.FilePath;
        NameBox.Text = pad.Name;
        ShortcutBox.Text = pad.Shortcut;
        VolumeSlider.Value = pad.Volume;
        LoopCheck.IsChecked = pad.Loop;

        ColorList.ItemsSource = MainViewModel.PadPalette;
        ColorList.SelectedItem = MainViewModel.PadPalette.Contains(pad.Color)
            ? pad.Color
            : MainViewModel.PadPalette[0];

        BuildWaveform();

        _syncingTrim = true;
        TrimStartSlider.Value = Math.Clamp(pad.TrimStart, 0, 0.6);
        TrimEndSlider.Value = Math.Clamp(pad.TrimEnd, 0.4, 1);
        _syncingTrim = false;

        UpdateTrimLabel();
        UpdateVolumeLabel();
        UpdateBarOpacity();

        Loaded += async (_, _) =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
            await LoadPeaksAsync();
        };
    }

    public PadEditResult Result { get; private set; } = PadEditResult.Cancel;

    // ---- Forma de onda ----------------------------------------------------

    void BuildWaveform()
    {
        var brush = PadBrush();
        for (int i = 0; i < BarCount; i++)
        {
            var bar = new Border
            {
                Background = brush,
                CornerRadius = new CornerRadius(1),
                Margin = new Thickness(1, 0, 1, 0),
                VerticalAlignment = VerticalAlignment.Center,
                // Hasta que lleguen los picos reales, una línea plana en vez de un hueco vacío.
                Height = 2
            };
            _bars[i] = bar;
            Waveform.Children.Add(bar);
        }
    }

    async Task LoadPeaksAsync()
    {
        var peaks = await _pad.GetPeaksAsync(BarCount);
        if (peaks is null) return;

        for (int i = 0; i < BarCount && i < peaks.Length; i++)
            _bars[i].Height = Math.Max(2, peaks[i] * 48);

        UpdateBarOpacity();
    }

    /// <summary>Las barras fuera del recorte se apagan al 20 %.</summary>
    void UpdateBarOpacity()
    {
        double start = TrimStartSlider.Value;
        double end = TrimEndSlider.Value;

        for (int i = 0; i < BarCount; i++)
        {
            double position = (i + 0.5) / BarCount;
            _bars[i].Opacity = position >= start && position <= end ? 1 : 0.2;
        }
    }

    Brush PadBrush()
    {
        var hex = ColorList.SelectedItem as string ?? _pad.Color;
        try
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!);
            brush.Freeze();
            return brush;
        }
        catch (Exception)
        {
            return Brushes.Gray;
        }
    }

    // ---- Eventos ----------------------------------------------------------

    void OnTrimChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // Los Slider disparan ValueChanged al parsearse el XAML, antes de que existan las barras.
        if (_syncingTrim || _bars[0] is null) return;

        // Empujamos el otro extremo en vez de bloquear el que se mueve: se siente mucho mejor.
        _syncingTrim = true;
        if (ReferenceEquals(sender, TrimStartSlider))
        {
            if (TrimEndSlider.Value - TrimStartSlider.Value < MinSpan)
                TrimEndSlider.Value = Math.Min(1, TrimStartSlider.Value + MinSpan);
        }
        else
        {
            if (TrimEndSlider.Value - TrimStartSlider.Value < MinSpan)
                TrimStartSlider.Value = Math.Max(0, TrimEndSlider.Value - MinSpan);
        }
        _syncingTrim = false;

        UpdateBarOpacity();
        UpdateTrimLabel();
    }

    void OnVolumeChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => UpdateVolumeLabel();

    void OnColorChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_bars[0] is null) return;
        var brush = PadBrush();
        foreach (var bar in _bars) bar.Background = brush;
    }

    void UpdateTrimLabel()
    {
        var total = _pad.Duration;
        var from = TimeSpan.FromSeconds(total.TotalSeconds * TrimStartSlider.Value);
        var to = TimeSpan.FromSeconds(total.TotalSeconds * TrimEndSlider.Value);
        TrimLabel.Text = $"Recorte — {Format(from)} – {Format(to)} de {Format(total)}";

        static string Format(TimeSpan value) => $"{(int)value.TotalMinutes}:{value.Seconds:00}";
    }

    void UpdateVolumeLabel() =>
        VolumeLabel.Text = $"Volumen — {Math.Round(VolumeSlider.Value * 100)} %";

    void OnShortcutChanged(object sender, TextChangedEventArgs e)
    {
        // Nos quedamos con el último carácter en mayúscula: el campo es una tecla, no un texto.
        var text = ShortcutBox.Text;
        if (text.Length == 0) return;

        var key = text[^1].ToString().ToUpperInvariant();
        if (key == text) return;

        ShortcutBox.Text = key;
        ShortcutBox.CaretIndex = 1;
    }

    void OnAccept(object sender, RoutedEventArgs e)
    {
        var name = NameBox.Text.Trim();
        _pad.Name = string.IsNullOrEmpty(name) ? _pad.FileName : name;
        _pad.Shortcut = ShortcutBox.Text.Trim().ToUpperInvariant();
        _pad.Volume = VolumeSlider.Value;
        _pad.Loop = LoopCheck.IsChecked == true;
        _pad.Color = ColorList.SelectedItem as string ?? _pad.Color;
        _pad.TrimStart = TrimStartSlider.Value;
        _pad.TrimEnd = TrimEndSlider.Value;

        Result = PadEditResult.Save;
        DialogResult = true;
    }

    void OnRemove(object sender, RoutedEventArgs e)
    {
        Result = PadEditResult.Remove;
        DialogResult = true;
    }

    void OnCancel(object sender, RoutedEventArgs e)
    {
        Result = PadEditResult.Cancel;
        DialogResult = false;
    }
}
