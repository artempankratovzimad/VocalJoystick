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

        var scored = templates
            .Select(kvp => (Action: kvp.Key, Template: kvp.Value, Distance: feature.DistanceTo(kvp.Value.Prototype)))
            .OrderBy(tuple => tuple.Distance)
            .FirstOrDefault();

        if (scored.Template is null)
        {
            return new DirectionalClassificationResult(null, 0, feature, false);
        }

        var maxDistance = templates.Max(t => feature.DistanceTo(t.Value.Prototype));
        var confidence = 1 - Math.Clamp(scored.Distance / Math.Max(maxDistance, 1), 0, 1);
        var reliable = confidence >= _settings.ActivationConfidence;
        return new DirectionalClassificationResult(scored.Action, confidence, feature, reliable);
    }
}
