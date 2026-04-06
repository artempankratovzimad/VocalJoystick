using System;
using System.Linq;
using VocalJoystick.Core.Interfaces;
using VocalJoystick.Core.Models;

namespace VocalJoystick.Recognition.Directional;

public sealed class VowelDirectionalClassifier : IDirectionalClassifier
{
    private readonly DirectionalRecognitionSettings _settings;

    public VowelDirectionalClassifier(DirectionalRecognitionSettings? settings = null)
    {
        _settings = settings ?? new DirectionalRecognitionSettings();
    }

    public DirectionalClassificationResult Classify(DirectionalFeatureVector feature, IReadOnlyDictionary<VocalAction, DirectionalTemplate> templates)
    {
        ArgumentNullException.ThrowIfNull(feature);
        if (templates is null || templates.Count == 0)
        {
            return new DirectionalClassificationResult(null, 0, feature, false);
        }

        var sampleMetrics = DirectionalSampleMetrics.FromFeatureVector(feature);
        if (sampleMetrics is null)
        {
            return new DirectionalClassificationResult(null, 0, feature, false);
        }

        var scored = templates
            .Select(kvp =>
            {
                var templateMetrics = DirectionalSampleMetrics.FromFeatureVector(kvp.Value.Prototype);
                if (templateMetrics is null)
                {
                    return (kvp.Key, Similarity: (double?)null);
                }

                var similarity = DirectionalSampleMetrics.CalculateSimilarity(sampleMetrics, templateMetrics);
                return (kvp.Key, Similarity: similarity);
            })
            .Where(tuple => tuple.Similarity.HasValue)
            .OrderByDescending(tuple => tuple.Similarity!.Value)
            .FirstOrDefault();

        if (!scored.Similarity.HasValue)
        {
            return new DirectionalClassificationResult(null, 0, feature, false);
        }

        var confidence = Math.Clamp(scored.Similarity.Value, 0, 1);
        var reliable = confidence >= _settings.ActivationConfidence;
        return new DirectionalClassificationResult(scored.Key, confidence, feature, reliable);
    }
}
