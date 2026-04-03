using System;
using System.Collections.Generic;
using System.Linq;
using VocalJoystick.Core.Interfaces;
using VocalJoystick.Core.Models;

namespace VocalJoystick.Recognition;

public sealed class DirectionalCommandRecognizer : ICommandRecognizer
{
    private readonly DirectionalRecognitionSettings _settings;
    private IReadOnlyDictionary<VocalAction, ActionTemplate> _templates = new Dictionary<VocalAction, ActionTemplate>();
    private VocalAction? _activeAction;
    private double _activeConfidence;
    private VocalAction? _pendingAction;
    private double _pendingHoldSeconds;
    private DateTimeOffset _lastTimestamp = DateTimeOffset.MinValue;

    public DirectionalCommandRecognizer(DirectionalRecognitionSettings? settings = null)
    {
        _settings = settings ?? new DirectionalRecognitionSettings();
    }

    public DirectionalRecognitionResult Recognize(DirectionalRecognitionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var candidate = EvaluateCandidate(input);
        UpdateState(candidate, input);
        var debug = BuildDebugState(candidate, input);
        var confidence = _activeAction is not null ? _activeConfidence : candidate?.Confidence ?? 0;
        return new DirectionalRecognitionResult(_activeAction, confidence, debug);
    }

    public void Reset() => ResetState();

    public void UpdateTemplates(IReadOnlyDictionary<VocalAction, ActionTemplate> templates)
    {
        if (templates is null)
        {
            _templates = new Dictionary<VocalAction, ActionTemplate>();
            ResetState();
            return;
        }

        _templates = templates
            .Where(kvp => kvp.Value.SampleCount > 0 && IsDirectionalAction(kvp.Key))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        ResetState();
    }

    private static bool IsDirectionalAction(VocalAction action) => action is
        VocalAction.MoveUp or
        VocalAction.MoveDown or
        VocalAction.MoveLeft or
        VocalAction.MoveRight;

    private DirectionalCandidate? EvaluateCandidate(DirectionalRecognitionInput input)
    {
        if (!_templates.Any() || !input.VoiceActivity.IsActive)
        {
            return null;
        }

        DirectionalCandidate? best = null;
        foreach (var kvp in _templates)
        {
            var pitchScore = CalculatePitchScore(input.Pitch, kvp.Value);
            var energyScore = CalculateEnergyScore(input.VoiceActivity.Rms, kvp.Value);
            var totalWeight = _settings.PitchWeight + _settings.EnergyWeight + _settings.VoicedWeight;
            if (totalWeight <= double.Epsilon)
            {
                continue;
            }

            var confidence = (pitchScore * _settings.PitchWeight + energyScore * _settings.EnergyWeight + _settings.VoicedWeight) / totalWeight;
            var candidate = new DirectionalCandidate(kvp.Key, confidence, pitchScore, energyScore);
            if (best is null || candidate.Confidence > best.Confidence)
            {
                best = candidate;
            }
        }

        return best;
    }

    private void UpdateState(DirectionalCandidate? candidate, DirectionalRecognitionInput input)
    {
        var delta = _lastTimestamp == DateTimeOffset.MinValue
            ? 0
            : Math.Max(0, (input.Timestamp - _lastTimestamp).TotalSeconds);
        _lastTimestamp = input.Timestamp;

        if (!input.VoiceActivity.IsActive)
        {
            ResetActiveHold();
            return;
        }

        if (candidate is null)
        {
            _pendingAction = null;
            _pendingHoldSeconds = 0;
        }
        else if (candidate.Confidence >= _settings.ActivationConfidence)
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
            if (candidate is not null && candidate.Action == _pendingAction && _pendingHoldSeconds >= _settings.ActivationHoldSeconds && candidate.Confidence >= _settings.ActivationConfidence)
            {
                _activeAction = candidate.Action;
                _activeConfidence = candidate.Confidence;
            }
        }
        else
        {
            if (!ShouldRemainActive(candidate, input.VoiceActivity))
            {
                _activeAction = null;
                _activeConfidence = 0;
            }
            else if (candidate is not null && candidate.Action == _activeAction)
            {
                _activeConfidence = candidate.Confidence;
            }
        }
    }

    private bool ShouldRemainActive(DirectionalCandidate? candidate, VoiceActivityResult vad)
    {
        if (!vad.IsActive || candidate is null || candidate.Action != _activeAction)
        {
            return false;
        }

        var requiredConfidence = Math.Max(0, _settings.ActivationConfidence - _settings.HysteresisMargin);
        return candidate.Confidence >= requiredConfidence;
    }

    private DirectionalRecognitionDebugState BuildDebugState(DirectionalCandidate? candidate, DirectionalRecognitionInput input)
    {
        return new DirectionalRecognitionDebugState(
            input.Timestamp,
            candidate?.Action,
            candidate?.Confidence ?? 0,
            candidate?.PitchScore ?? 0,
            candidate?.EnergyScore ?? 0,
            _pendingHoldSeconds,
            _settings.ActivationConfidence,
            _settings.HysteresisMargin,
            _settings.ActivationHoldSeconds,
            input.VoiceActivity.Rms,
            input.Pitch.PitchHz,
            input.Pitch.Confidence,
            input.VoiceActivity.IsActive,
            _templates.Any(),
            DetermineStatus(candidate, input));
    }

    private string DetermineStatus(DirectionalCandidate? candidate, DirectionalRecognitionInput input)
    {
        if (!input.VoiceActivity.IsActive)
        {
            return "Voice inactive";
        }

        if (_activeAction is not null)
        {
            return "Active";
        }

        if (!_templates.Any())
        {
            return "No direction templates";
        }

        if (candidate is null)
        {
            return "Waiting for candidate";
        }

        if (candidate.Confidence >= _settings.ActivationConfidence)
        {
            return _pendingHoldSeconds >= _settings.ActivationHoldSeconds
                ? "Candidate held" : "Candidate pending";
        }

        return "Confidence below threshold";
    }

    private double CalculatePitchScore(PitchDetectionResult pitch, ActionTemplate template)
    {
        if (!pitch.IsVoiced || pitch.PitchHz is null || template.AveragePitchHz <= double.Epsilon)
        {
            return 0;
        }

        var tolerance = Math.Max(template.PitchStdDev * _settings.PitchToleranceMultiplier, _settings.MinimumPitchTolerance);
        var diff = Math.Abs(pitch.PitchHz.Value - template.AveragePitchHz);
        if (tolerance <= double.Epsilon)
        {
            return 0;
        }

        return 1 - Math.Clamp(diff / tolerance, 0, 1);
    }

    private double CalculateEnergyScore(double rms, ActionTemplate template)
    {
        var tolerance = Math.Max(template.AverageRms * _settings.EnergyToleranceMultiplier, _settings.MinimumEnergyTolerance);
        if (tolerance <= double.Epsilon)
        {
            return 0;
        }

        var diff = Math.Abs(rms - template.AverageRms);
        return 1 - Math.Clamp(diff / tolerance, 0, 1);
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

    private sealed record DirectionalCandidate(VocalAction Action, double Confidence, double PitchScore, double EnergyScore);
}
