using System.Numerics;
using VocalJoystick.Core.Interfaces;
using VocalJoystick.Core.Models;

namespace VocalJoystick.Recognition.FeatureExtraction;

public sealed class BasicFormantExtractor : IFormantExtractor
{
    private const int TargetFftSize = 2048;

    public FormantResult ExtractFormants(float[] samples, int sampleRate)
    {
        if (samples is null || samples.Length == 0)
        {
            return new FormantResult(0, 0);
        }

        var spectrum = FourierUtility.MagnitudeSpectrum(samples, TargetFftSize);
        var f1 = FindPeak(spectrum, sampleRate, TargetFftSize, 250, 950);
        var f2 = FindPeak(spectrum, sampleRate, TargetFftSize, 950, 2400);
        return new FormantResult(f1, f2);
    }
    private static double FindPeak(double[] spectrum, int sampleRate, int fftSize, double minHz, double maxHz)
    {
        var bins = spectrum.Length;
        var start = (int)Math.Floor(minHz / sampleRate * fftSize);
        var end = (int)Math.Min(bins - 1, Math.Ceiling(maxHz / sampleRate * fftSize));
        var bestIndex = start;
        var bestValue = 0d;
        for (var i = Math.Max(0, start); i <= end; i++)
        {
            if (spectrum[i] > bestValue)
            {
                bestValue = spectrum[i];
                bestIndex = i;
            }
        }

        return bestIndex * sampleRate / (double)fftSize;
    }
}
