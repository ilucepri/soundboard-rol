using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Soundboard.ViewModels;
using Soundboard.Views;

namespace Soundboard;

public partial class MainWindow : Window
{
    const int DwmwaUseImmersiveDarkMode = 20;

    // DllImport en vez de LibraryImport: el generador de este último exige AllowUnsafeBlocks en todo
    // el proyecto, y no compensa activarlo por una sola llamada.
    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);

    public MainWindow()
    {
        InitializeComponent();

        // Aunque la barra de título sea nuestra, el marco que pinta el gestor de ventanas
        // (bordes, sombra) sigue siendo del sistema y hay que pedirle el tema oscuro.
        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            int enabled = 1;
            DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref enabled, sizeof(int));
        };

        MaximizeFix.Apply(this);

        // E923 = restaurar, E922 = maximizar (Segoe Fluent Icons).
        StateChanged += (_, _) =>
            MaximizeGlyph.Text = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
    }

    MainViewModel? ViewModel => DataContext as MainViewModel;

    // ---- Barra de título --------------------------------------------------

    void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    void OnToggleMaximize(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    void OnClose(object sender, RoutedEventArgs e) => Close();

    // ---- Pads -------------------------------------------------------------

    void OnPadClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: PadViewModel pad })
            _ = pad.TriggerAsync();
    }

    /// <summary>
    /// El deslizador de volumen vive dentro del pad, y el pad entero es pulsable. Sin esto,
    /// ajustar el volumen dispararía también el sonido.
    /// </summary>
    void OnSwallowClick(object sender, MouseButtonEventArgs e) => e.Handled = true;

    // ---- Teclado ----------------------------------------------------------

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Handled || ViewModel is null) return;

        // Con el foco en un campo de texto las teclas son texto, no atajos.
        if (Keyboard.FocusedElement is TextBox) return;

        if (e.Key == Key.Escape)
        {
            ViewModel.StopAllCommand.Execute(null);
            e.Handled = true;
            return;
        }

        var typed = KeyToCharacter(e.Key);
        if (typed is not null && ViewModel.TriggerShortcut(typed))
            e.Handled = true;
    }

    /// <summary>Letras y dígitos, que es lo que admite el campo de atajo del editor.</summary>
    static string? KeyToCharacter(Key key) => key switch
    {
        >= Key.A and <= Key.Z => ((char)('A' + (key - Key.A))).ToString(),
        >= Key.D0 and <= Key.D9 => ((char)('0' + (key - Key.D0))).ToString(),
        >= Key.NumPad0 and <= Key.NumPad9 => ((char)('0' + (key - Key.NumPad0))).ToString(),
        _ => null
    };

    // ---- Arrastrar y soltar ------------------------------------------------

    void OnDragOverFiles(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    void OnDropFiles(object sender, DragEventArgs e)
    {
        if (ViewModel is null) return;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths) return;

        ViewModel.AddFiles(paths);
        e.Handled = true;
    }
}
