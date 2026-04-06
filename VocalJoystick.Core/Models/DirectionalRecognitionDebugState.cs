using System;

using System.Collections.Generic;

namespace VocalJoystick.Core.Models;

public sealed record DirectionalRecognitionDebugState(
    DateTimeOffset Timestamp,
    VocalAction? CandidateDirection,
    double CandidateConfidence,
    double PitchScore,
    double EnergyScore,
    double HoldSeconds,
    double ActivationThreshold,
    double HysteresisMargin,
    double ActivationHoldSeconds,
    double Rms,
    double? PitchHz,
    double PitchConfidence,
    bool VoiceActive,
    bool HasTemplates,
    string Status,
    IReadOnlyDictionary<VocalAction, double?>? Similarities = null)
{
    public static DirectionalRecognitionDebugState Idle { get; } = new(
        DateTimeOffset.MinValue,
        null,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        null,
        0,
        false,
        false,
        "Idle");
}
