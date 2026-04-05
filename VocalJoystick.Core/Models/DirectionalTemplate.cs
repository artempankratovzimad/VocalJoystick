namespace VocalJoystick.Core.Models;

public sealed record DirectionalTemplate(
    VocalAction Action,
    DirectionalFeatureVector Prototype,
    int SampleCount,
    double ConfidenceThreshold);
