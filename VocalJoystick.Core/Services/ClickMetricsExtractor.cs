using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VocalJoystick.Core.Interfaces;
using VocalJoystick.Core.Models;

namespace VocalJoystick.Core.Services;

public sealed class ClickMetricsExtractor
{
    private readonly IFeatureExtractor _featureExtractor;

    public ClickMetricsExtractor(IFeatureExtractor featureExtractor)
    {
        _featureExtractor = featureExtractor;
    }

    public async Task<ClickMetricsExtractionResult> ExtractAsync(float[] samples, int sampleRate, CancellationToken cancellationToken)
    {
        var durationMs = samples.Length / (double)Math.Max(1, sampleRate) * 1000;
        var rms = ClickMetricsHelper.CalculateRms(samples);
        var peak = ClickMetricsHelper.GetPeakAmplitude(samples);
        var attackMs = ClickMetricsHelper.ComputeAttackTime(samples, sampleRate);
        var decayMs = ClickMetricsHelper.ComputeDecayTime(samples, sampleRate);
        var zeroCrossing = ClickMetricsHelper.CalculateZeroCrossingRate(samples);
        var analysisLength = Math.Min(samples.Length, ClickMetricsHelper.SpectrumAnalysisMaxLength);
        var magnitude = ClickMetricsHelper.CalculateMagnitudeSpectrum(samples, Math.Max(analysisLength, 2));
        var spectralCentroid = ClickMetricsHelper.CalculateSpectralCentroid(magnitude, sampleRate, Math.Max(analysisLength, 1));
        var spectralRolloff = ClickMetricsHelper.CalculateSpectralRolloff(magnitude, sampleRate, Math.Max(analysisLength, 1), 0.85);
        var spectralBandwidth = ClickMetricsHelper.CalculateSpectralBandwidth(magnitude, sampleRate, Math.Max(analysisLength, 1), spectralCentroid);
        var spectralFlatness = ClickMetricsHelper.CalculateSpectralFlatness(magnitude);

        var extraction = await _featureExtractor.ExtractFeaturesAsync(new AudioBuffer(samples, sampleRate), cancellationToken).ConfigureAwait(false);
        var mfcc = NormalizeMfcc(extraction.DirectionalFeature?.MfccCoefficients);

        var metrics = new ClickSampleMetrics(
            durationMs,
            rms,
            peak,
            attackMs,
            decayMs,
            zeroCrossing,
            spectralCentroid,
            spectralRolloff,
            spectralBandwidth,
            spectralFlatness,
            mfcc);

        return new ClickMetricsExtractionResult(metrics, extraction);
    }

    private static double[] NormalizeMfcc(double[]? mfcc)
    {
        const int target = 13;
        var output = new double[target];
        if (mfcc is null)
        {
            return output;
        }

        for (var i = 0; i < target; i++)
        {
            output[i] = i < mfcc.Length ? mfcc[i] : 0;
        }

        return output;
    }
}

public sealed record ClickMetricsExtractionResult(ClickSampleMetrics Metrics, FeatureExtractionResult FeatureExtraction);
