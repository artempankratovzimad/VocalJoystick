using System;

namespace VocalJoystick.Core.Models;

public sealed record FrameProcessingSettings(int FrameSize, double Overlap, double VadThreshold)
{
    public static FrameProcessingSettings CreateDefault() => new(512, 0.5, 0.02);

    public FrameProcessingSettings WithFrameSize(int frameSize) => this with { FrameSize = Math.Max(64, frameSize) };
    public FrameProcessingSettings WithOverlap(double overlap) => this with { Overlap = Math.Clamp(overlap, 0, 0.95) };
    public FrameProcessingSettings WithThreshold(double threshold) => this with { VadThreshold = Math.Max(0, threshold) };
}
