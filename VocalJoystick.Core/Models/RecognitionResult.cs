using System;

namespace VocalJoystick.Core.Models;

public sealed class RecognitionResult
{
    public VocalAction Action { get; init; }
    public double Confidence { get; init; }
    public string? Description { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
