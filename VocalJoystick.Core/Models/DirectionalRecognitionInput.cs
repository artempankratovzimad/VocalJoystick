using System;

namespace VocalJoystick.Core.Models;

public sealed record DirectionalRecognitionInput(
    VoiceActivityResult VoiceActivity,
    PitchDetectionResult Pitch,
    DateTimeOffset Timestamp);
