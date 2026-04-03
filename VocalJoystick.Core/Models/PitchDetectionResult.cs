namespace VocalJoystick.Core.Models;

public sealed record PitchDetectionResult(double? PitchHz, double Confidence, bool IsVoiced)
{
    public static PitchDetectionResult Unvoiced => new(null, 0, false);
}
