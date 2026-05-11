using System;
using System.Linq;

namespace VocalJoystick.Core.Models;

public sealed record DirectionalSampleMetrics(
    double MfccMean,
    double MfccMin,
    double MfccMax,
    double MfccStdDev,
    double MfccRange,
    double FormantFirstHz,
    double FormantSecondHz,
    double FormantDeltaHz,
    double SpectralCentroid)
{
    public static DirectionalSampleMetrics? FromFeatureVector(DirectionalFeatureVector? feature)
    {
        if (feature is null)
        {
            return null;
        }

        var coefficients = feature.MfccCoefficients;
        double mfccMean;
        double mfccMin;
        double mfccMax;
        double mfccStdDev;
        double mfccRange;

        if (coefficients?.Length > 0)
        {
            mfccMean = coefficients.Average();
            mfccMin = coefficients.Min();
            mfccMax = coefficients.Max();
            mfccRange = mfccMax - mfccMin;
            var variance = coefficients.Sum(value => Math.Pow(value - mfccMean, 2)) / coefficients.Length;
            mfccStdDev = Math.Sqrt(Math.Max(0, variance));
        }
        else
        {
            mfccMean = 0;
            mfccMin = 0;
            mfccMax = 0;
            mfccStdDev = 0;
            mfccRange = 0;
        }
        var formantDelta = Math.Abs(feature.Formants.FirstFormantHz - feature.Formants.SecondFormantHz);
        return new DirectionalSampleMetrics(
            mfccMean,
            mfccMin,
            mfccMax,
            mfccStdDev,
            mfccRange,
            feature.Formants.FirstFormantHz,
            feature.Formants.SecondFormantHz,
            formantDelta,
            feature.SpectralCentroid);
    }

    public static double? CalculateSimilarity(DirectionalSampleMetrics? sample, DirectionalSampleMetrics? average)
    {
        if (sample is null || average is null)
        {
            return null;
        }

        var eMfcc = RelativeError(sample.MfccMean, average.MfccMean);
        var eDelta = RelativeError(sample.FormantDeltaHz, average.FormantDeltaHz);
        var eSpectral = RelativeError(sample.SpectralCentroid, average.SpectralCentroid);

        var error = 0.5 * eMfcc + 0.4 * eDelta + 0.1 * eSpectral;
        return Math.Max(0, 1 - error);
    }

    private static double RelativeError(double value, double reference)
    {
        var denominator = Math.Abs(reference);
        if (denominator < double.Epsilon)
        {
            return Math.Abs(value - reference);
        }

        return Math.Abs(value - reference) / denominator;
    }
}
