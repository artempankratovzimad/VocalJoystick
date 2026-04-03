using System;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using VocalJoystick.Core.Interfaces;
using VocalJoystick.Core.Models;
using VocalJoystick.Recognition;

namespace VocalJoystick.App.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly IProfileRepository _profileRepository;
    private readonly ISettingsRepository _settingsRepository;
    private readonly IAudioCaptureService _audioCaptureService;
    private readonly IVoiceActivityDetector _voiceActivityDetector;
    private readonly IPitchDetector _pitchDetector;
    private readonly ISampleRecorder _sampleRecorder;
    private readonly ILogger _logger;
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
    private FrameProcessingSettings _frameSettings = FrameProcessingSettings.CreateDefault();
    private double _latestRms;
    private bool _vadActive;
    private string _vadState = "Inactive";
    private double? _currentPitch;
    private double _pitchConfidence;
    private string _pitchDisplay = "—";
    private ProfileConfiguration? _profileConfiguration;
    private readonly Dictionary<VocalAction, ActionSampleState> _actionStateMap;
    private readonly IShortClickRecognitionEngine _clickRecognitionEngine;
    private string _clickRecognitionStatus = "Awaiting click events";
    private double _clickRecognitionConfidence;

    public MainWindowViewModel(
        IProfileRepository profileRepository,
        ISettingsRepository settingsRepository,
        IAudioCaptureService audioCaptureService,
        IVoiceActivityDetector voiceActivityDetector,
        IPitchDetector pitchDetector,
        ISampleRecorder sampleRecorder,
        IShortClickRecognitionEngine clickRecognitionEngine,
        ILogger logger)
    {
        _profileRepository = profileRepository;
        _settingsRepository = settingsRepository;
        _audioCaptureService = audioCaptureService;
        _voiceActivityDetector = voiceActivityDetector;
        _pitchDetector = pitchDetector;
        _sampleRecorder = sampleRecorder;
        _clickRecognitionEngine = clickRecognitionEngine;
        _logger = logger;
        _uiContext = SynchronizationContext.Current ?? new SynchronizationContext();
        _selectedMicrophoneId = _audioCaptureService.SelectedDevice?.Id;

        _audioCaptureService.SignalLevelUpdated += (_, level) => _uiContext.Post(_ => UpdateSignalLevel(level), null);
        _audioCaptureService.BufferCaptured += OnBufferCaptured;

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

        _actionStateMap = Enum.GetValues<VocalAction>().ToDictionary(action => action, action => new ActionSampleState(action));
        ActionSampleStates = new ObservableCollection<ActionSampleState>(_actionStateMap.Values);
        StartRecordingCommand = new DelegateCommand<VocalAction?>(
            action => FireAndForget(StartRecordingForAction(action)),
            action => action.HasValue && !_actionStateMap[action.Value].IsRecording);
        StopRecordingCommand = new DelegateCommand<VocalAction?>(
            action => FireAndForget(StopRecordingForAction(action)),
            action => action.HasValue && _actionStateMap[action.Value].IsRecording);
        DeleteRecordingCommand = new DelegateCommand<VocalAction?>(
            action => FireAndForget(DeleteRecordingsForAction(action)),
            action => action.HasValue && !_actionStateMap[action.Value].IsRecording);
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

    public ObservableCollection<ActionSampleState> ActionSampleStates { get; }
    public DelegateCommand<VocalAction?> StartRecordingCommand { get; }
    public DelegateCommand<VocalAction?> StopRecordingCommand { get; }
    public DelegateCommand<VocalAction?> DeleteRecordingCommand { get; }

    public string CurrentProfileDisplay => _activeProfile?.DisplayName ?? "(no profile)";

    public IReadOnlyList<ActionConfigurationStatus> ActionStatuses => _actionStatuses;

    public string ClickRecognitionStatus
    {
        get => _clickRecognitionStatus;
        private set => SetProperty(ref _clickRecognitionStatus, value);
    }

    public double ClickRecognitionConfidence
    {
        get => _clickRecognitionConfidence;
        private set => SetProperty(ref _clickRecognitionConfidence, value);
    }

    public double ClickConfidenceThreshold
    {
        get => CurrentSettings.ClickConfidenceThreshold;
        set => UpdateClickConfidenceThreshold(value);
    }

    public int ClickCooldownMs
    {
        get => CurrentSettings.ClickCooldownMs;
        set => UpdateClickCooldownMs(value);
    }

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

    public string SelectedMicrophoneName => _audioCaptureService.SelectedDevice?.Name ?? "(none)";

    public double LatestRms
    {
        get => _latestRms;
        private set => SetProperty(ref _latestRms, value);
    }

    public bool VADActive
    {
        get => _vadActive;
        private set => SetProperty(ref _vadActive, value);
    }

    public string VadState
    {
        get => _vadState;
        private set => SetProperty(ref _vadState, value);
    }

    public double? CurrentPitch
    {
        get => _currentPitch;
        private set
        {
            if (SetProperty(ref _currentPitch, value))
            {
                PitchDisplay = value.HasValue ? $"{value.Value:F1} Hz" : "—";
            }
        }
    }

    public double PitchConfidence
    {
        get => _pitchConfidence;
        private set => SetProperty(ref _pitchConfidence, value);
    }

    public string PitchDisplay
    {
        get => _pitchDisplay;
        private set => SetProperty(ref _pitchDisplay, value);
    }

    public double VadThreshold
    {
        get => _frameSettings.VadThreshold;
        set => UpdateFrameSettings(_frameSettings.WithThreshold(value));
    }

    public int FrameSize
    {
        get => _frameSettings.FrameSize;
        set => UpdateFrameSettings(_frameSettings.WithFrameSize(value));
    }

    public double FrameOverlap
    {
        get => _frameSettings.Overlap;
        set => UpdateFrameSettings(_frameSettings.WithOverlap(value));
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
        _frameSettings = settings.FrameSettings;

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
        _profileConfiguration = configuration;
        RefreshActionStatuses(configuration);
        UpdateActionSampleStates();
        _clickRecognitionEngine.Reset();

        await SelectMicrophoneAsync(CurrentSettings.SelectedMicrophoneId, cancellationToken).ConfigureAwait(true);
        UpdateSignalLevel(0);
        CaptureState = _audioCaptureService.IsCapturing ? "Capturing" : "Stopped";
        _logger.LogInfo("Main view model initialized");
    }

    private void UpdateSignalLevel(double level)
    {
        var percent = Math.Clamp(level, 0, 1) * 100;
        SignalLevelPercent = percent;
        SignalLevel = $"Signal Level: {(int)percent}%";
    }

    private async Task StartRecordingForAction(VocalAction? action)
    {
        if (action is null || _activeProfile is null)
        {
            return;
        }

        var state = _actionStateMap[action.Value];
        if (state.IsRecording)
        {
            return;
        }

        state.IsRecording = true;
        RefreshRecordingCommandStates();
        await _sampleRecorder.StartRecordingAsync(_activeProfile.Id, action.Value, _frameSettings, CancellationToken.None);
        _logger.LogInfo($"Started recording for {action.Value}");
    }

    private async Task StopRecordingForAction(VocalAction? action)
    {
        if (action is null || _activeProfile is null)
        {
            return;
        }

        var state = _actionStateMap[action.Value];
        if (!state.IsRecording)
        {
            return;
        }

        var metadata = await _sampleRecorder.StopRecordingAsync(_activeProfile.Id, action.Value, CancellationToken.None);
        state.IsRecording = false;
        RefreshRecordingCommandStates();
            if (metadata is not null)
            {
                var config = GetActionConfiguration(action.Value);
                config.Samples.Add(metadata);
                config.RefreshTemplate();
                if (_profileConfiguration is not null)
                {
                    await _profileRepository.SaveProfileConfigurationAsync(_profileConfiguration, CancellationToken.None).ConfigureAwait(true);
                }
                state.UpdateMetadata(config.Samples, config.Template);
                RefreshActionStatuses();
            }
        _logger.LogInfo($"Stopped recording for {action.Value}");
    }

    private async Task DeleteRecordingsForAction(VocalAction? action)
    {
        if (action is null || _activeProfile is null)
        {
            return;
        }

        var state = _actionStateMap[action.Value];
        await _sampleRecorder.DeleteSamplesAsync(_activeProfile.Id, action.Value, CancellationToken.None);
        var config = GetActionConfiguration(action.Value);
        config.Samples.Clear();
        config.RefreshTemplate();
        if (_profileConfiguration is not null)
        {
            await _profileRepository.SaveProfileConfigurationAsync(_profileConfiguration, CancellationToken.None).ConfigureAwait(true);
        }
        state.UpdateMetadata(config.Samples, config.Template);
        RefreshActionStatuses();
        _logger.LogInfo($"Deleted recordings for {action.Value}");
        RefreshRecordingCommandStates();
    }

    private ActionConfiguration GetActionConfiguration(VocalAction action)
    {
        if (_profileConfiguration is null)
        {
            throw new InvalidOperationException("Profile configuration not loaded");
        }

        return _profileConfiguration.ActionConfigurations[action];
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

    private void UpdateFrameSettings(FrameProcessingSettings newSettings, bool persist = true)
    {
        _frameSettings = newSettings;
        CurrentSettings = CurrentSettings.WithFrameSettings(newSettings);
        OnPropertyChanged(nameof(FrameSize));
        OnPropertyChanged(nameof(FrameOverlap));
        OnPropertyChanged(nameof(VadThreshold));

        if (persist)
        {
            _ = _settingsRepository.SaveSettingsAsync(CurrentSettings, CancellationToken.None);
        }
    }

    private void UpdateClickConfidenceThreshold(double threshold)
    {
        if (Math.Abs(CurrentSettings.ClickConfidenceThreshold - threshold) < 1e-6)
        {
            return;
        }

        CurrentSettings = CurrentSettings.WithClickConfidenceThreshold(threshold);
        OnPropertyChanged(nameof(ClickConfidenceThreshold));
        _ = _settingsRepository.SaveSettingsAsync(CurrentSettings, CancellationToken.None);
    }

    private void UpdateClickCooldownMs(int cooldownMs)
    {
        if (CurrentSettings.ClickCooldownMs == cooldownMs)
        {
            return;
        }

        CurrentSettings = CurrentSettings.WithClickCooldownMs(cooldownMs);
        OnPropertyChanged(nameof(ClickCooldownMs));
        _ = _settingsRepository.SaveSettingsAsync(CurrentSettings, CancellationToken.None);
    }

    private void OnBufferCaptured(object? sender, AudioBufferEventArgs args)
    {
        var frames = FrameSegmenter.Segment(args.Buffer.Samples, _frameSettings, args.Buffer.SampleRate).ToList();
        if (!frames.Any())
        {
            return;
        }

        var lastFrame = frames.Last();
        var result = _voiceActivityDetector.Analyze(lastFrame, _frameSettings);
        _uiContext.Post(_ =>
        {
            LatestRms = result.Rms;
            VADActive = result.IsActive;
            VadState = result.IsActive ? "Active" : "Inactive";
            UpdateSignalLevel(result.Rms);
        }, null);

        FireAndForget(PredictPitchAsync(lastFrame));
        FireAndForget(RecognizeShortClicksAsync(args.Buffer));
    }

    private async Task PredictPitchAsync(Frame frame)
    {
        try
        {
            var result = await _pitchDetector.DetectPitchAsync(frame, CancellationToken.None).ConfigureAwait(true);
            _uiContext.Post(_ =>
            {
                if (result.IsVoiced)
                {
                    CurrentPitch = result.PitchHz;
                    PitchConfidence = result.Confidence;
                }
                else
                {
                    CurrentPitch = null;
                    PitchConfidence = 0;
                }
            }, null);
        }
        catch (Exception ex)
        {
            _logger.LogError("Pitch detection failed", ex);
        }
    }

    private async Task RecognizeShortClicksAsync(AudioBuffer buffer)
    {
        if (_profileConfiguration is null)
        {
            return;
        }

        var templates = BuildClickTemplates();
        if (templates.Count == 0)
        {
            return;
        }

        var cooldown = TimeSpan.FromMilliseconds(ClickCooldownMs);
        var result = await _clickRecognitionEngine.ProcessBufferAsync(buffer, templates, ClickConfidenceThreshold, cooldown, CancellationToken.None).ConfigureAwait(false);
        if (result is null)
        {
            return;
        }

        _uiContext.Post(_ =>
        {
            ClickRecognitionStatus = $"{result.Action} detected";
            ClickRecognitionConfidence = result.Confidence;
        }, null);
    }

    private IReadOnlyDictionary<VocalAction, ActionTemplate> BuildClickTemplates()
    {
        if (_profileConfiguration is null)
        {
            return new Dictionary<VocalAction, ActionTemplate>();
        }

        var lookup = new Dictionary<VocalAction, ActionTemplate>();
        foreach (var action in new[] { VocalAction.LeftClick, VocalAction.RightClick, VocalAction.DoubleClick })
        {
            if (_profileConfiguration.ActionConfigurations.TryGetValue(action, out var config) && config.Template.SampleCount > 0)
            {
                lookup[action] = config.Template;
            }
        }

        return lookup;
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

    private void RefreshRecordingCommandStates()
    {
        StartRecordingCommand.RaiseCanExecuteChanged();
        StopRecordingCommand.RaiseCanExecuteChanged();
        DeleteRecordingCommand.RaiseCanExecuteChanged();
    }

    private void RefreshActionStatuses()
    {
        if (_profileConfiguration is null)
        {
            return;
        }

        RefreshActionStatuses(_profileConfiguration!);
    }

    private void RefreshActionStatuses(ProfileConfiguration configuration)
    {
        foreach (var actionConfig in configuration.ActionConfigurations.Values)
        {
            actionConfig.RefreshTemplate();
        }

        var statuses = configuration.ActionConfigurations.Values
            .Select(config => new ActionConfigurationStatus(
                config.Action,
                config.HasSamples,
                config.HasSamples ? "Configured" : "Not configured"))
            .ToList();

        _actionStatuses = statuses;
        OnPropertyChanged(nameof(ActionStatuses));
    }

    private void UpdateActionSampleStates()
    {
        if (_profileConfiguration is null)
        {
            return;
        }

        foreach (var kvp in _actionStateMap)
        {
            var configuration = _profileConfiguration.ActionConfigurations[kvp.Key];
            kvp.Value.UpdateMetadata(configuration.Samples, configuration.Template);
        }
    }
}
