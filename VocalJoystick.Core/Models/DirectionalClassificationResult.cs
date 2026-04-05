namespace VocalJoystick.Core.Models;

public sealed record DirectionalClassificationResult(
    VocalAction? Action,
    double Confidence,
    DirectionalFeatureVector Feature,
    bool IsReliable);
