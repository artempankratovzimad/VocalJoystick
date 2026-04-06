using System;
using System.Linq;

namespace VocalJoystick.Core.Models;

public sealed record DirectionalSampleMetrics(
    double MfccMean,
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
        var mfccMean = coefficients?.Length > 0 ? coefficients.Average() : 0;
        var formantDelta = Math.Abs(feature.Formants.FirstFormantHz - feature.Formants.SecondFormantHz);
        return new DirectionalSampleMetrics(
            mfccMean,
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
