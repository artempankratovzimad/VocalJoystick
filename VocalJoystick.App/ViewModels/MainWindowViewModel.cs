using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using VocalJoystick.App.Services;
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
    private AppMode _currentMode = AppMode.Idle;
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
    private readonly IDirectionalVowelRecognizer _directionalRecognizer;
    private readonly IExecutionActionSink _workingActionSink;
    private readonly IExecutionActionSink _testActionSink;
    private readonly ITestOverlayController _testOverlayController;
    private readonly IFeatureExtractor _featureExtractor;
    private readonly DelegateCommand _configurationModeCommand;
    private readonly DelegateCommand _workingModeCommand;
    private readonly DelegateCommand _testModeCommand;
    private readonly DelegateCommand _stopCommand;
    private readonly DelegateCommand _saveProfileCommand;
    private readonly DelegateCommand<VocalAction?> _viewSamplesCommand;
    private readonly IDirectionalTrainingService _trainingService;
    private string _clickRecognitionStatus = "Awaiting click events";
    private double _clickRecognitionConfidence;
    private double? _previousMfccMean;
    private readonly ClickPrototypeBuilder _clickPrototypeBuilder = new();
    private VocalAction? _recognizedDirection;
    private double _directionRecognitionConfidence;
    private DirectionalRecognitionDebugState _directionRecognitionDebug = DirectionalRecognitionDebugState.Idle;
    private CancellationTokenSource? _movementLoopCts;
    private TimeSpan _movementAccelerationElapsed;
    private VocalAction? _movementLoopDirection;
    private TimeSpan _currentSilentDuration = TimeSpan.Zero;
    private static readonly TimeSpan ExecutionMovementTickInterval = TimeSpan.FromMilliseconds(20);
    private static readonly TimeSpan ClickActivationDelay = TimeSpan.FromMilliseconds(500);
    private static readonly VocalAction[] DirectionalActions =
        { VocalAction.MoveUp, VocalAction.MoveDown, VocalAction.MoveLeft, VocalAction.MoveRight };
    private static readonly JsonSerializerOptions _directionalDebugJsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    private string _statusMessage = "Select a mode to begin.";
    private string _statusSeverity = "Info";
    private string _bufferDebugInfo = "Waiting for microphone buffer...";
    private string _trainingDebugInfo = string.Empty;
    private bool _directionalDebugEnabled;
    private bool _executionVoiceObserved;
    private DateTimeOffset? _executionVoiceDetectedAt;
    private DateTimeOffset? _executionCandidateDetectedAt;
    private DateTimeOffset? _executionDirectionActivatedAt;
    private bool _awaitingExecutionFirstMoveLog;

    public MainWindowViewModel(
        IProfileRepository profileRepository,
        ISettingsRepository settingsRepository,
        IAudioCaptureService audioCaptureService,
        IVoiceActivityDetector voiceActivityDetector,
        IPitchDetector pitchDetector,
        ISampleRecorder sampleRecorder,
        IShortClickRecognitionEngine clickRecognitionEngine,
        IDirectionalVowelRecognizer directionalRecognizer,
        CursorExecutionActionSink workingActionSink,
        OverlayExecutionActionSink testActionSink,
        ITestOverlayController testOverlayController,
        IFeatureExtractor featureExtractor,
        IDirectionalTrainingService trainingService,
        ILogger logger)
    {
        _profileRepository = profileRepository;
        _settingsRepository = settingsRepository;
        _audioCaptureService = audioCaptureService;
        _voiceActivityDetector = voiceActivityDetector;
        _pitchDetector = pitchDetector;
        _sampleRecorder = sampleRecorder;
        _clickRecognitionEngine = clickRecognitionEngine;
        _directionalRecognizer = directionalRecognizer;
        _workingActionSink = workingActionSink;
        _testActionSink = testActionSink;
        _testOverlayController = testOverlayController;
        _featureExtractor = featureExtractor;
        _trainingService = trainingService;
        _logger = logger;
        _uiContext = SynchronizationContext.Current ?? new SynchronizationContext();
        _selectedMicrophoneId = _audioCaptureService.SelectedDevice?.Id;

        _audioCaptureService.SignalLevelUpdated += (_, level) => _uiContext.Post(_ => UpdateSignalLevel(level), null);
        _audioCaptureService.BufferCaptured += OnBufferCaptured;

        _configurationModeCommand = new DelegateCommand(
            () => FireAndForget(StartConfigurationFlowAsync()),
            () => CurrentMode != AppMode.Configuration);

        _workingModeCommand = new DelegateCommand(
            () => FireAndForget(StartWorkingFlowAsync()),
            CanStartExecutionMode);

        _testModeCommand = new DelegateCommand(
            () => FireAndForget(StartTestFlowAsync()),
            CanStartExecutionMode);

        _stopCommand = new DelegateCommand(
            () => FireAndForget(StopModeAsync("Microphone paused", "Control loop paused", "Capture stopped.")),
            () => CurrentMode != AppMode.Stopped);

        _saveProfileCommand = new DelegateCommand(
            () => FireAndForget(SaveProfilePreferencesAsync()),
            () => _profileConfiguration is not null);

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
        _viewSamplesCommand = new DelegateCommand<VocalAction?>(
            action => FireAndForget(ShowDirectionalSamplesAsync(action)),
            action => action.HasValue && _actionStateMap[action.Value].IsDirectional && _actionStateMap[action.Value].HasSamples);
    }

    public DelegateCommand ConfigurationModeCommand => _configurationModeCommand;
    public DelegateCommand WorkingModeCommand => _workingModeCommand;
    public DelegateCommand TestModeCommand => _testModeCommand;
    public DelegateCommand StopCommand => _stopCommand;
    public DelegateCommand SaveProfileCommand => _saveProfileCommand;
    public DelegateCommand SettingsCommand { get; }

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
    public DelegateCommand<VocalAction?> ViewSamplesCommand => _viewSamplesCommand;
    public event EventHandler<SampleListRequestEventArgs>? SampleListRequested;

    public string CurrentProfileDisplay => _activeProfile?.DisplayName ?? "(no profile)";

    public IReadOnlyList<ActionConfigurationStatus> ActionStatuses => _actionStatuses;

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string StatusSeverity
    {
        get => _statusSeverity;
        private set => SetProperty(ref _statusSeverity, value);
    }

    public string BufferDebugInfo
    {
        get => _bufferDebugInfo;
        private set => SetProperty(ref _bufferDebugInfo, value);
    }

    public string TrainingDebugInfo
    {
        get => _trainingDebugInfo;
        private set => SetProperty(ref _trainingDebugInfo, value);
    }

    public bool DirectionalDebugEnabled
    {
        get => _directionalDebugEnabled;
        set => SetProperty(ref _directionalDebugEnabled, value);
    }

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

    public VocalAction? RecognizedDirection
    {
        get => _recognizedDirection;
        private set => SetProperty(ref _recognizedDirection, value);
    }

    public double DirectionRecognitionConfidence
    {
        get => _directionRecognitionConfidence;
        private set
        {
            if (SetProperty(ref _directionRecognitionConfidence, value))
            {
                OnPropertyChanged(nameof(DirectionConfidenceDisplay));
            }
        }
    }

    public DirectionalRecognitionDebugState DirectionRecognitionDebug
    {
        get => _directionRecognitionDebug;
        private set
        {
            if (SetProperty(ref _directionRecognitionDebug, value))
            {
                OnDirectionDebugChanged();
            }
        }
    }

    public string DirectionRecognitionStatus => DirectionRecognitionDebug.Status;

    public string DirectionConfidenceDisplay => DirectionRecognitionConfidence > 0
        ? $"{DirectionRecognitionConfidence:P0}"
        : "—";

    public string DirectionHoldDisplay => DirectionRecognitionDebug.HoldSeconds > 0
        ? $"{DirectionRecognitionDebug.HoldSeconds:F2}s / {DirectionRecognitionDebug.ActivationHoldSeconds:F2}s"
        : $"0.00s / {DirectionRecognitionDebug.ActivationHoldSeconds:F2}s";

    public string DirectionVoiceDisplay => DirectionRecognitionDebug.VoiceActive ? "Voice active" : "Voice inactive";

    public string DirectionPitchDisplay => DirectionRecognitionDebug.PitchHz.HasValue
        ? $"{DirectionRecognitionDebug.PitchHz.Value:F1} Hz ({DirectionRecognitionDebug.PitchConfidence:P0})"
        : "—";

    public string DirectionRmsDisplay => $"{DirectionRecognitionDebug.Rms:F3}";

    public string DirectionTemplateStatus => DirectionRecognitionDebug.HasTemplates ? "Directional templates loaded" : "Waiting for templates";

    public string DirectionCandidateDisplay => DirectionRecognitionDebug.CandidateDirection?.ToString() ?? "—";

    public double ClickConfidenceThreshold
    {
        get => CurrentSettings.ClickConfidenceThreshold;
        set => UpdateClickConfidenceThreshold(value);
    }

    public string ClickConfidenceThresholdDisplay => ClickConfidenceThreshold.ToString("F2");

    public double ClickMarginThreshold
    {
        get => CurrentSettings.ClickMarginThreshold;
        set => UpdateClickMarginThreshold(value);
    }

    public string ClickMarginThresholdDisplay => ClickMarginThreshold.ToString("F2");

    public int ClickCooldownMs
    {
        get => CurrentSettings.ClickCooldownMs;
        set => UpdateClickCooldownMs(value);
    }

    public bool RequireClickSilence
    {
        get => CurrentSettings.RequireClickSilence;
        set => UpdateRequireClickSilence(value);
    }

    public double MovementStartSpeed
    {
        get => CurrentSettings.MovementStartSpeed;
        set => UpdateMovementStartSpeed(value);
    }

    public double MovementEndSpeed
    {
        get => CurrentSettings.MovementEndSpeed;
        set => UpdateMovementEndSpeed(value);
    }

    public double MovementAccelerationSeconds
    {
        get => CurrentSettings.MovementAccelerationSeconds;
        set => UpdateMovementAccelerationSeconds(value);
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

    public string VadThresholdDisplay => VadThreshold.ToString("F3");

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

    public double FrameOverlapPercent
    {
        get => Math.Round(_frameSettings.Overlap * 100, 2);
        set
        {
            var clamped = Math.Clamp(value, 0, 95);
            var newOverlap = clamped / 100d;
            if (Math.Abs(_frameSettings.Overlap - newOverlap) < 1e-6)
            {
                return;
            }

            UpdateFrameSettings(_frameSettings.WithOverlap(newOverlap));
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
                OnPropertyChanged(nameof(MovementStartSpeed));
                OnPropertyChanged(nameof(MovementEndSpeed));
                OnPropertyChanged(nameof(MovementAccelerationSeconds));
                OnPropertyChanged(nameof(RequireClickSilence));
                OnPropertyChanged(nameof(ClickConfidenceThreshold));
                OnPropertyChanged(nameof(ClickMarginThreshold));
                OnPropertyChanged(nameof(ClickCooldownMs));
            }
        }
    }

    public string ModeDisplay => CurrentMode.ToString();

    private bool IsExecutionMode => IsExecutionModeValue(CurrentMode);

    private IExecutionActionSink CurrentActionSink => CurrentMode == AppMode.Test
        ? _testActionSink
        : _workingActionSink;

    private static bool IsExecutionModeValue(AppMode mode) => mode is AppMode.Working or AppMode.Test;

    public AppMode CurrentMode
    {
        get => _currentMode;
        private set
        {
            if (SetProperty(ref _currentMode, value))
            {
                OnPropertyChanged(nameof(ModeDisplay));
                UpdateCommandStates();
            }
        }
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var settings = await _settingsRepository.LoadSettingsAsync(cancellationToken).ConfigureAwait(true);
        if (settings.FrameSettings is null)
        {
            settings = settings.WithFrameSettings(FrameProcessingSettings.CreateDefault());
        }
        CurrentSettings = settings;
        _frameSettings = settings.FrameSettings;

        await EnsureProfileConfigurationLoadedAsync(cancellationToken).ConfigureAwait(true);
        OnPropertyChanged(nameof(CurrentProfileDisplay));
        _clickRecognitionEngine.Reset();

        await SelectMicrophoneAsync(CurrentSettings.SelectedMicrophoneId, cancellationToken).ConfigureAwait(true);
        UpdateSignalLevel(0);
        CaptureState = _audioCaptureService.IsCapturing ? "Capturing" : "Stopped";
        ApplyMode(AppMode.Idle, "Idle", "Awaiting mode selection");
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
                    UpdateDirectionalTemplateFromSample(action.Value, metadata);
                    if (_profileConfiguration is not null)
                    {
                        await _profileRepository.SaveProfileConfigurationAsync(_profileConfiguration, CancellationToken.None).ConfigureAwait(true);
                    }
                    state.UpdateMetadata(config.Samples, config.Template, config.DirectionalTemplate);
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
        state.UpdateMetadata(config.Samples, config.Template, config.DirectionalTemplate);
        RefreshActionStatuses();
        _logger.LogInfo($"Deleted recordings for {action.Value}");
        RefreshRecordingCommandStates();
    }

    private Task ShowDirectionalSamplesAsync(VocalAction? action)
    {
        if (action is null || _profileConfiguration is null)
        {
            return Task.CompletedTask;
        }

        var config = GetActionConfiguration(action.Value);
        if (config.Samples.Count == 0)
        {
            return Task.CompletedTask;
        }

        var averageMetrics = config.DirectionalMetricsAverage
            ?? DirectionalSampleMetrics.FromFeatureVector(config.DirectionalTemplate?.Prototype);
        var args = new SampleListRequestEventArgs(
            action.Value,
            config.Samples.ToList(),
            config.DirectionalTemplate,
            averageMetrics,
            sample => DeleteDirectionalSampleAsync(action.Value, sample));

        SampleListRequested?.Invoke(this, args);
        return Task.CompletedTask;
    }

    private async Task<bool> DeleteDirectionalSampleAsync(VocalAction action, SampleMetadata sample)
    {
        if (_profileConfiguration is null || _activeProfile is null || sample is null)
        {
            return false;
        }

        var config = GetActionConfiguration(action);
        var index = config.Samples.IndexOf(sample);
        if (index < 0)
        {
            return false;
        }

        config.Samples.RemoveAt(index);
        config.RefreshTemplate();
        config.DirectionalTemplate = null;

        try
        {
            await _sampleRecorder.DeleteSampleAsync(_activeProfile.Id, sample, CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to delete vocal sample", ex);
            config.Samples.Insert(index, sample);
            config.RefreshTemplate();
            SetStatusMessage("Unable to remove the selected sample.", "Error");
            return false;
        }

        WarmUpTrainingTemplates(_profileConfiguration);
        await _profileRepository.SaveProfileConfigurationAsync(_profileConfiguration, CancellationToken.None).ConfigureAwait(true);
        RefreshActionStatuses(_profileConfiguration);
        UpdateActionSampleStates();
        RefreshRecordingCommandStates();
        SetStatusMessage($"Removed sample from {action}.", "Info");
        return true;
    }

    private ActionConfiguration GetActionConfiguration(VocalAction action)
    {
        if (_profileConfiguration is null)
        {
            throw new InvalidOperationException("Profile configuration not loaded");
        }

        return _profileConfiguration.ActionConfigurations[action];
    }

    private void WarmUpTrainingTemplates(ProfileConfiguration configuration)
    {
        _trainingService.Reset();
        foreach (var action in DirectionalActions)
        {
            var configurationEntry = configuration.ActionConfigurations[action];
            foreach (var sample in configurationEntry.Samples)
            {
                var feature = sample.FeatureSummary?.DirectionalFeature;
                if (feature is not null)
                {
                    _trainingService.AddSample(action, feature);
                }
            }

            if (configurationEntry.DirectionalTemplate is null && _trainingService.TryBuildTemplate(action, out var template))
            {
                configurationEntry.DirectionalTemplate = template;
            }
        }

        RefreshTrainingDebugInfo();
    }

    private void RefreshTrainingDebugInfo()
    {
        if (_profileConfiguration is null)
        {
            TrainingDebugInfo = string.Empty;
            return;
        }

        var counts = _trainingService.GetSampleCounts();
        var entries = new List<string>();
        foreach (var action in DirectionalActions)
        {
            counts.TryGetValue(action, out var count);
            var template = _profileConfiguration.ActionConfigurations[action].DirectionalTemplate;
            var status = template is not null ? "ready" : "pending";
            entries.Add($"{action}: {count}/{_trainingService.MaximumSamples} samples ({status})");
        }

        TrainingDebugInfo = string.Join(" · ", entries);
    }

    private void UpdateDirectionalTemplateFromSample(VocalAction action, SampleMetadata metadata)
    {
        if (metadata.FeatureSummary?.DirectionalFeature is not { } feature || _profileConfiguration is null)
        {
            return;
        }

        _trainingService.AddSample(action, feature);
        if (_trainingService.TryBuildTemplate(action, out var template))
        {
            var config = GetActionConfiguration(action);
            config.DirectionalTemplate = template;
            _ = _profileRepository.SaveProfileConfigurationAsync(_profileConfiguration, CancellationToken.None);
        }

        RefreshTrainingDebugInfo();
    }

    private void ApplyMode(AppMode mode, string micState, string commandState)
    {
        _directionalRecognizer.UseLowLatencyMode(IsExecutionModeValue(mode));

        if (!IsExecutionModeValue(mode))
        {
            ResetDirectionState();
        }

        if (mode != AppMode.Test)
        {
            _testOverlayController.Stop();
        }

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
        if (_audioCaptureService.IsCapturing)
        {
            CaptureState = "Capturing";
            return;
        }

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
        ResetDirectionState();
    }

    private void UpdateFrameSettings(FrameProcessingSettings newSettings, bool persist = true)
    {
        _frameSettings = newSettings;
        CurrentSettings = CurrentSettings.WithFrameSettings(newSettings);
        OnPropertyChanged(nameof(FrameSize));
        OnPropertyChanged(nameof(FrameOverlap));
        OnPropertyChanged(nameof(FrameOverlapPercent));
        OnPropertyChanged(nameof(VadThreshold));
        OnPropertyChanged(nameof(VadThresholdDisplay));

        if (persist)
        {
            _ = _settingsRepository.SaveSettingsAsync(CurrentSettings, CancellationToken.None);
        }
    }

    private void ApplyProfilePreferences(ProfilePreferences? preferences)
    {
        if (preferences is null)
        {
            return;
        }

        _frameSettings = preferences.FrameSettings;
        OnPropertyChanged(nameof(FrameSize));
        OnPropertyChanged(nameof(FrameOverlap));
        OnPropertyChanged(nameof(FrameOverlapPercent));
        OnPropertyChanged(nameof(VadThreshold));
        OnPropertyChanged(nameof(VadThresholdDisplay));

        var updatedSettings = CurrentSettings with
        {
            FrameSettings = preferences.FrameSettings,
            ClickConfidenceThreshold = preferences.ClickConfidenceThreshold,
            ClickMarginThreshold = preferences.ClickMarginThreshold,
            ClickCooldownMs = preferences.ClickCooldownMs,
            MovementStartSpeed = preferences.MovementStartSpeed,
            MovementEndSpeed = preferences.MovementEndSpeed,
            MovementAccelerationSeconds = preferences.MovementAccelerationSeconds,
            RequireClickSilence = preferences.RequireClickSilence,
            LastUpdated = DateTimeOffset.UtcNow
        };

        CurrentSettings = updatedSettings;

        _ = _settingsRepository.SaveSettingsAsync(updatedSettings, CancellationToken.None);
    }

    private async Task SaveProfilePreferencesAsync()
    {
        if (_profileConfiguration is null)
        {
            SetStatusMessage("Profile configuration unavailable.", "Error");
            return;
        }

        var preferences = new ProfilePreferences(
            _frameSettings,
            ClickConfidenceThreshold,
            ClickMarginThreshold,
            ClickCooldownMs,
            RequireClickSilence,
            MovementStartSpeed,
            MovementEndSpeed,
            MovementAccelerationSeconds);

        _profileConfiguration.Preferences = preferences;

        try
        {
            await _profileRepository.SaveProfileConfigurationAsync(_profileConfiguration, CancellationToken.None).ConfigureAwait(true);
            SetStatusMessage("Profile preferences saved.", "Info");
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to save profile preferences", ex);
            SetStatusMessage("Unable to save profile preferences.", "Error");
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
        OnPropertyChanged(nameof(ClickConfidenceThresholdDisplay));
        _ = _settingsRepository.SaveSettingsAsync(CurrentSettings, CancellationToken.None);
    }

    private void UpdateClickMarginThreshold(double margin)
    {
        if (Math.Abs(CurrentSettings.ClickMarginThreshold - margin) < 1e-6)
        {
            return;
        }

        CurrentSettings = CurrentSettings.WithClickMarginThreshold(margin);
        OnPropertyChanged(nameof(ClickMarginThreshold));
        OnPropertyChanged(nameof(ClickMarginThresholdDisplay));
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

    private void UpdateMovementStartSpeed(double speed)
    {
        if (Math.Abs(CurrentSettings.MovementStartSpeed - speed) < 1e-3)
        {
            return;
        }

        CurrentSettings = CurrentSettings.WithMovementStartSpeed(speed);
        OnPropertyChanged(nameof(MovementStartSpeed));
        _ = _settingsRepository.SaveSettingsAsync(CurrentSettings, CancellationToken.None);
    }

    private void UpdateMovementEndSpeed(double speed)
    {
        if (Math.Abs(CurrentSettings.MovementEndSpeed - speed) < 1e-3)
        {
            return;
        }

        CurrentSettings = CurrentSettings.WithMovementEndSpeed(speed);
        OnPropertyChanged(nameof(MovementEndSpeed));
        _ = _settingsRepository.SaveSettingsAsync(CurrentSettings, CancellationToken.None);
    }

    private void UpdateMovementAccelerationSeconds(double seconds)
    {
        if (Math.Abs(CurrentSettings.MovementAccelerationSeconds - seconds) < 1e-3)
        {
            return;
        }

        CurrentSettings = CurrentSettings.WithMovementAccelerationSeconds(seconds);
        OnPropertyChanged(nameof(MovementAccelerationSeconds));
        _ = _settingsRepository.SaveSettingsAsync(CurrentSettings, CancellationToken.None);
    }

    private void UpdateRequireClickSilence(bool enabled)
    {
        if (CurrentSettings.RequireClickSilence == enabled)
        {
            return;
        }

        CurrentSettings = CurrentSettings.WithRequireClickSilence(enabled);
        OnPropertyChanged(nameof(RequireClickSilence));
        _ = _settingsRepository.SaveSettingsAsync(CurrentSettings, CancellationToken.None);
    }

    private void OnBufferCaptured(object? sender, AudioBufferEventArgs args)
    {
        var frames = FrameSegmenter.Segment(args.Buffer.Samples, _frameSettings, args.Buffer.SampleRate).ToList();
        if (!frames.Any())
        {
            BufferDebugInfo = FormatEmptyBufferDebug(args.Buffer);
            _logger.LogWarning("Buffer dropped – no frames available");
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

        FireAndForget(ProcessDirectionRecognitionAsync(args.Buffer, lastFrame, result, frames.Count));
        FireAndForget(RecognizeShortClicksAsync(args.Buffer));
    }

    private string FormatBufferDebug(VoiceActivityResult result, int frameCount, AudioBuffer buffer)
    {
        var vadLabel = result.IsActive ? "active" : "inactive";
        var hopSize = CalculateHopSize();
        var overlapPercent = _frameSettings.Overlap * 100;
        return $"Buffer {buffer.Samples.Length} samples / {frameCount} frames @ {buffer.SampleRate}Hz · Frame {FrameSize} samples · overlap {overlapPercent:F1}% (hop {hopSize}) · RMS {result.Rms:F4} · VAD {vadLabel} · {buffer.Timestamp:HH:mm:ss.fff}";
    }

    private int CalculateHopSize()
    {
        var overlap = Math.Clamp(_frameSettings.Overlap, 0, 0.95);
        return Math.Max(1, (int)Math.Round(_frameSettings.FrameSize * (1 - overlap)));
    }

    private static string FormatEmptyBufferDebug(AudioBuffer buffer)
        => $"Buffer {buffer.Samples.Length} samples @ {buffer.SampleRate}Hz · no frames extracted · {buffer.Timestamp:HH:mm:ss.fff}";

    private void UpdateSilentDuration(VoiceActivityResult voiceActivity, AudioBuffer buffer)
    {
        if (buffer.SampleRate <= 0 || buffer.Samples.Length == 0)
        {
            _currentSilentDuration = TimeSpan.Zero;
            return;
        }

        if (voiceActivity.Rms < ClickConfidenceThreshold)
        {
            _currentSilentDuration += GetBufferDuration(buffer);
        }
        else
        {
            _currentSilentDuration = TimeSpan.Zero;
        }
    }

    private static TimeSpan GetBufferDuration(AudioBuffer buffer)
        => buffer.SampleRate <= 0
            ? TimeSpan.Zero
            : TimeSpan.FromSeconds(buffer.Samples.Length / (double)buffer.SampleRate);

    private void ResetSilentDuration() => _currentSilentDuration = TimeSpan.Zero;

    private async Task ProcessDirectionRecognitionAsync(AudioBuffer buffer, Frame frame, VoiceActivityResult voiceActivity, int frameCount)
    {
        try
        {
            UpdateSilentDuration(voiceActivity, buffer);
            var pitchResult = await _pitchDetector.DetectPitchAsync(frame, CancellationToken.None).ConfigureAwait(true);
            _uiContext.Post(_ =>
            {
                if (pitchResult.IsVoiced)
                {
                    CurrentPitch = pitchResult.PitchHz;
                }
                else
                {
                    CurrentPitch = null;
                }

                PitchConfidence = pitchResult.Confidence;
            }, null);

            var extraction = await _featureExtractor.ExtractFeaturesAsync(buffer, CancellationToken.None).ConfigureAwait(true);
            BufferDebugInfo = FormatBufferDebug(voiceActivity, frameCount, buffer);
            DirectionalRecognitionResult? recognitionResult = null;
            if (IsExecutionMode)
            {
                if (voiceActivity.IsActive && !_executionVoiceObserved)
                {
                    _executionVoiceObserved = true;
                    _executionVoiceDetectedAt = DateTimeOffset.UtcNow;
                    _executionCandidateDetectedAt = null;
                    _executionDirectionActivatedAt = null;
                    _awaitingExecutionFirstMoveLog = false;
                    _logger.LogInfo($"Execution latency [mode={CurrentActionSink.SinkName}]: voice became active");
                }

                if (!voiceActivity.IsActive)
                {
                    ResetExecutionLatencyTracking();
                    _directionalRecognizer.Reset();
                    _uiContext.Post(_ => ResetDirectionState(), null);
                }
                else if (extraction.DirectionalFeature is not null)
                {
                    recognitionResult = _directionalRecognizer.Recognize(voiceActivity, pitchResult, extraction.DirectionalFeature, buffer.Timestamp);
                    _uiContext.Post(_ => UpdateDirectionRecognitionState(recognitionResult), null);
                }
            }

            LogDirectionalDebug(buffer, frameCount, voiceActivity, pitchResult, extraction, recognitionResult);
        }
        catch (Exception ex)
        {
            _logger.LogError("Recognition buffer failed", ex);
        }
    }

    private void LogDirectionalDebug(AudioBuffer buffer, int frameCount, VoiceActivityResult voiceActivity, PitchDetectionResult pitch, FeatureExtractionResult extraction, DirectionalRecognitionResult? recognitionResult)
    {
        if (!DirectionalDebugEnabled)
        {
            return;
        }

        var feature = extraction.DirectionalFeature;
        var metrics = DirectionalSampleMetrics.FromFeatureVector(feature);
        var debugState = recognitionResult?.Debug ?? DirectionalRecognitionDebugState.Idle;
        var candidate = debugState.CandidateDirection?.ToString() ?? "none";
        var activeDirection = recognitionResult?.ActiveDirection?.ToString() ?? "none";
        var pitchDisplay = pitch.PitchHz.HasValue ? $"{pitch.PitchHz.Value:F1}Hz" : "—";
        var mfccMean = metrics?.MfccMean.ToString("F3") ?? "n/a";
        var mfccMin = metrics?.MfccMin.ToString("F3") ?? "n/a";
        var mfccMax = metrics?.MfccMax.ToString("F3") ?? "n/a";
        var mfccStdDev = metrics?.MfccStdDev.ToString("F3") ?? "n/a";
        var mfccRange = metrics?.MfccRange.ToString("F3") ?? "n/a";
        var deltaMean = metrics is not null && _previousMfccMean.HasValue
            ? (metrics.MfccMean - _previousMfccMean.Value).ToString("F3")
            : "n/a";
        var firstFormant = metrics?.FormantFirstHz.ToString("F1") ?? "n/a";
        var secondFormant = metrics?.FormantSecondHz.ToString("F1") ?? "n/a";
        var formantDelta = metrics?.FormantDeltaHz.ToString("F1") ?? "n/a";
        var spectralCentroid = metrics?.SpectralCentroid.ToString("F1") ?? "n/a";
        var spectralSpread = feature?.SpectralSpread.ToString("F1") ?? "n/a";
        var spectralPower = feature?.Power.ToString("F1") ?? "n/a";
        var summaryRms = extraction.Summary.Rms;
        var summaryVoiced = extraction.Summary.VoicedRatio;
        var confidence = recognitionResult?.Confidence ?? 0;
        var status = debugState.Status;
        var featureFlag = feature is not null ? "yes" : "no";
        var bufferSamples = buffer.Samples.Length;
        var bufferRms = voiceActivity.Rms;

        var overlapPercent = _frameSettings.Overlap * 100;
        var hopSize = CalculateHopSize();
        var message = $"[DirectionalDebug] {buffer.Timestamp:HH:mm:ss.fff} buf={bufferSamples} rms={bufferRms:F3} frames={frameCount} vad={(voiceActivity.IsActive ? "active" : "inactive")} pitch={pitchDisplay} pitchConf={pitch.Confidence:P0} feature={featureFlag} MFCCmean={mfccMean} min={mfccMin} max={mfccMax} std={mfccStdDev} range={mfccRange} Δ={deltaMean} spread={spectralSpread} power={spectralPower} summaryRms={summaryRms:F3} voiced={summaryVoiced:P0} F1={firstFormant} F2={secondFormant} Δ={formantDelta} spectral={spectralCentroid} overlap={overlapPercent:F1}% hop={hopSize} candidate={candidate} active={activeDirection} confidence={confidence:P0} status={status}";
        var similarities = debugState.Similarities ?? new Dictionary<VocalAction, double?>();
        var comparisons = DirectionalActions.Select(action =>
        {
            similarities.TryGetValue(action, out var similarity);
            return similarity.HasValue
                ? $"{action}:{similarity.Value:P0}"
                : $"{action}:n/a";
        });
        var joined = string.Join(", ", comparisons);
        if (!string.IsNullOrEmpty(joined))
        {
            message += $" | similarities={joined}";
        }
        var similarityMap = DirectionalActions
            .ToDictionary(action => action.ToString(), action => similarities.TryGetValue(action, out var value) ? value : null);
            var entry = new DirectionalDebugEntry(
                buffer.Timestamp,
                bufferSamples,
                bufferRms,
                frameCount,
            voiceActivity.IsActive,
            extraction.FeatureVector.Length,
            feature is not null,
            pitch.PitchHz,
            pitch.Confidence,
            feature?.MfccCoefficients.Length ?? 0,
            metrics?.MfccMean,
            metrics?.MfccMin,
            metrics?.MfccMax,
            metrics?.MfccStdDev,
            metrics?.MfccRange,
            metrics is not null && _previousMfccMean.HasValue
                ? metrics.MfccMean - _previousMfccMean.Value
                : null,
            metrics?.SpectralCentroid,
            feature?.SpectralSpread,
            feature?.Power,
            summaryRms,
            summaryVoiced,
            metrics?.FormantFirstHz,
            metrics?.FormantSecondHz,
            metrics?.FormantDeltaHz,
            candidate,
            activeDirection,
            confidence,
            status,
            similarityMap,
            FrameSize,
            _frameSettings.Overlap,
            overlapPercent,
            hopSize);
        var json = JsonSerializer.Serialize(entry, _directionalDebugJsonOptions);
        message += $" | structured={json}";
        _logger.LogDebug(message);
        _previousMfccMean = metrics?.MfccMean;
    }

    private async Task RecognizeShortClicksAsync(AudioBuffer buffer)
    {
        if (_profileConfiguration is null)
        {
            return;
        }

        var prototypes = BuildClickPrototypes();
        if (prototypes.Count == 0)
        {
            return;
        }

        var cooldown = TimeSpan.FromMilliseconds(ClickCooldownMs);
        var result = await _clickRecognitionEngine.ProcessBufferAsync(buffer, prototypes, ClickConfidenceThreshold, ClickMarginThreshold, cooldown, DirectionalDebugEnabled, CancellationToken.None).ConfigureAwait(false);
        if (result is null)
        {
            return;
        }

            if (RequireClickSilence)
            {
                if (_currentSilentDuration < ClickActivationDelay)
                {
                    var remaining = ClickActivationDelay - _currentSilentDuration;
                    if (remaining < TimeSpan.Zero)
                    {
                        remaining = TimeSpan.Zero;
                    }

                    _logger.LogInfo($"Click suppressed: need {remaining.TotalSeconds:F1}s silence (current {_currentSilentDuration.TotalSeconds:F1}s)");
                    _uiContext.Post(_ =>
                    {
                        ClickRecognitionStatus = $"{result.Action} suppressed ({remaining.TotalSeconds:F1}s silence needed)";
                        ClickRecognitionConfidence = result.Confidence;
                    }, null);

                    return;
                }
            }

        await HandleClickRecognitionAsync(result).ConfigureAwait(false);
        ResetSilentDuration();

        _uiContext.Post(_ =>
        {
            ClickRecognitionStatus = $"{result.Action} detected";
            ClickRecognitionConfidence = result.Confidence;
        }, null);
    }

    private IReadOnlyDictionary<VocalAction, ClickPrototype> BuildClickPrototypes()
    {
        if (_profileConfiguration is null)
        {
            return new Dictionary<VocalAction, ClickPrototype>();
        }

        return _clickPrototypeBuilder.Build(_profileConfiguration.ActionConfigurations.Values);
    }

    private void UpdateDirectionalTemplates(ProfileConfiguration configuration)
    {
        _directionalRecognizer.UpdateTemplates(_trainingService.GetTemplates());
    }

    private static IReadOnlyDictionary<VocalAction, ActionTemplate> BuildDirectionalTemplates(ProfileConfiguration configuration)
    {
        var lookup = new Dictionary<VocalAction, ActionTemplate>();
        foreach (var action in DirectionalActions)
        {
            if (configuration.ActionConfigurations.TryGetValue(action, out var config) && config.Template.SampleCount > 0)
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

    private void RefreshViewSamplesCommandState()
    {
        _viewSamplesCommand.RaiseCanExecuteChanged();
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
        UpdateDirectionalTemplates(configuration);
        UpdateCommandStates();
        RefreshViewSamplesCommandState();
        if (CurrentMode is AppMode.Idle or AppMode.Stopped)
        {
            if (HasDirectionalTemplates(configuration))
            {
                SetStatusMessage("Directional templates are ready; start Working or Test mode.", "Info");
            }
            else
            {
                SetStatusMessage("Record MoveUp/Down/Left/Right before entering Working or Test mode.", "Warning");
            }
        }
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
                kvp.Value.UpdateMetadata(configuration.Samples, configuration.Template, configuration.DirectionalTemplate);
        }
        RefreshViewSamplesCommandState();
    }

    private async Task StartConfigurationFlowAsync()
    {
        if (CurrentMode == AppMode.Configuration)
        {
            SetStatusMessage("Already in configuration mode.", "Info");
            return;
        }

        try
        {
            await StopCaptureAsync().ConfigureAwait(true);
            _testOverlayController.Stop();
            await EnsureProfileConfigurationLoadedAsync(CancellationToken.None).ConfigureAwait(true);
            await StartCaptureAsync().ConfigureAwait(true);
            ApplyMode(AppMode.Configuration, "Recording microphone for configuration", "Ready to record action samples");
            SetStatusMessage("Configuration mode active; record each action sample.", "Info");
        }
        catch (Exception ex)
        {
            _logger.LogError("Configuration mode failed to start", ex);
            SetStatusMessage("Configuration mode could not start. Check the microphone.", "Error");
        }
    }

    private async Task StartWorkingFlowAsync()
    {
        if (CurrentMode == AppMode.Working)
        {
            SetStatusMessage("Already in working mode.", "Info");
            return;
        }

        try
        {
            await EnsureProfileConfigurationLoadedAsync(CancellationToken.None).ConfigureAwait(true);

            if (_profileConfiguration is null)
            {
                SetStatusMessage("Profile configuration unavailable.", "Error");
                return;
            }

            if (!HasDirectionalTemplates(_profileConfiguration))
            {
                SetStatusMessage("Record MoveUp, MoveDown, MoveLeft, and MoveRight before entering working mode.", "Warning");
                return;
            }

            await StopCaptureAsync().ConfigureAwait(true);
            _testOverlayController.Stop();
            await StartCaptureAsync().ConfigureAwait(true);
            ApplyMode(AppMode.Working, "Listening for trained patterns", "Ready to execute commands");
            SetStatusMessage("Working mode active; vocal gestures now move the cursor.", "Info");
        }
        catch (Exception ex)
        {
            _logger.LogError("Working mode failed to start", ex);
            SetStatusMessage("Unable to start working mode. Check microphone and templates.", "Error");
        }
    }

    private async Task StartTestFlowAsync()
    {
        if (CurrentMode == AppMode.Test)
        {
            SetStatusMessage("Already in test mode.", "Info");
            return;
        }

        try
        {
            await EnsureProfileConfigurationLoadedAsync(CancellationToken.None).ConfigureAwait(true);

            if (_profileConfiguration is null)
            {
                SetStatusMessage("Profile configuration unavailable.", "Error");
                return;
            }

            if (!HasDirectionalTemplates(_profileConfiguration))
            {
                SetStatusMessage("Record MoveUp, MoveDown, MoveLeft, and MoveRight before entering test mode.", "Warning");
                return;
            }

            await StopCaptureAsync().ConfigureAwait(true);
            _testOverlayController.Start();
            await StartCaptureAsync().ConfigureAwait(true);
            ApplyMode(AppMode.Test, "Listening for trained patterns", "Driving test overlay");
            SetStatusMessage("Test mode active; vocal gestures move the red ball without touching the cursor.", "Info");
        }
        catch (Exception ex)
        {
            _logger.LogError("Test mode failed to start", ex);
            SetStatusMessage("Unable to start test mode. Check microphone and templates.", "Error");
            _testOverlayController.Stop();
        }
    }

    private async Task StopModeAsync(string micState, string commandState, string statusMessage, string severity = "Info")
    {
        await StopCaptureAsync().ConfigureAwait(true);
        ApplyMode(AppMode.Stopped, micState, commandState);
        SetStatusMessage(statusMessage, severity);
    }

    private bool CanStartExecutionMode()
    {
        return CurrentMode is not AppMode.Working
            && CurrentMode is not AppMode.Test
            && _profileConfiguration is not null
            && HasDirectionalTemplates(_profileConfiguration);
    }

    private async Task EnsureProfileConfigurationLoadedAsync(CancellationToken cancellationToken)
    {
        if (_activeProfile is null)
        {
            _activeProfile = await _profileRepository.GetActiveProfileAsync(cancellationToken).ConfigureAwait(true);
            if (_activeProfile is null)
            {
                _activeProfile = new UserProfileMetadata { DisplayName = "Default profile" };
                await _profileRepository.SaveProfileAsync(_activeProfile, cancellationToken).ConfigureAwait(true);
                await _profileRepository.SetActiveProfileAsync(_activeProfile, cancellationToken).ConfigureAwait(true);
            }
        }

        var profile = _activeProfile ?? throw new InvalidOperationException("Active profile must exist");
        var configuration = await _profileRepository.LoadOrCreateProfileConfigurationAsync(profile, cancellationToken).ConfigureAwait(true);
        if (configuration.Version < ProfileConfiguration.CurrentVersion)
        {
            configuration.Version = ProfileConfiguration.CurrentVersion;
            await _profileRepository.SaveProfileConfigurationAsync(configuration, cancellationToken).ConfigureAwait(true);
        }
        _profileConfiguration = configuration;
        ApplyProfilePreferences(configuration.Preferences ?? ProfilePreferences.CreateDefault());
        RefreshSaveProfileCommandState();
        RefreshActionStatuses(configuration);
        UpdateActionSampleStates();
        WarmUpTrainingTemplates(configuration);
        _directionalRecognizer.UpdateTemplates(_trainingService.GetTemplates());
        OnPropertyChanged(nameof(CurrentProfileDisplay));
    }

    private static bool HasDirectionalTemplates(ProfileConfiguration configuration)
    {
        foreach (var action in DirectionalActions)
        {
            if (!configuration.ActionConfigurations.TryGetValue(action, out var config) || config.Template.SampleCount == 0)
            {
                return false;
            }
        }

        return true;
    }

    private void SetStatusMessage(string message, string severity)
    {
        StatusMessage = message;
        StatusSeverity = severity;
        switch (severity)
        {
            case "Warning":
                _logger.LogWarning(message);
                break;
            case "Error":
                _logger.LogError(message);
                break;
            default:
                _logger.LogInfo(message);
                break;
        }
    }

    private void UpdateCommandStates()
    {
        _configurationModeCommand.RaiseCanExecuteChanged();
        _workingModeCommand.RaiseCanExecuteChanged();
        _testModeCommand.RaiseCanExecuteChanged();
        _stopCommand.RaiseCanExecuteChanged();
    }

    private void RefreshSaveProfileCommandState() => _saveProfileCommand.RaiseCanExecuteChanged();

    private void UpdateDirectionRecognitionState(DirectionalRecognitionResult result)
    {
        var previousDirection = RecognizedDirection;
        var modeLabel = CurrentActionSink.SinkName;

        if (IsExecutionMode
            && result.Debug.CandidateDirection is not null
            && _executionCandidateDetectedAt is null)
        {
            _executionCandidateDetectedAt = DateTimeOffset.UtcNow;
            if (_executionVoiceDetectedAt.HasValue)
            {
                var candidateMs = (_executionCandidateDetectedAt.Value - _executionVoiceDetectedAt.Value).TotalMilliseconds;
                _logger.LogInfo($"Execution latency [mode={modeLabel}]: first candidate in {candidateMs:F0} ms");
            }
            else
            {
                _logger.LogInfo($"Execution latency [mode={modeLabel}]: first candidate detected");
            }
        }

        RecognizedDirection = result.ActiveDirection;
        DirectionRecognitionConfidence = result.Confidence;
        DirectionRecognitionDebug = result.Debug;

        if (IsExecutionMode
            && previousDirection is null
            && RecognizedDirection is not null)
        {
            _executionDirectionActivatedAt = DateTimeOffset.UtcNow;
            _awaitingExecutionFirstMoveLog = true;
            if (_executionVoiceDetectedAt.HasValue)
            {
                var activationMs = (_executionDirectionActivatedAt.Value - _executionVoiceDetectedAt.Value).TotalMilliseconds;
                _logger.LogInfo($"Execution latency [mode={modeLabel}]: direction activated in {activationMs:F0} ms");
            }
            else
            {
                _logger.LogInfo($"Execution latency [mode={modeLabel}]: direction activated");
            }
        }

        if (previousDirection != RecognizedDirection)
        {
            var directionLabel = RecognizedDirection?.ToString() ?? "none";
            _logger.LogInfo($"Direction recognition: {directionLabel}, confidence {DirectionConfidenceDisplay}, status {DirectionRecognitionStatus}");
        }

        if (RecognizedDirection is not null)
        {
            if (IsExecutionMode)
            {
                StartMovementLoop();
            }
        }
        else if (previousDirection is not null)
        {
            StopMovementLoop();
        }
    }

    private async Task HandleClickRecognitionAsync(RecognitionResult result)
    {
        try
        {
            switch (result.Action)
            {
                case VocalAction.LeftClick:
                case VocalAction.RightClick:
                    await CurrentActionSink.ClickAsync(result.Action, CancellationToken.None).ConfigureAwait(false);
                    break;
                case VocalAction.DoubleClick:
                    await CurrentActionSink.DoubleClickAsync(CancellationToken.None).ConfigureAwait(false);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Mouse action failed", ex);
        }
    }

    private void StartMovementLoop()
    {
        if (RecognizedDirection is null || _movementLoopCts is not null)
        {
            return;
        }

        var cts = new CancellationTokenSource();
        _movementLoopCts = cts;
        _movementAccelerationElapsed = TimeSpan.Zero;
        _movementLoopDirection = null;
        _ = MovementLoopAsync(cts);
    }

    private async Task MovementLoopAsync(CancellationTokenSource cts)
    {
        var token = cts.Token;
        try
        {
            while (!token.IsCancellationRequested)
            {
                var direction = RecognizedDirection;
                if (direction is null)
                {
                    break;
                }

                if (_movementLoopDirection != direction)
                {
                    _movementLoopDirection = direction;
                    _movementAccelerationElapsed = TimeSpan.Zero;
                }

                var settings = CurrentSettings;
                var accelerationTime = Math.Max(0, settings.MovementAccelerationSeconds);
                var progress = accelerationTime <= double.Epsilon
                    ? 1.0
                    : Math.Min(1.0, _movementAccelerationElapsed.TotalSeconds / accelerationTime);
                var easedProgress = progress * progress;
                var currentSpeed = settings.MovementStartSpeed + (settings.MovementEndSpeed - settings.MovementStartSpeed) * easedProgress;

                var confidence = Math.Clamp(DirectionRecognitionConfidence, 0, 1);
                var intensity = currentSpeed * confidence * ExecutionMovementTickInterval.TotalSeconds;
                if (intensity > double.Epsilon)
                {
                    await CurrentActionSink.MoveAsync(direction.Value, intensity, token).ConfigureAwait(false);
                    if (_awaitingExecutionFirstMoveLog)
                    {
                        _awaitingExecutionFirstMoveLog = false;
                        var now = DateTimeOffset.UtcNow;
                        var modeLabel = CurrentActionSink.SinkName;
                        if (_executionVoiceDetectedAt.HasValue)
                        {
                            var firstMoveMs = (now - _executionVoiceDetectedAt.Value).TotalMilliseconds;
                            if (_executionDirectionActivatedAt.HasValue)
                            {
                                var activationToMoveMs = (now - _executionDirectionActivatedAt.Value).TotalMilliseconds;
                                _logger.LogInfo($"Execution latency [mode={modeLabel}]: first move sent in {firstMoveMs:F0} ms (activation->move {activationToMoveMs:F0} ms)");
                            }
                            else
                            {
                                _logger.LogInfo($"Execution latency [mode={modeLabel}]: first move sent in {firstMoveMs:F0} ms");
                            }
                        }
                        else
                        {
                            _logger.LogInfo($"Execution latency [mode={modeLabel}]: first move sent");
                        }
                    }
                }

                _movementAccelerationElapsed += ExecutionMovementTickInterval;
                await Task.Delay(ExecutionMovementTickInterval, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError("Movement loop failed", ex);
        }
        finally
        {
            if (_movementLoopCts == cts)
            {
                _movementLoopCts = null;
            }

            _movementLoopDirection = null;
            _movementAccelerationElapsed = TimeSpan.Zero;
            cts.Dispose();
        }
    }

    private void StopMovementLoop()
    {
        _movementLoopCts?.Cancel();
    }

    private void ResetDirectionState()
    {
        RecognizedDirection = null;
        DirectionRecognitionConfidence = 0;
        DirectionRecognitionDebug = DirectionalRecognitionDebugState.Idle;
        StopMovementLoop();
        ResetExecutionLatencyTracking();
    }

    private void ResetExecutionLatencyTracking()
    {
        _executionVoiceObserved = false;
        _executionVoiceDetectedAt = null;
        _executionCandidateDetectedAt = null;
        _executionDirectionActivatedAt = null;
        _awaitingExecutionFirstMoveLog = false;
    }

    private void OnDirectionDebugChanged()
    {
        OnPropertyChanged(nameof(DirectionRecognitionStatus));
        OnPropertyChanged(nameof(DirectionConfidenceDisplay));
        OnPropertyChanged(nameof(DirectionHoldDisplay));
        OnPropertyChanged(nameof(DirectionVoiceDisplay));
        OnPropertyChanged(nameof(DirectionPitchDisplay));
        OnPropertyChanged(nameof(DirectionRmsDisplay));
        OnPropertyChanged(nameof(DirectionTemplateStatus));
        OnPropertyChanged(nameof(DirectionCandidateDisplay));
    }
}
