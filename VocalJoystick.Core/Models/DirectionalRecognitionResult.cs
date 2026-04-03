namespace VocalJoystick.Core.Models;

public sealed record DirectionalRecognitionResult(
    VocalAction? ActiveDirection,
    double Confidence,
    DirectionalRecognitionDebugState Debug)
{
    public bool IsActive => ActiveDirection is not null;
}
