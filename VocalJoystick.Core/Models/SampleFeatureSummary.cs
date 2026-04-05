namespace VocalJoystick.Core.Models;

public sealed record SampleFeatureSummary(
    double Rms,
    double ZeroCrossingRate,
    double PitchHz,
    double PitchStdDev,
    double SpectralCentroid,
    double SpectralRolloff,
    double VoicedRatio,
    DirectionalFeatureVector? DirectionalFeature = null);
