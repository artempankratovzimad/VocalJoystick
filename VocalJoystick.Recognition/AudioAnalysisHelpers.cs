using System;

namespace VocalJoystick.Recognition;

internal static class AudioAnalysisHelpers
{
    public static double CalculateRms(float[] samples)
    {
        double sumSquares = 0;
        foreach (var sample in samples)
        {
            sumSquares += sample * sample;
        }

        return samples.Length == 0 ? 0d : Math.Sqrt(sumSquares / samples.Length);
    }
}
