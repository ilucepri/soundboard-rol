using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Soundboard.ViewModels;

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
        // WPF no sigue el tema oscuro en la barra de título; hay que pedírselo al gestor de ventanas.
        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            int enabled = 1;
            DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref enabled, sizeof(int));
        };
    }

    void OnDragOverFiles(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    void OnDropFiles(object sender, DragEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths) return;

        viewModel.AddFiles(paths);
        e.Handled = true;
    }
}
