using System;
using System.Collections.Generic;
using VocalJoystick.Core.Models;

namespace VocalJoystick.Recognition;

public static class FrameSegmenter
{
    public static IEnumerable<Frame> Segment(float[] samples, FrameProcessingSettings settings, int sampleRate)
    {
        var frameSize = Math.Max(1, settings.FrameSize);
        var overlap = Math.Clamp(settings.Overlap, 0, 0.95);
        var hopSize = Math.Max(1, (int)Math.Round(frameSize * (1 - overlap)));
        var index = 0;
        var frameIndex = 0;

        if (samples.Length < frameSize)
        {
            var buffer = new float[frameSize];
            Array.Copy(samples, buffer, samples.Length);
            yield return new Frame(frameIndex++, buffer, sampleRate);
            yield break;
        }

        while (index + frameSize <= samples.Length)
        {
            var buffer = new float[frameSize];
            Array.Copy(samples, index, buffer, 0, frameSize);
            yield return new Frame(frameIndex++, buffer, sampleRate);
            index += hopSize;
        }
    }
}
