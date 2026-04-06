using System;
using System.Collections.Generic;
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
                var similarity = templateMetrics is not null
                    ? DirectionalSampleMetrics.CalculateSimilarity(sampleMetrics, templateMetrics)
                    : null;
                return (Action: kvp.Key, Similarity: similarity);
            })
            .ToList();

        var similarityLookup = scored.ToDictionary(tuple => tuple.Action, tuple => tuple.Similarity);
        var winner = scored
            .Where(tuple => tuple.Similarity.HasValue)
            .OrderByDescending(tuple => tuple.Similarity!.Value)
            .FirstOrDefault();

        if (winner.Similarity is null)
        {
            return new DirectionalClassificationResult(null, 0, feature, false, similarityLookup);
        }

        var confidence = Math.Clamp(winner.Similarity.Value, 0, 1);
        var reliable = confidence >= _settings.ActivationConfidence;
        return new DirectionalClassificationResult(winner.Action, confidence, feature, reliable, similarityLookup);
    }
}
