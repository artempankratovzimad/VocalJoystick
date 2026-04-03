using System;
using System.Collections.Generic;
using System.Linq;
using VocalJoystick.Core.Models;

namespace VocalJoystick.Recognition;

public sealed class ShortClickTemplateClassifier
{
    private static readonly VocalAction[] ClickActions =
    {
        VocalAction.LeftClick,
        VocalAction.RightClick,
        VocalAction.DoubleClick
    };

    public RecognitionResult? Classify(SampleFeatureSummary summary, IReadOnlyDictionary<VocalAction, ActionTemplate> templates, double confidenceThreshold)
    {
        RecognitionResult? best = null;
        double bestConfidence = 0;

        foreach (var action in ClickActions)
        {
            if (!templates.TryGetValue(action, out var template))
            {
                continue;
            }

            if (template.SampleCount == 0)
            {
                continue;
            }

            var distance = CalculateDistance(summary, template);
            var confidence = 1d / (1d + distance);
            if (confidence <= confidenceThreshold)
            {
                continue;
            }

            if (best is null || confidence > bestConfidence)
            {
                bestConfidence = confidence;
                best = new RecognitionResult
                {
                    Action = action,
                    Confidence = confidence,
                    Description = $"Click template match ({action})"
                };
            }
        }

        return best;
    }

    private static double CalculateDistance(SampleFeatureSummary summary, ActionTemplate template)
    {
        var values = new[]
        {
            Normalize(summary.Rms, template.AverageRms),
            Normalize(summary.ZeroCrossingRate, template.AverageZeroCrossingRate),
            Normalize(summary.PitchHz, template.AveragePitchHz),
            Normalize(summary.SpectralCentroid, template.AverageSpectralCentroid),
            Normalize(summary.SpectralRolloff, template.AverageSpectralRolloff),
            Normalize(summary.VoicedRatio, template.VoicedRatio)
        };

        var squared = values.Sum(value => value * value);
        return Math.Sqrt(squared);
    }

    private static double Normalize(double value, double reference)
    {
        if (double.IsNaN(value) || double.IsNaN(reference))
        {
            return 1;
        }

        if (reference == 0)
        {
            return Math.Abs(value);
        }

        return Math.Abs((value - reference) / (reference + 1e-6));
    }
}
