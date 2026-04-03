using System;
using System.Threading;
using System.Threading.Tasks;
using VocalJoystick.Core.Interfaces;
using VocalJoystick.Core.Models;

namespace VocalJoystick.Recognition;

public sealed class AutocorrelationPitchDetector : IPitchDetector
{
    private const double MinFrequency = 70;
    private const double MaxFrequency = 400;
    private const double ConfidenceThreshold = 0.35;

    public Task<PitchDetectionResult> DetectPitchAsync(Frame frame, CancellationToken cancellationToken)
    {
        if (frame.Samples.Length == 0)
        {
            return Task.FromResult(PitchDetectionResult.Unvoiced);
        }

        var sampleRate = frame.SampleRate;
        var minLag = Math.Max(1, (int)Math.Floor(sampleRate / MaxFrequency));
        var maxLag = Math.Min(frame.Samples.Length - 1, (int)Math.Ceiling(sampleRate / MinFrequency));

        double bestCorrelation = 0;
        int bestLag = -1;
        var energy = Energy(frame.Samples);

        for (var lag = minLag; lag <= maxLag; lag++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var corr = 0d;
            for (var i = 0; i < frame.Samples.Length - lag; i++)
            {
                corr += frame.Samples[i] * frame.Samples[i + lag];
            }

            if (corr <= bestCorrelation)
            {
                continue;
            }

            var normalization = Math.Sqrt(energy * energy);
            var confidence = normalization > double.Epsilon ? corr / normalization : 0;
            if (confidence > bestCorrelation)
            {
                bestCorrelation = confidence;
                bestLag = lag;
            }
        }

        if (bestLag <= 0 || bestCorrelation < ConfidenceThreshold)
        {
            return Task.FromResult(PitchDetectionResult.Unvoiced);
        }

        var pitch = sampleRate / (double)bestLag;
        return Task.FromResult(new PitchDetectionResult(pitch, bestCorrelation, true));
    }

    private static double Energy(float[] samples)
    {
        var sum = 0d;
        foreach (var sample in samples)
        {
            sum += sample * sample;
        }

        return sum;
    }
}
