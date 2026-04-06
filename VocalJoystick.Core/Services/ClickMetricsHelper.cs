using System;

namespace VocalJoystick.Core.Services;

internal static class ClickMetricsHelper
{
    public const int SpectrumAnalysisMaxLength = 2048;

    public static double CalculateRms(float[] samples)
    {
        if (samples.Length == 0)
        {
            return 0;
        }

        var sumSquares = 0d;
        foreach (var sample in samples)
        {
            sumSquares += sample * sample;
        }

        return Math.Sqrt(sumSquares / samples.Length);
    }

    public static double GetPeakAmplitude(float[] samples)
    {
        if (samples.Length == 0)
        {
            return 0;
        }

        var peak = 0d;
        foreach (var sample in samples)
        {
            var abs = Math.Abs(sample);
            if (abs > peak)
            {
                peak = abs;
            }
        }

        return peak;
    }

    public static double ComputeAttackTime(float[] samples, int sampleRate)
    {
        var peakIndex = GetPeakIndex(samples);
        return peakIndex / (double)Math.Max(1, sampleRate) * 1000;
    }

    public static double ComputeDecayTime(float[] samples, int sampleRate)
    {
        var peakIndex = GetPeakIndex(samples);
        if (samples.Length <= 1 || peakIndex >= samples.Length - 1)
        {
            return 0;
        }

        return (samples.Length - 1 - peakIndex) / (double)Math.Max(1, sampleRate) * 1000;
    }

    private static int GetPeakIndex(float[] samples)
    {
        var peak = 0d;
        var peakIndex = 0;
        for (var i = 0; i < samples.Length; i++)
        {
            var value = Math.Abs(samples[i]);
            if (value <= peak)
            {
                continue;
            }

            peak = value;
            peakIndex = i;
        }

        return peakIndex;
    }

    public static double CalculateZeroCrossingRate(float[] samples)
    {
        if (samples.Length < 2)
        {
            return 0;
        }

        var crossings = 0;
        for (var i = 1; i < samples.Length; i++)
        {
            var previous = samples[i - 1];
            var current = samples[i];
            if ((previous >= 0 && current < 0) || (previous < 0 && current >= 0))
            {
                crossings++;
            }
        }

        return crossings / (double)(samples.Length - 1);
    }

    public static double[] CalculateMagnitudeSpectrum(float[] samples, int analysisLength)
    {
        if (analysisLength <= 1)
        {
            return Array.Empty<double>();
        }

        var frame = new float[analysisLength];
        Array.Copy(samples, frame, Math.Min(samples.Length, analysisLength));
        var half = analysisLength / 2;
        var spectrum = new double[half];
        var angleFactorBase = -2 * Math.PI / analysisLength;

        for (var bin = 0; bin < half; bin++)
        {
            var real = 0d;
            var imag = 0d;
            var angleFactor = angleFactorBase * bin;

            for (var sampleIndex = 0; sampleIndex < analysisLength; sampleIndex++)
            {
                var value = frame[sampleIndex];
                var angle = angleFactor * sampleIndex;
                real += value * Math.Cos(angle);
                imag += value * Math.Sin(angle);
            }

            spectrum[bin] = Math.Sqrt(real * real + imag * imag);
        }

        return spectrum;
    }

    public static double CalculateSpectralCentroid(double[] magnitude, int sampleRate, int frameLength)
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

    public static double CalculateSpectralRolloff(double[] magnitude, int sampleRate, int frameLength, double target)
    {
        if (magnitude.Length == 0 || frameLength == 0)
        {
            return 0;
        }

        var totalEnergy = 0d;
        foreach (var mag in magnitude)
        {
            totalEnergy += mag;
        }

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

    public static double CalculateSpectralBandwidth(double[] magnitude, int sampleRate, int frameLength, double centroid)
    {
        if (magnitude.Length == 0 || frameLength == 0)
        {
            return 0;
        }

        var denominator = 0d;
        foreach (var mag in magnitude)
        {
            denominator += mag;
        }

        if (denominator <= double.Epsilon)
        {
            return 0;
        }

        var sum = 0d;
        for (var bin = 0; bin < magnitude.Length; bin++)
        {
            var freq = bin * (double)sampleRate / frameLength;
            var mag = magnitude[bin];
            var diff = freq - centroid;
            sum += mag * diff * diff;
        }

        return Math.Sqrt(sum / denominator);
    }

    public static double CalculateSpectralFlatness(double[] magnitude)
    {
        if (magnitude.Length == 0)
        {
            return 0;
        }

        const double epsilon = 1e-12;
        var linearSum = 0d;
        var logSum = 0d;
        foreach (var mag in magnitude)
        {
            var value = Math.Max(mag, epsilon);
            linearSum += value;
            logSum += Math.Log(value);
        }

        var linearMean = linearSum / magnitude.Length;
        var geometricMean = Math.Exp(logSum / magnitude.Length);
        return linearMean <= double.Epsilon ? 0 : geometricMean / linearMean;
    }
}
