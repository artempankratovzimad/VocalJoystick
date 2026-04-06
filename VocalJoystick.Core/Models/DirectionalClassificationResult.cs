using System.Collections.Generic;

namespace VocalJoystick.Core.Models;

public sealed record DirectionalClassificationResult(
    VocalAction? Action,
    double Confidence,
    DirectionalFeatureVector Feature,
    bool IsReliable,
    IReadOnlyDictionary<VocalAction, double?>? Similarities = null);
