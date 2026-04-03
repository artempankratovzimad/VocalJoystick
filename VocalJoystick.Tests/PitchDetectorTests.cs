using System;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VocalJoystick.Core.Models;
using VocalJoystick.Recognition;

namespace VocalJoystick.Tests;

[TestClass]
public sealed class PitchDetectorTests
{
    [TestMethod]
    public async Task Autocorrelation_DetectsPitchForSineWave()
    {
        var detector = new AutocorrelationPitchDetector();
        var sampleRate = 16000;
        var frequency = 220.0;
        var frameSize = 512;
        var samples = Enumerable.Range(0, frameSize)
            .Select(i => (float)Math.Sin(2 * Math.PI * frequency * i / sampleRate))
            .ToArray();
        var frame = new Frame(0, samples, sampleRate);
        var settings = FrameProcessingSettings.CreateDefault();

        var result = await detector.DetectPitchAsync(frame, CancellationToken.None);

        Assert.IsTrue(result.IsVoiced);
        Assert.IsNotNull(result.PitchHz);
        Assert.IsTrue(Math.Abs(result.PitchHz.Value - frequency) < 10);
        Assert.IsTrue(result.Confidence > 0.3);
    }

    [TestMethod]
    public async Task Autocorrelation_ReturnsUnvoicedForNoise()
    {
        var detector = new AutocorrelationPitchDetector();
        var samples = Enumerable.Range(0, 512).Select(_ => (float)(Random.Shared.NextDouble() * 2 - 1)).ToArray();
        var frame = new Frame(0, samples, 16000);
        var result = await detector.DetectPitchAsync(frame, CancellationToken.None);

        Assert.IsFalse(result.IsVoiced);
        Assert.IsNull(result.PitchHz);
    }
}
