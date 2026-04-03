using System;
using System.Collections.Generic;
using System.Linq;
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
    private readonly IAudioCaptureService _audioCaptureService;
    private readonly SynchronizationContext _uiContext;

    private string _microphoneStatus = "Inactive";
    private string _signalLevel = "Signal Level: 0%";
    private double _signalLevelPercent;
    private string _captureState = "Stopped";
    private string _currentCommand = "Awaiting activation";
    private AppMode _currentMode = AppMode.Stopped;
    private UserProfileMetadata? _activeProfile;
    private AppSettings _currentSettings = AppSettings.CreateDefault();
    private IReadOnlyList<ActionConfigurationStatus> _actionStatuses = Array.Empty<ActionConfigurationStatus>();
    private string? _selectedMicrophoneId;

    public MainWindowViewModel(
        IProfileRepository profileRepository,
        ISettingsRepository settingsRepository,
        IAudioCaptureService audioCaptureService,
        ILogger logger)
    {
        _profileRepository = profileRepository;
        _settingsRepository = settingsRepository;
        _audioCaptureService = audioCaptureService;
        _logger = logger;
        _uiContext = SynchronizationContext.Current ?? new SynchronizationContext();

        _audioCaptureService.SignalLevelUpdated += (_, level) => _uiContext.Post(_ => UpdateSignalLevel(level), null);

        ConfigurationModeCommand = new DelegateCommand(() =>
        {
            ApplyMode(AppMode.Configuration, "Recording microphone for configuration", "Loading configuration canvas");
            FireAndForget(StopCaptureAsync());
        });

        WorkingModeCommand = new DelegateCommand(() =>
        {
            ApplyMode(AppMode.Working, "Listening for trained patterns", "Ready to execute commands");
            FireAndForget(StartCaptureAsync());
        });

        StopCommand = new DelegateCommand(() =>
        {
            ApplyMode(AppMode.Stopped, "Microphone paused", "Working loop paused");
            FireAndForget(StopCaptureAsync());
        });

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

    public double SignalLevelPercent
    {
        get => _signalLevelPercent;
        private set => SetProperty(ref _signalLevelPercent, value);
    }

    public string CaptureState
    {
        get => _captureState;
        private set => SetProperty(ref _captureState, value);
    }

    public string CurrentCommand
    {
        get => _currentCommand;
        private set => SetProperty(ref _currentCommand, value);
    }

    public string CurrentProfileDisplay => _activeProfile?.DisplayName ?? "(no profile)";

    public IReadOnlyList<ActionConfigurationStatus> ActionStatuses => _actionStatuses;

    public IReadOnlyList<AudioDeviceInfo> AvailableMicrophones => _audioCaptureService.AvailableDevices;

    public string? SelectedMicrophoneId
    {
        get => _selectedMicrophoneId ?? _audioCaptureService.SelectedDevice?.Id;
        set
        {
            if (SetProperty(ref _selectedMicrophoneId, value))
            {
                FireAndForget(SelectMicrophoneAsync(value, CancellationToken.None));
            }
        }
    }

    public string SettingsSummary =>
        $"Mode: {CurrentSettings.LastMode}; Profile: {CurrentSettings.ActiveProfileId ?? "(none)"}; Mic: {SelectedMicrophoneName}";

    public AppSettings CurrentSettings
    {
        get => _currentSettings;
        private set
        {
            if (SetProperty(ref _currentSettings, value))
            {
                OnPropertyChanged(nameof(SettingsSummary));
            }
        }
    }

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
        var settings = await _settingsRepository.LoadSettingsAsync(cancellationToken).ConfigureAwait(true);
        CurrentSettings = settings;
        CurrentMode = settings.LastMode;

        _activeProfile = await _profileRepository.GetActiveProfileAsync(cancellationToken).ConfigureAwait(true);
        if (_activeProfile is null)
        {
            _activeProfile = new UserProfileMetadata { DisplayName = "Default profile" };
            await _profileRepository.SaveProfileAsync(_activeProfile, cancellationToken).ConfigureAwait(true);
            await _profileRepository.SetActiveProfileAsync(_activeProfile, cancellationToken).ConfigureAwait(true);
        }

        OnPropertyChanged(nameof(CurrentProfileDisplay));

        CurrentSettings = CurrentSettings.WithMode(CurrentMode, _activeProfile?.Id);
        var profile = _activeProfile ?? throw new InvalidOperationException("Active profile must exist");
        var configuration = await _profileRepository.LoadOrCreateProfileConfigurationAsync(profile, cancellationToken).ConfigureAwait(true);
        UpdateActionStatuses(configuration);

        await SelectMicrophoneAsync(CurrentSettings.SelectedMicrophoneId, cancellationToken).ConfigureAwait(true);
        UpdateSignalLevel(0);
        CaptureState = _audioCaptureService.IsCapturing ? "Capturing" : "Stopped";
        _logger.LogInfo("Main view model initialized");
    }

    public void UpdateSignalLevel(double level)
    {
        var percent = Math.Clamp(level, 0, 1) * 100;
        SignalLevelPercent = percent;
        SignalLevel = $"Signal Level: {(int)percent}%";
    }

    private void ApplyMode(AppMode mode, string micState, string commandState)
    {
        CurrentMode = mode;
        MicrophoneStatus = micState;
        CurrentCommand = commandState;
        _logger.LogInfo($"Mode switched to {mode}");
        CurrentSettings = CurrentSettings.WithMode(mode, _activeProfile?.Id);
        OnPropertyChanged(nameof(SettingsSummary));
        _ = _settingsRepository.SaveSettingsAsync(CurrentSettings, CancellationToken.None);
    }

    private async Task SelectMicrophoneAsync(string? deviceId, CancellationToken cancellationToken)
    {
        try
        {
            var wasCapturing = _audioCaptureService.IsCapturing;

            if (wasCapturing)
            {
                await _audioCaptureService.StopAsync(cancellationToken).ConfigureAwait(true);
            }

        await _audioCaptureService.SelectDeviceAsync(deviceId, cancellationToken).ConfigureAwait(true);
        _selectedMicrophoneId = _audioCaptureService.SelectedDevice?.Id;
        OnPropertyChanged(nameof(SelectedMicrophoneId));
        OnPropertyChanged(nameof(AvailableMicrophones));
        OnPropertyChanged(nameof(SelectedMicrophoneName));
        CurrentSettings = CurrentSettings.WithDevice(_audioCaptureService.SelectedDevice?.Id);
        OnPropertyChanged(nameof(SettingsSummary));
            await _settingsRepository.SaveSettingsAsync(CurrentSettings, cancellationToken).ConfigureAwait(true);

            if (wasCapturing)
            {
                await _audioCaptureService.StartAsync(cancellationToken).ConfigureAwait(true);
            }

            CaptureState = _audioCaptureService.IsCapturing ? "Capturing" : "Stopped";
            _logger.LogInfo($"Selected microphone: {SelectedMicrophoneName}");
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to select microphone", ex);
        }
    }

    private async Task StartCaptureAsync()
    {
        try
        {
            await SelectMicrophoneAsync(SelectedMicrophoneId, CancellationToken.None).ConfigureAwait(true);
            await _audioCaptureService.StartAsync(CancellationToken.None).ConfigureAwait(true);
            CaptureState = "Capturing";
            _logger.LogInfo($"Capture started ({SelectedMicrophoneName})");
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to start capture", ex);
        }
    }

    private async Task StopCaptureAsync()
    {
        if (!_audioCaptureService.IsCapturing)
        {
            CaptureState = "Stopped";
            return;
        }

        try
        {
            await _audioCaptureService.StopAsync(CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to stop capture", ex);
        }

        CaptureState = "Stopped";
        _logger.LogInfo("Capture stopped");
    }

    private void FireAndForget(Task task)
    {
        task.ContinueWith(t =>
        {
            if (t.Exception is not null)
            {
                _logger.LogError("Background task failed", t.Exception);
            }
        }, TaskScheduler.Current);
    }

    private void UpdateActionStatuses(ProfileConfiguration configuration)
    {
        var statuses = configuration.ActionConfigurations.Values
            .Select(config => new ActionConfigurationStatus(
                config.Action,
                config.IsConfigured,
                config.IsConfigured ? "Configured" : "Not configured"))
            .ToList();

        _actionStatuses = statuses;
        OnPropertyChanged(nameof(ActionStatuses));
    }

    public string SelectedMicrophoneName => _audioCaptureService.SelectedDevice?.Name ?? "(none)";
}
