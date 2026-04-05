using System.Numerics;

namespace VocalJoystick.Recognition.FeatureExtraction;

internal static class FourierUtility
{
    public static double[] MagnitudeSpectrum(float[] samples, int fftSize)
    {
        var windowed = new Complex[fftSize];
        var length = Math.Min(samples.Length, fftSize);
        for (var i = 0; i < length; i++)
        {
            windowed[i] = samples[i] * Hamming(i, length);
        }

        for (var i = length; i < fftSize; i++)
        {
            windowed[i] = Complex.Zero;
        }

        FourierTransform(windowed);
        var magnitudes = new double[fftSize / 2];
        for (var i = 0; i < magnitudes.Length; i++)
        {
            magnitudes[i] = windowed[i].Magnitude;
        }

        return magnitudes;
    }

    public static void FourierTransform(Complex[] buffer)
    {
        var n = buffer.Length;
        var bits = (int)Math.Log2(n);
        for (var i = 0; i < n; i++)
        {
            var j = ReverseBits(i, bits);
            if (j > i)
            {
                (buffer[i], buffer[j]) = (buffer[j], buffer[i]);
            }
        }

        for (var len = 2; len <= n; len <<= 1)
        {
            var half = len / 2;
            var tableStep = n / len;
            for (var i = 0; i < n; i += len)
            {
                for (var j = 0; j < half; j++)
                {
                    var index = j * tableStep;
                    var angle = -2 * Math.PI * index / n;
                    var w = new Complex(Math.Cos(angle), Math.Sin(angle));
                    var even = buffer[i + j];
                    var odd = buffer[i + j + half] * w;
                    buffer[i + j] = even + odd;
                    buffer[i + j + half] = even - odd;
                }
            }
        }
    }

    private static double Hamming(int index, int length)
        => 0.54 - 0.46 * Math.Cos(2 * Math.PI * index / Math.Max(1, length - 1));

    private static int ReverseBits(int value, int bits)
    {
        var result = 0;
        for (var i = 0; i < bits; i++)
        {
            result <<= 1;
            result |= (value >> i) & 1;
        }

        return result;
    }
}
