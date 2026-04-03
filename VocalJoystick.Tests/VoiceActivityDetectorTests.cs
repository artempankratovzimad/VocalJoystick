using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VocalJoystick.Core.Models;
using VocalJoystick.Recognition;

namespace VocalJoystick.Tests;

[TestClass]
public sealed class VoiceActivityDetectorTests
{
    [TestMethod]
    public void EnergyDetector_ReportsSilenceForZeros()
    {
        var detector = new EnergyVoiceActivityDetector();
        var settings = FrameProcessingSettings.CreateDefault().WithThreshold(0.01);
        var frame = new Frame(0, new float[settings.FrameSize], 16_000);

        var result = detector.Analyze(frame, settings);

        Assert.IsFalse(result.IsActive);
        Assert.AreEqual(0d, result.Rms);
    }

    [TestMethod]
    public void EnergyDetector_ReportsActivityAboveThreshold()
    {
        var detector = new EnergyVoiceActivityDetector();
        var frameSize = 512;
        var highSamples = Enumerable.Repeat(0.5f, frameSize).ToArray();
        var settings = FrameProcessingSettings.CreateDefault().WithThreshold(0.1).WithFrameSize(frameSize);
        var frame = new Frame(0, highSamples, 16_000);

        var result = detector.Analyze(frame, settings);

        Assert.IsTrue(result.IsActive);
        Assert.IsTrue(result.Rms > settings.VadThreshold);
    }

    [TestMethod]
    public void FrameSegmenter_ProducesOverlappingFrames()
    {
        var frameSize = 256;
        var settings = FrameProcessingSettings.CreateDefault().WithFrameSize(frameSize).WithOverlap(0.5);
        var samples = Enumerable.Range(0, frameSize * 3).Select(i => (float)Math.Sin(i * 0.1)).ToArray();

        var frames = FrameSegmenter.Segment(samples, settings, 16_000).ToArray();

        Assert.IsTrue(frames.Length >= 2);
        Assert.AreEqual(frameSize, frames.First().Samples.Length);
    }
}
