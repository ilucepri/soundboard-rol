using System.IO;
using System.Windows;
using System.Windows.Threading;
using Soundboard.Audio;
using Soundboard.Services;
using Soundboard.ViewModels;

namespace Soundboard;

public partial class App : Application
{
    AudioDeviceService? _deviceService;
    AudioEngine? _engine;
    MainViewModel? _viewModel;
    ProfileStore? _store;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnUnhandledException;

        _deviceService = new AudioDeviceService();
        _engine = new AudioEngine(_deviceService);
        _store = new ProfileStore();

        var window = new MainWindow();
        _viewModel = new MainViewModel(_store, _deviceService, _engine, new DialogService(window));
        window.DataContext = _viewModel;

        MainWindow = window;
        window.Show();
    }

    /// <summary>
    /// Un fallo suelto en la interfaz no debería tirar la app en mitad de una partida: lo dejamos
    /// escrito junto a los perfiles y seguimos.
    /// </summary>
    void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        var path = Path.Combine(_store?.RootPath ?? Path.GetTempPath(), "crash.log");

        try
        {
            File.AppendAllText(path,
                $"""

                ===== {DateTime.Now:yyyy-MM-dd HH:mm:ss} =====
                {e.Exception}

                """);
        }
        catch (Exception)
        {
            // Si ni siquiera podemos escribir el registro, no hay mucho más que hacer.
        }

        MessageBox.Show(
            $"Algo ha fallado, pero la app sigue abierta.\n\n{e.Exception.Message}\n\nDetalles en:\n{path}",
            "Soundboard de rol", MessageBoxButton.OK, MessageBoxImage.Warning);

        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _viewModel?.Dispose();
        _engine?.Dispose();
        _deviceService?.Dispose();
        base.OnExit(e);
    }
}
