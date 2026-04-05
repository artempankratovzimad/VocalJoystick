using System;
using System.Collections.Generic;
using System.Linq;
using VocalJoystick.Core.Interfaces;
using VocalJoystick.Core.Models;

namespace VocalJoystick.Infrastructure.Recognition;

public sealed class DirectionalTrainingService : IDirectionalTrainingService
{
    private const int MinSamples = 5;
    private const int MaxSamples = 12;
    private readonly Dictionary<VocalAction, List<DirectionalFeatureVector>> _samples = new();
    private readonly Dictionary<VocalAction, DirectionalTemplate> _templates = new();
    private readonly object _sync = new();

    public DirectionalTrainingService()
    {
        foreach (var action in DirectionalActions)
        {
            _samples[action] = new List<DirectionalFeatureVector>();
        }
    }

    public int MinimumSamples => MinSamples;
    public int MaximumSamples => MaxSamples;

    public void AddSample(VocalAction action, DirectionalFeatureVector feature)
    {
        if (!IsDirectionalAction(action) || feature is null || !ValidateSample(feature, out _))
        {
            return;
        }

        lock (_sync)
        {
            var list = _samples[action];
            if (list.Count >= MaxSamples)
            {
                list.RemoveAt(0);
            }

            list.Add(feature);
            if (TryBuildTemplate(action, out var template))
            {
                _templates[action] = template;
            }
        }
    }

    public bool TryBuildTemplate(VocalAction action, out DirectionalTemplate template)
    {
        lock (_sync)
        {
            if (!IsDirectionalAction(action))
            {
                template = default!;
                return false;
            }

            var list = _samples[action];
            if (list.Count < MinSamples)
            {
                template = default!;
                return false;
            }

            template = new DirectionalTemplate(action, BuildPrototype(list), list.Count, 0.6);
            return true;
        }
    }

    public IReadOnlyDictionary<VocalAction, DirectionalTemplate> GetTemplates()
    {
        lock (_sync)
        {
            return _templates.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
    }

    public bool ValidateSample(DirectionalFeatureVector feature, out string? failureReason)
    {
        if (feature is null)
        {
            failureReason = "Feature vector missing";
            return false;
        }

        if (!feature.IsVoiced)
        {
            failureReason = "Sample is not voiced";
            return false;
        }

        if (feature.Rms < 0.01)
        {
            failureReason = "RMS too low";
            return false;
        }

        failureReason = null;
        return true;
    }

    public void Reset()
    {
        lock (_sync)
        {
            foreach (var key in DirectionalActions)
            {
                _samples[key].Clear();
            }

            _templates.Clear();
        }
    }

    public IReadOnlyDictionary<VocalAction, int> GetSampleCounts()
    {
        lock (_sync)
        {
            return _samples.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Count);
        }
    }

    private static DirectionalFeatureVector BuildPrototype(List<DirectionalFeatureVector> samples)
    {
        var length = samples.First().MfccCoefficients.Length;
        var mfccAverage = new double[length];
        for (var i = 0; i < length; i++)
        {
            mfccAverage[i] = samples.Average(sample => sample.MfccCoefficients[i]);
        }

        var formantF1 = samples.Average(sample => sample.Formants.FirstFormantHz);
        var formantF2 = samples.Average(sample => sample.Formants.SecondFormantHz);
        var rms = samples.Average(sample => sample.Rms);
        var centroid = samples.Average(sample => sample.SpectralCentroid);
        var spread = samples.Average(sample => sample.SpectralSpread);
        var voiced = samples.Average(sample => sample.IsVoiced ? 1 : 0) >= 0.5;
        var pitch = samples.Average(sample => sample.PitchHz);
        var pitchConf = samples.Average(sample => sample.PitchConfidence);
        var power = samples.Average(sample => sample.Power);

        return new DirectionalFeatureVector(mfccAverage, new FormantResult(formantF1, formantF2), rms, centroid, spread, voiced, pitch, pitchConf, power);
    }

    private static bool IsDirectionalAction(VocalAction action) => action is
        VocalAction.MoveUp or
        VocalAction.MoveDown or
        VocalAction.MoveLeft or
        VocalAction.MoveRight;

    private static readonly VocalAction[] DirectionalActions =
        { VocalAction.MoveUp, VocalAction.MoveDown, VocalAction.MoveLeft, VocalAction.MoveRight };
}
