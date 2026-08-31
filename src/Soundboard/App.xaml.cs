using System.Windows;
using Soundboard.Audio;
using Soundboard.Services;
using Soundboard.ViewModels;

namespace Soundboard;

public partial class App : Application
{
    AudioDeviceService? _deviceService;
    AudioEngine? _engine;
    MainViewModel? _viewModel;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _deviceService = new AudioDeviceService();
        _engine = new AudioEngine(_deviceService);

        var window = new MainWindow();
        _viewModel = new MainViewModel(new ProfileStore(), _deviceService, _engine, new DialogService(window));
        window.DataContext = _viewModel;

        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _viewModel?.Dispose();
        _engine?.Dispose();
        _deviceService?.Dispose();
        base.OnExit(e);
    }
}
