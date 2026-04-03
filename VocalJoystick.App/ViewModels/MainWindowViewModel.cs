using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using VocalJoystick.Core.Interfaces;
using VocalJoystick.Core.Models;

namespace VocalJoystick.App.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly IProfileRepository _profileRepository;
    private readonly ISettingsRepository _settingsRepository;
    private readonly ILogger _logger;

    private string _microphoneStatus = "Inactive";
    private string _signalLevel = "Signal: –";
    private string _currentCommand = "Awaiting activation";
    private AppMode _currentMode = AppMode.Stopped;
    private UserProfileMetadata? _activeProfile;

    public MainWindowViewModel(
        IProfileRepository profileRepository,
        ISettingsRepository settingsRepository,
        ILogger logger)
    {
        _profileRepository = profileRepository;
        _settingsRepository = settingsRepository;
        _logger = logger;

        ConfigurationModeCommand = new DelegateCommand(() => ApplyMode(AppMode.Configuration, "Recording microphone for configuration", "Loading configuration canvas"));
        WorkingModeCommand = new DelegateCommand(() => ApplyMode(AppMode.Working, "Listening for trained patterns", "Ready to execute commands"));
        StopCommand = new DelegateCommand(() => ApplyMode(AppMode.Stopped, "Microphone paused", "Working loop paused"));
        SettingsCommand = new DelegateCommand(() => CurrentCommand = "Settings dialog placeholder");
    }

    public ICommand ConfigurationModeCommand { get; }
    public ICommand WorkingModeCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand SettingsCommand { get; }

    public string MicrophoneStatus
    {
        get => _microphoneStatus;
        private set => SetProperty(ref _microphoneStatus, value);
    }

    public string SignalLevel
    {
        get => _signalLevel;
        private set => SetProperty(ref _signalLevel, value);
    }

    public string CurrentCommand
    {
        get => _currentCommand;
        private set => SetProperty(ref _currentCommand, value);
    }

    public string CurrentProfileDisplay => _activeProfile?.DisplayName ?? "(no profile)";

    public string ModeDisplay => CurrentMode.ToString();

    public AppMode CurrentMode
    {
        get => _currentMode;
        private set
        {
            if (SetProperty(ref _currentMode, value))
            {
                OnPropertyChanged(nameof(ModeDisplay));
            }
        }
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        _activeProfile = await _profileRepository.GetActiveProfileAsync(cancellationToken).ConfigureAwait(true);
        if (_activeProfile is null)
        {
            _activeProfile = new UserProfileMetadata { DisplayName = "Default profile" };
            await _profileRepository.SaveProfileAsync(_activeProfile, cancellationToken).ConfigureAwait(true);
            await _profileRepository.SetActiveProfileAsync(_activeProfile, cancellationToken).ConfigureAwait(true);
        }

        OnPropertyChanged(nameof(CurrentProfileDisplay));

        var settings = await _settingsRepository.LoadSettingsAsync(cancellationToken).ConfigureAwait(true);
        CurrentMode = settings?.LastMode ?? AppMode.Stopped;
        UpdateSignalLevel(0);
        _logger.LogInfo("Main view model initialized");
    }

    public void UpdateSignalLevel(double level)
    {
        SignalLevel = $"Signal Level: {(int)(level * 100)}%";
    }

    private void ApplyMode(AppMode mode, string micState, string commandState)
    {
        CurrentMode = mode;
        MicrophoneStatus = micState;
        CurrentCommand = commandState;
        _logger.LogInfo($"Mode switched to {mode}");
        var snapshot = new SettingsSnapshot(mode, _activeProfile?.Id);
        _ = _settingsRepository.SaveSettingsAsync(snapshot, CancellationToken.None);
    }
}
