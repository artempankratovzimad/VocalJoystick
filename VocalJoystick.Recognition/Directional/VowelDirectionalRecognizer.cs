using System.Collections.Generic;
using System.Linq;
using VocalJoystick.Core.Interfaces;
using VocalJoystick.Core.Models;

namespace VocalJoystick.Recognition.Directional;

public sealed class VowelDirectionalRecognizer : IDirectionalVowelRecognizer
{
    private readonly IDirectionalClassifier _classifier;
    private readonly IDirectionalTrainingService _trainingService;
    private readonly ILogger _logger;
    private readonly DirectionalRecognitionSettings _settings;
    private IReadOnlyDictionary<VocalAction, DirectionalTemplate> _templates = new Dictionary<VocalAction, DirectionalTemplate>();
    private VocalAction? _activeAction;
    private double _activeConfidence;
    private VocalAction? _pendingAction;
    private double _pendingHoldSeconds;
    private DateTimeOffset _lastTimestamp = DateTimeOffset.MinValue;

    public VowelDirectionalRecognizer(
        IDirectionalClassifier classifier,
        IDirectionalTrainingService trainingService,
        ILogger logger,
        DirectionalRecognitionSettings? settings = null)
    {
        _classifier = classifier;
        _trainingService = trainingService;
        _settings = settings ?? new DirectionalRecognitionSettings();
        _logger = logger;
    }

    public DirectionalRecognitionResult Recognize(VoiceActivityResult voiceActivity, PitchDetectionResult pitch, DirectionalFeatureVector feature, DateTimeOffset timestamp)
    {
        ArgumentNullException.ThrowIfNull(voiceActivity);
        ArgumentNullException.ThrowIfNull(pitch);
        if (feature is null)
        {
            ResetState();
            return new DirectionalRecognitionResult(null, 0, DirectionalRecognitionDebugState.Idle);
        }

        var candidate = EvaluateCandidate(voiceActivity, feature);
        UpdateState(candidate, voiceActivity, timestamp);
        var debug = BuildDebugState(candidate, voiceActivity, pitch);
        var confidence = _activeAction is not null ? _activeConfidence : candidate?.Confidence ?? 0;
        return new DirectionalRecognitionResult(_activeAction, confidence, debug);
    }

    public void UpdateTemplates(IReadOnlyDictionary<VocalAction, DirectionalTemplate> templates)
    {
        _templates = templates ?? new Dictionary<VocalAction, DirectionalTemplate>();
        ResetState();
    }

    public void Reset()
    {
        ResetState();
    }

    private DirectionalClassificationResult? EvaluateCandidate(VoiceActivityResult voiceActivity, DirectionalFeatureVector feature)
    {
        if (!_templates.Any() || !voiceActivity.IsActive)
        {
            return null;
        }

        var result = _classifier.Classify(feature, _templates);
        return result.IsReliable ? result : result with { Confidence = 0 };
    }

    private void UpdateState(DirectionalClassificationResult? candidate, VoiceActivityResult voiceActivity, DateTimeOffset timestamp)
    {
        var delta = _lastTimestamp == DateTimeOffset.MinValue
            ? 0
            : Math.Max(0, (timestamp - _lastTimestamp).TotalSeconds);
        _lastTimestamp = timestamp;

        if (!voiceActivity.IsActive || candidate?.Action is null)
        {
            ResetActiveHold();
            return;
        }

        if (candidate.Confidence >= _settings.ActivationConfidence)
        {
            if (_pendingAction == candidate.Action)
            {
                _pendingHoldSeconds += delta;
            }
            else
            {
                _pendingAction = candidate.Action;
                _pendingHoldSeconds = delta;
            }
        }
        else
        {
            _pendingAction = null;
            _pendingHoldSeconds = 0;
        }

        if (_activeAction is null)
        {
            if (candidate is not null && candidate.Action == _pendingAction && _pendingHoldSeconds >= _settings.ActivationHoldSeconds)
            {
                _activeAction = candidate.Action;
                _activeConfidence = candidate.Confidence;
            }
        }
        else if (!ShouldRemainActive(candidate, voiceActivity))
        {
            ResetActiveHold();
        }
        else if (candidate is not null && candidate.Action == _activeAction)
        {
            _activeConfidence = candidate.Confidence;
        }
    }

    private bool ShouldRemainActive(DirectionalClassificationResult? candidate, VoiceActivityResult vad)
    {
        if (!vad.IsActive || candidate is null || candidate.Action != _activeAction)
        {
            return false;
        }

        var requiredConfidence = Math.Max(0, _settings.ActivationConfidence - _settings.HysteresisMargin);
        return candidate.Confidence >= requiredConfidence;
    }

    private DirectionalRecognitionDebugState BuildDebugState(DirectionalClassificationResult? candidate, VoiceActivityResult voiceActivity, PitchDetectionResult pitch)
    {
        return new DirectionalRecognitionDebugState(
            DateTimeOffset.UtcNow,
            candidate?.Action,
            candidate?.Confidence ?? 0,
            pitch.PitchHz ?? 0,
            voiceActivity.Rms,
            _pendingHoldSeconds,
            _settings.ActivationConfidence,
            _settings.HysteresisMargin,
            _settings.ActivationHoldSeconds,
            voiceActivity.Rms,
            pitch.PitchHz,
            pitch.Confidence,
            voiceActivity.IsActive,
            _templates.Any(),
            candidate is null ? "Candidate pending" : "Active");
    }

    private void ResetActiveHold()
    {
        _activeAction = null;
        _activeConfidence = 0;
        _pendingAction = null;
        _pendingHoldSeconds = 0;
    }

    private void ResetState()
    {
        ResetActiveHold();
        _lastTimestamp = DateTimeOffset.MinValue;
    }
}
