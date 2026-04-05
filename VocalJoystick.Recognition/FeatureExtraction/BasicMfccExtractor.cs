using System.Linq;
using VocalJoystick.Core.Interfaces;

namespace VocalJoystick.Recognition.FeatureExtraction;

public sealed class BasicMfccExtractor : IMfccExtractor
{
    private const int FftSize = 2048;
    private const int FilterCount = 24;

    public double[] ExtractMfcc(float[] samples, int sampleRate, int coefficientCount = 13)
    {
        if (samples is null || samples.Length == 0)
        {
            return Array.Empty<double>();
        }

        var spectrum = FourierUtility.MagnitudeSpectrum(samples, FftSize);
        var melEnergy = ComputeMelEnergy(spectrum, sampleRate);
        var logEnergy = melEnergy.Select(e => Math.Log(e + 1e-9)).ToArray();
        var mfcc = ApplyDct(logEnergy, coefficientCount);
        return mfcc;
    }

    private static double[] ComputeMelEnergy(double[] spectrum, int sampleRate)
    {
        var melMin = Mel(0);
        var melMax = Mel(sampleRate / 2.0);
        var melPoints = Linspace(melMin, melMax, FilterCount + 2);
        var binPoints = melPoints.Select(m => (int)Math.Floor(InverseMel(m) / sampleRate * FftSize)).ToArray();
        var energies = new double[FilterCount];

        for (var i = 1; i <= FilterCount; i++)
        {
            var left = Math.Clamp(binPoints[i - 1], 0, spectrum.Length - 1);
            var center = Math.Clamp(binPoints[i], 0, spectrum.Length - 1);
            var right = Math.Clamp(binPoints[i + 1], 0, spectrum.Length - 1);
            for (var j = left; j < center; j++)
            {
                energies[i - 1] += spectrum[j] * ((double)(j - left) / Math.Max(1, center - left));
            }
            for (var j = center; j < right; j++)
            {
                energies[i - 1] += spectrum[j] * (1 - (double)(j - center) / Math.Max(1, right - center));
            }
        }

        return energies;
    }

    private static double[] ApplyDct(double[] values, int coefficientCount)
    {
        var result = new double[Math.Max(0, Math.Min(values.Length, coefficientCount))];
        var n = values.Length;
        if (n == 0)
        {
            return result;
        }

        for (var k = 0; k < result.Length; k++)
        {
            var sum = 0d;
            for (var i = 0; i < n; i++)
            {
                sum += values[i] * Math.Cos(Math.PI * k * (2 * i + 1) / (2 * n));
            }
            result[k] = sum;
        }

        return result;
    }

    private static double[] Linspace(double min, double max, int count)
    {
        var result = new double[count];
        var step = count <= 1 ? 0 : (max - min) / (count - 1);
        for (var i = 0; i < count; i++)
        {
            result[i] = min + step * i;
        }

        return result;
    }

    private static double Mel(double frequency) => 2595 * Math.Log10(1 + frequency / 700);
    private static double InverseMel(double mel) => 700 * (Math.Pow(10, mel / 2595) - 1);
}
