using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VocalJoystick.Core.Interfaces;
using VocalJoystick.Core.Models;

namespace VocalJoystick.Recognition;

public sealed class FeatureExtractor : IFeatureExtractor
{
    private const double RolloffTarget = 0.85;
    private readonly FrameProcessingSettings _settings = FrameProcessingSettings.CreateDefault();
    private readonly IVoiceActivityDetector _voiceActivityDetector;
    private readonly IPitchDetector _pitchDetector;

    public FeatureExtractor(IPitchDetector pitchDetector, IVoiceActivityDetector voiceActivityDetector)
    {
        _pitchDetector = pitchDetector;
        _voiceActivityDetector = voiceActivityDetector;
    }

    public async Task<FeatureExtractionResult> ExtractFeaturesAsync(AudioBuffer buffer, CancellationToken cancellationToken)
    {
        var samples = buffer.Samples;
        var frames = FrameSegmenter.Segment(samples, _settings, buffer.SampleRate).ToArray();
        var overallRms = AudioAnalysisHelpers.CalculateRms(samples);
        var zeroCrossingRate = CalculateZeroCrossingRate(samples);

        var centroids = new List<double>();
        var rolloffs = new List<double>();
        var voicedFrames = 0;
        var pitchValues = new List<double>();

        foreach (var frame in frames)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var spectrum = CalculateMagnitudeSpectrum(frame.Samples);
            centroids.Add(CalculateSpectralCentroid(spectrum, frame.SampleRate, frame.Samples.Length));
            rolloffs.Add(CalculateSpectralRolloff(spectrum, frame.SampleRate, frame.Samples.Length, RolloffTarget));

            var vad = _voiceActivityDetector.Analyze(frame, _settings);
            if (vad.IsActive)
            {
                voicedFrames++;
            }

            var pitchResult = await _pitchDetector.DetectPitchAsync(frame, cancellationToken).ConfigureAwait(false);
            if (pitchResult.IsVoiced && pitchResult.PitchHz.HasValue)
            {
                pitchValues.Add(pitchResult.PitchHz.Value);
            }
        }

        var averageCentroid = centroids.Any() ? centroids.Average() : 0;
        var averageRolloff = rolloffs.Any() ? rolloffs.Average() : 0;
        var voicedRatio = frames.Length == 0 ? 0 : (double)voicedFrames / frames.Length;
        var pitchMean = pitchValues.Any() ? pitchValues.Average() : 0;
        var pitchStdDev = pitchValues.Count > 1 ? CalculateStandardDeviation(pitchValues, pitchMean) : 0;

        var featureVector = new float[]
        {
            (float)overallRms,
            (float)zeroCrossingRate,
            (float)averageCentroid,
            (float)averageRolloff,
            (float)pitchMean,
            (float)pitchStdDev,
            (float)voicedRatio
        };

        var summary = new SampleFeatureSummary(
            overallRms,
            zeroCrossingRate,
            pitchMean,
            pitchStdDev,
            averageCentroid,
            averageRolloff,
            voicedRatio);

        return new FeatureExtractionResult(featureVector, summary);
    }

    private static double CalculateZeroCrossingRate(float[] samples)
    {
        if (samples.Length < 2)
        {
            return 0;
        }

        var crossings = 0;
        for (var i = 1; i < samples.Length; i++)
        {
            var prev = samples[i - 1];
            var curr = samples[i];
            if ((prev >= 0 && curr < 0) || (prev < 0 && curr >= 0))
            {
                crossings++;
            }
        }

        return crossings / (double)(samples.Length - 1);
    }

    private static double[] CalculateMagnitudeSpectrum(float[] frame)
    {
        var length = frame.Length;
        var half = length / 2;
        var spectrum = new double[half];

        for (var bin = 0; bin < half; bin++)
        {
            var real = 0d;
            var imag = 0d;
            var angleFactor = -2 * Math.PI * bin / length;

            for (var sampleIndex = 0; sampleIndex < length; sampleIndex++)
            {
                var angle = angleFactor * sampleIndex;
                real += frame[sampleIndex] * Math.Cos(angle);
                imag += frame[sampleIndex] * Math.Sin(angle);
            }

            spectrum[bin] = Math.Sqrt(real * real + imag * imag);
        }

        return spectrum;
    }

    private static double CalculateSpectralCentroid(double[] magnitude, int sampleRate, int frameLength)
    {
        if (magnitude.Length == 0 || frameLength == 0)
        {
            return 0;
        }

        var numerator = 0d;
        var denominator = 0d;

        for (var bin = 0; bin < magnitude.Length; bin++)
        {
            var freq = bin * (double)sampleRate / frameLength;
            var mag = magnitude[bin];
            numerator += freq * mag;
            denominator += mag;
        }

        return denominator == 0 ? 0 : numerator / denominator;
    }

    private static double CalculateSpectralRolloff(double[] magnitude, int sampleRate, int frameLength, double target)
    {
        if (magnitude.Length == 0 || frameLength == 0)
        {
            return 0;
        }

        var totalEnergy = magnitude.Sum();
        if (totalEnergy <= double.Epsilon)
        {
            return 0;
        }

        var threshold = totalEnergy * Math.Clamp(target, 0, 1);
        var cumulative = 0d;

        for (var bin = 0; bin < magnitude.Length; bin++)
        {
            cumulative += magnitude[bin];
            if (cumulative >= threshold)
            {
                return bin * (double)sampleRate / frameLength;
            }
        }

        return sampleRate / 2d;
    }

    private static double CalculateStandardDeviation(IReadOnlyList<double> values, double mean)
    {
        if (values.Count < 2)
        {
            return 0;
        }

        var sumSquares = values.Sum(value => Math.Pow(value - mean, 2));
        return Math.Sqrt(sumSquares / (values.Count - 1));
    }
}
