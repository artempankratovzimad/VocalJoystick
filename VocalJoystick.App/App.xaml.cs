using System.Windows;
using VocalJoystick.App.DependencyInjection;
using VocalJoystick.App.ViewModels;
using VocalJoystick.Core.Interfaces;
using VocalJoystick.Infrastructure.Logging;
using VocalJoystick.Infrastructure.Persistence;
using VocalJoystick.Infrastructure.Stubs;

namespace VocalJoystick.App;

public partial class App : Application
{
    private SimpleServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _serviceProvider = new SimpleServiceProvider();
        RegisterServices(_serviceProvider);

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }

    private static void RegisterServices(SimpleServiceProvider services)
    {
        services.RegisterSingleton<ILogger>(_ => new FileLogger());
        services.RegisterSingleton<IProfileRepository>(_ => new JsonProfileRepository());
        services.RegisterSingleton<ISettingsRepository>(_ => new JsonSettingsRepository());
        services.RegisterSingleton<IAudioCaptureService>(_ => new StubAudioCaptureService());
        services.RegisterSingleton<IVoiceActivityDetector>(_ => new StubVoiceActivityDetector());
        services.RegisterSingleton<IPitchDetector>(_ => new StubPitchDetector());
        services.RegisterSingleton<IFeatureExtractor>(_ => new StubFeatureExtractor());
        services.RegisterSingleton<ICommandRecognizer>(_ => new StubCommandRecognizer());
        services.RegisterSingleton<IMouseController>(_ => new StubMouseController());

        services.RegisterSingleton<MainWindowViewModel>(sp => new MainWindowViewModel(
            sp.GetRequiredService<IProfileRepository>(),
            sp.GetRequiredService<ISettingsRepository>(),
            sp.GetRequiredService<ILogger>()));

        services.RegisterSingleton<MainWindow>(sp => new MainWindow(sp.GetRequiredService<MainWindowViewModel>()));
    }
}
