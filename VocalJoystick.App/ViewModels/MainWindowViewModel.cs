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
    private readonly IMouseController _mouseController;
    private readonly IFeatureExtractor _featureExtractor;
    private readonly DelegateCommand _configurationModeCommand;
    private readonly DelegateCommand _workingModeCommand;
    private readonly DelegateCommand _stopCommand;
    private readonly IDirectionalTrainingService _trainingService;
    private string _clickRecognitionStatus = "Awaiting click events";
    private double _clickRecognitionConfidence;
    private VocalAction? _recognizedDirection;
    private double _directionRecognitionConfidence;
    private DirectionalRecognitionDebugState _directionRecognitionDebug = DirectionalRecognitionDebugState.Idle;
    private CancellationTokenSource? _movementLoopCts;
    private static readonly TimeSpan MovementTickInterval = TimeSpan.FromMilliseconds(40);
    private static readonly VocalAction[] DirectionalActions =
        { VocalAction.MoveUp, VocalAction.MoveDown, VocalAction.MoveLeft, VocalAction.MoveRight };
    private string _statusMessage = "Select a mode to begin.";
    private string _statusSeverity = "Info";
    private string _bufferDebugInfo = "Waiting for microphone buffer...";
    private string _trainingDebugInfo = string.Empty;

    public MainWindowViewModel(
        IProfileRepository profileRepository,
        ISettingsRepository settingsRepository,
        IAudioCaptureService audioCaptureService,
        IVoiceActivityDetector voiceActivityDetector,
        IPitchDetector pitchDetector,
        ISampleRecorder sampleRecorder,
        IShortClickRecognitionEngine clickRecognitionEngine,
        IDirectionalVowelRecognizer directionalRecognizer,
        IMouseController mouseController,
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
        _mouseController = mouseController;
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
            CanStartWorking);

        _stopCommand = new DelegateCommand(
            () => FireAndForget(StopModeAsync("Microphone paused", "Working loop paused", "Capture stopped.")),
            () => CurrentMode != AppMode.Stopped);

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

    public DelegateCommand ConfigurationModeCommand => _configurationModeCommand;
    public DelegateCommand WorkingModeCommand => _workingModeCommand;
    public DelegateCommand StopCommand => _stopCommand;
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

    public int ClickCooldownMs
    {
        get => CurrentSettings.ClickCooldownMs;
        set => UpdateClickCooldownMs(value);
    }

    public double MovementSpeed
    {
        get => CurrentSettings.MovementSpeed;
        set => UpdateMovementSpeed(value);
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
                OnPropertyChanged(nameof(MovementSpeed));
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
        if (mode != AppMode.Working)
        {
            ResetDirectionState();
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

    private void UpdateMovementSpeed(double speed)
    {
        if (Math.Abs(CurrentSettings.MovementSpeed - speed) < 1e-3)
        {
            return;
        }

        CurrentSettings = CurrentSettings.WithMovementSpeed(speed);
        OnPropertyChanged(nameof(MovementSpeed));
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

    private static string FormatBufferDebug(VoiceActivityResult result, int frameCount, AudioBuffer buffer)
    {
        var vadLabel = result.IsActive ? "active" : "inactive";
        return $"Buffer {buffer.Samples.Length} samples / {frameCount} frames @ {buffer.SampleRate}Hz · RMS {result.Rms:F4} · VAD {vadLabel} · {buffer.Timestamp:HH:mm:ss.fff}";
    }

    private static string FormatEmptyBufferDebug(AudioBuffer buffer)
        => $"Buffer {buffer.Samples.Length} samples @ {buffer.SampleRate}Hz · no frames extracted · {buffer.Timestamp:HH:mm:ss.fff}";

    private async Task ProcessDirectionRecognitionAsync(AudioBuffer buffer, Frame frame, VoiceActivityResult voiceActivity, int frameCount)
    {
        try
        {
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
            if (CurrentMode == AppMode.Working && extraction.DirectionalFeature is not null)
            {
                var recognitionResult = _directionalRecognizer.Recognize(voiceActivity, pitchResult, extraction.DirectionalFeature, buffer.Timestamp);
                _uiContext.Post(_ => UpdateDirectionRecognitionState(recognitionResult), null);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Recognition buffer failed", ex);
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

        await HandleClickRecognitionAsync(result).ConfigureAwait(false);

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
        if (CurrentMode is AppMode.Idle or AppMode.Stopped)
        {
            if (HasDirectionalTemplates(configuration))
            {
                SetStatusMessage("Directional templates are ready; start Working mode.", "Info");
            }
            else
            {
                SetStatusMessage("Record MoveUp/Down/Left/Right before entering Working mode.", "Warning");
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

    private async Task StopModeAsync(string micState, string commandState, string statusMessage, string severity = "Info")
    {
        await StopCaptureAsync().ConfigureAwait(true);
        ApplyMode(AppMode.Stopped, micState, commandState);
        SetStatusMessage(statusMessage, severity);
    }

    private bool CanStartWorking()
    {
        return CurrentMode != AppMode.Working && _profileConfiguration is not null && HasDirectionalTemplates(_profileConfiguration);
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
        _stopCommand.RaiseCanExecuteChanged();
    }

    private void UpdateDirectionRecognitionState(DirectionalRecognitionResult result)
    {
        var previousDirection = RecognizedDirection;
        RecognizedDirection = result.ActiveDirection;
        DirectionRecognitionConfidence = result.Confidence;
        DirectionRecognitionDebug = result.Debug;

        if (previousDirection != RecognizedDirection)
        {
            var directionLabel = RecognizedDirection?.ToString() ?? "none";
            _logger.LogInfo($"Direction recognition: {directionLabel}, confidence {DirectionConfidenceDisplay}, status {DirectionRecognitionStatus}");
        }

        if (RecognizedDirection is not null)
        {
            if (CurrentMode == AppMode.Working)
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
                    await _mouseController.ClickAsync(result.Action, CancellationToken.None).ConfigureAwait(false);
                    break;
                case VocalAction.DoubleClick:
                    await _mouseController.DoubleClickAsync(CancellationToken.None).ConfigureAwait(false);
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

                var confidence = Math.Clamp(DirectionRecognitionConfidence, 0, 1);
                var intensity = MovementSpeed * confidence * MovementTickInterval.TotalSeconds;
                if (intensity > double.Epsilon)
                {
                    await _mouseController.MoveAsync(direction.Value, intensity, token).ConfigureAwait(false);
                }

                await Task.Delay(MovementTickInterval, token).ConfigureAwait(false);
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
