namespace VocalJoystick.Core.Models;

public sealed record ClickSampleMetrics(
    double DurationMs,
    double RmsEnergy,
    double PeakAmplitude,
    double AttackTimeMs,
    double DecayTimeMs,
    double ZeroCrossingRate,
    double SpectralCentroid,
    double SpectralRolloff,
    double SpectralBandwidth,
    double SpectralFlatness,
    double[] MfccCoefficients);
