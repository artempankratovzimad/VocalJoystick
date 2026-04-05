using System.Windows;
using VocalJoystick.App.DependencyInjection;
using VocalJoystick.App.ViewModels;
using VocalJoystick.Audio;
using VocalJoystick.Core.Interfaces;
using VocalJoystick.Core.Models;
using VocalJoystick.Infrastructure;
using VocalJoystick.Infrastructure.Logging;
using VocalJoystick.Infrastructure.Persistence;
using VocalJoystick.Infrastructure.Recording;
using VocalJoystick.Infrastructure.Recognition;
using VocalJoystick.Input;
using VocalJoystick.Recognition;
using VocalJoystick.Recognition.Directional;
using VocalJoystick.Recognition.FeatureExtraction;

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
        services.RegisterSingleton<IAppStorageLocation>(_ => new AppStorageLocation());
        services.RegisterSingleton<ILogger>(sp => new FileLogger(sp.GetRequiredService<IAppStorageLocation>()));
        services.RegisterSingleton<IProfileRepository>(sp => new JsonProfileRepository(sp.GetRequiredService<IAppStorageLocation>()));
        services.RegisterSingleton<ISettingsRepository>(sp => new JsonSettingsRepository(sp.GetRequiredService<IAppStorageLocation>()));
        services.RegisterSingleton<IAudioCaptureService>(_ => new NAudioCaptureService(_.GetRequiredService<ILogger>()));
        services.RegisterSingleton<IVoiceActivityDetector>(_ => new EnergyVoiceActivityDetector());
        services.RegisterSingleton<IPitchDetector>(_ => new AutocorrelationPitchDetector());
        services.RegisterSingleton<IFormantExtractor>(_ => new BasicFormantExtractor());
        services.RegisterSingleton<IMfccExtractor>(_ => new BasicMfccExtractor());
        services.RegisterSingleton<IFeatureExtractor>(sp => new FeatureExtractor(
            sp.GetRequiredService<IPitchDetector>(),
            sp.GetRequiredService<IVoiceActivityDetector>(),
            sp.GetRequiredService<IFormantExtractor>(),
            sp.GetRequiredService<IMfccExtractor>()));
        services.RegisterSingleton<IDirectionalTrainingService>(_ => new DirectionalTrainingService());
        services.RegisterSingleton<IDirectionalClassifier>(_ => new VowelDirectionalClassifier(new DirectionalRecognitionSettings()));
        services.RegisterSingleton<IDirectionalVowelRecognizer>(sp => new VowelDirectionalRecognizer(
            sp.GetRequiredService<IDirectionalClassifier>(),
            sp.GetRequiredService<IDirectionalTrainingService>(),
            sp.GetRequiredService<ILogger>(),
            new DirectionalRecognitionSettings()));
        services.RegisterSingleton<IShortClickRecognitionEngine>(sp => new ShortClickRecognitionEngine(sp.GetRequiredService<IFeatureExtractor>()));
        services.RegisterSingleton<ISampleRecorder>(sp => new SampleRecorder(
            sp.GetRequiredService<IAudioCaptureService>(),
            sp.GetRequiredService<IAppStorageLocation>(),
            sp.GetRequiredService<ILogger>(),
            sp.GetRequiredService<IFeatureExtractor>()));
        services.RegisterSingleton<IMouseController>(sp => new Win32MouseController(sp.GetRequiredService<ILogger>()));

        services.RegisterSingleton<MainWindowViewModel>(sp => new MainWindowViewModel(
            sp.GetRequiredService<IProfileRepository>(),
            sp.GetRequiredService<ISettingsRepository>(),
            sp.GetRequiredService<IAudioCaptureService>(),
            sp.GetRequiredService<IVoiceActivityDetector>(),
            sp.GetRequiredService<IPitchDetector>(),
            sp.GetRequiredService<ISampleRecorder>(),
            sp.GetRequiredService<IShortClickRecognitionEngine>(),
            sp.GetRequiredService<IDirectionalVowelRecognizer>(),
            sp.GetRequiredService<IMouseController>(),
            sp.GetRequiredService<IFeatureExtractor>(),
            sp.GetRequiredService<IDirectionalTrainingService>(),
            sp.GetRequiredService<ILogger>()));

        services.RegisterSingleton<MainWindow>(sp => new MainWindow(sp.GetRequiredService<MainWindowViewModel>()));
    }
}
