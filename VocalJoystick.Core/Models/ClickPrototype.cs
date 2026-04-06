using System;
using System.Linq;

namespace VocalJoystick.Core.Models;

public sealed record ClickFeatureStats(double Mean, double Tolerance)
{
    public static ClickFeatureStats Create(double[] values, double minimumTolerance)
    {
        if (values.Length == 0)
        {
            return new ClickFeatureStats(0, minimumTolerance);
        }

        var mean = values.Average();
        var variance = values
            .Select(value => value - mean)
            .Select(diff => diff * diff)
            .Average();
        var stdDev = Math.Sqrt(variance);
        var tolerance = Math.Max(stdDev, minimumTolerance);
        return new ClickFeatureStats(mean, tolerance);
    }
}

public sealed record ClickPrototype(
    VocalAction Action,
    ClickFeatureStats Duration,
    ClickFeatureStats AttackTime,
    ClickFeatureStats SpectralCentroid,
    ClickFeatureStats SpectralRolloff,
    ClickFeatureStats SpectralFlatness,
    ClickFeatureStats ZeroCrossingRate,
    ClickFeatureStats RmsEnergy,
    double[] MfccMean);
