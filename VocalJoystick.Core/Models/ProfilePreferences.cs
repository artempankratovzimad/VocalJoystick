namespace VocalJoystick.Core.Models;

public sealed record ProfilePreferences(
    FrameProcessingSettings FrameSettings,
    double ClickConfidenceThreshold,
    double ClickMarginThreshold,
    int ClickCooldownMs,
    bool RequireClickSilence,
    double MovementStartSpeed,
    double MovementEndSpeed,
    double MovementAccelerationSeconds)
{
    public static ProfilePreferences CreateDefault() => new(
        FrameProcessingSettings.CreateDefault(),
        0.7,
        0.1,
        400,
        true,
        320,
        320,
        1);
}
