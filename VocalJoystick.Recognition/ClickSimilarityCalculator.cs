using System;
using System.Collections.Generic;
using System.Linq;
using VocalJoystick.Core.Models;

namespace VocalJoystick.Recognition;

public sealed class ClickSimilarityCalculator
{
    private const double MfccWeight = 0.45;
    private const double DurationWeight = 0.15;
    private const double AttackWeight = 0.10;
    private const double CentroidWeight = 0.10;
    private const double RolloffWeight = 0.05;
    private const double FlatnessWeight = 0.05;
    private const double ZeroCrossingWeight = 0.05;
    private const double RmsWeight = 0.05;
    private const double MfccAlpha = 1.0;

    public ClickSimilarityResult Calculate(ClickSampleMetrics sample, ClickPrototype prototype)
    {
        var featureSimilarities = new Dictionary<string, double>
        {
            ["Duration"] = ScalarSimilarity(sample.DurationMs, prototype.Duration),
            ["Attack"] = ScalarSimilarity(sample.AttackTimeMs, prototype.AttackTime),
            ["SpectralCentroid"] = ScalarSimilarity(sample.SpectralCentroid, prototype.SpectralCentroid),
            ["SpectralRolloff"] = ScalarSimilarity(sample.SpectralRolloff, prototype.SpectralRolloff),
            ["SpectralFlatness"] = ScalarSimilarity(sample.SpectralFlatness, prototype.SpectralFlatness),
            ["ZeroCrossing"] = ScalarSimilarity(sample.ZeroCrossingRate, prototype.ZeroCrossingRate),
            ["Rms"] = ScalarSimilarity(sample.RmsEnergy, prototype.RmsEnergy)
        };

        var mfccSim = ComputeMfccSimilarity(sample.MfccCoefficients, prototype.MfccMean);
        var overall = DurationWeight * featureSimilarities["Duration"]
            + AttackWeight * featureSimilarities["Attack"]
            + CentroidWeight * featureSimilarities["SpectralCentroid"]
            + RolloffWeight * featureSimilarities["SpectralRolloff"]
            + FlatnessWeight * featureSimilarities["SpectralFlatness"]
            + ZeroCrossingWeight * featureSimilarities["ZeroCrossing"]
            + RmsWeight * featureSimilarities["Rms"]
            + MfccWeight * mfccSim;

        return new ClickSimilarityResult(overall, featureSimilarities, mfccSim);
    }

    private static double ScalarSimilarity(double value, ClickFeatureStats stats)
    {
        if (double.IsNaN(value) || double.IsNaN(stats.Mean))
        {
            return 0;
        }

        var tolerance = Math.Max(stats.Tolerance, 1e-6);
        var diff = Math.Abs(value - stats.Mean);
        return Math.Max(0, 1 - diff / tolerance);
    }

    private static double ComputeMfccSimilarity(double[] sample, double[] prototype)
    {
        if (sample.Length == 0 || prototype.Length == 0)
        {
            return 0;
        }

        var length = Math.Min(sample.Length, prototype.Length);
        var sum = 0d;
        for (var i = 0; i < 13; i++)
        {
            var s = i < sample.Length ? sample[i] : 0;
            var p = i < prototype.Length ? prototype[i] : 0;
            var diff = s - p;
            sum += diff * diff;
        }

        var distance = Math.Sqrt(sum / 13);
        return Math.Exp(-MfccAlpha * distance);
    }
}

public sealed record ClickSimilarityResult(double OverallSimilarity, IReadOnlyDictionary<string, double> FeatureSimilarities, double MfccSimilarity);
