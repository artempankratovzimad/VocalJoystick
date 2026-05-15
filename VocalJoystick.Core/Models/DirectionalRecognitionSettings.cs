namespace VocalJoystick.Core.Models;

public sealed record DirectionalRecognitionSettings
{
    public double ActivationConfidence { get; init; } = 0.62;
    public double ActivationHoldSeconds { get; init; } = 0.2;
    public double HysteresisMargin { get; init; } = 0.15;
    public double PitchWeight { get; init; } = 0.55;
    public double EnergyWeight { get; init; } = 0.35;
    public double VoicedWeight { get; init; } = 0.1;
    public double PitchToleranceMultiplier { get; init; } = 3.0;
    public double EnergyToleranceMultiplier { get; init; } = 0.35;
    public double MinimumPitchTolerance { get; init; } = 10;
    public double MinimumEnergyTolerance { get; init; } = 0.05;

    public static DirectionalRecognitionSettings CreateConservativeDefault() => new();

    public static DirectionalRecognitionSettings CreateWorkingLowLatency() => new()
    {
        ActivationConfidence = 0.35,
        ActivationHoldSeconds = 0,
        HysteresisMargin = 0.05
    };
}
