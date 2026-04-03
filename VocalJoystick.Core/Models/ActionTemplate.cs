using System.Collections.Generic;
using System.Linq;

namespace VocalJoystick.Core.Models;

public sealed record ActionTemplate
{
    public static ActionTemplate Empty { get; } = new();

    public int SampleCount { get; init; }
    public double AverageDurationSeconds { get; init; }
    public double AverageRms { get; init; }
    public double AverageZeroCrossingRate { get; init; }
    public double AveragePitchHz { get; init; }
    public double PitchStdDev { get; init; }
    public double AverageSpectralCentroid { get; init; }
    public double AverageSpectralRolloff { get; init; }
    public double VoicedRatio { get; init; }

    public static ActionTemplate Create(IEnumerable<SampleMetadata>? samples)
    {
        if (samples is null)
        {
            return Empty;
        }

        var withFeatures = samples
            .Where(sample => sample.FeatureSummary is not null)
            .ToArray();

        if (withFeatures.Length == 0)
        {
            return Empty;
        }

        var featureSummaries = withFeatures.Select(sample => sample.FeatureSummary!).ToArray();
        var pitchValues = featureSummaries.Select(summary => summary.PitchHz).ToArray();
        var durationAvg = withFeatures.Average(sample => sample.DurationSeconds);

        return new ActionTemplate
        {
            SampleCount = withFeatures.Length,
            AverageDurationSeconds = durationAvg,
            AverageRms = featureSummaries.Average(summary => summary.Rms),
            AverageZeroCrossingRate = featureSummaries.Average(summary => summary.ZeroCrossingRate),
            AveragePitchHz = pitchValues.Any() ? pitchValues.Average() : 0,
            PitchStdDev = pitchValues.Length > 1 ? CalculateStandardDeviation(pitchValues, pitchValues.Average()) : 0,
            AverageSpectralCentroid = featureSummaries.Average(summary => summary.SpectralCentroid),
            AverageSpectralRolloff = featureSummaries.Average(summary => summary.SpectralRolloff),
            VoicedRatio = featureSummaries.Average(summary => summary.VoicedRatio)
        };
    }

    private static double CalculateStandardDeviation(double[] values, double mean)
    {
        if (values.Length < 2)
        {
            return 0;
        }

        var sumSquares = values.Sum(value => Math.Pow(value - mean, 2));
        return Math.Sqrt(sumSquares / (values.Length - 1));
    }
}
