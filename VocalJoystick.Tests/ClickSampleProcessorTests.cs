using System;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VocalJoystick.Core.Interfaces;
using VocalJoystick.Core.Models;
using VocalJoystick.Infrastructure.Recording;

namespace VocalJoystick.Tests;

[TestClass]
public sealed class ClickSampleProcessorTests
{
    [TestMethod]
    public async Task ProcessAsync_FindsClickEvent()
    {
        var processor = new ClickSampleProcessor(new StubFeatureExtractor(), new TestLogger());
        var samples = BuildSamplesWithImpulse(16000, 0, 4800, 0.8f);
        var result = await processor.ProcessAsync(VocalAction.LeftClick, samples, 16000, FrameProcessingSettings.CreateDefault(), CancellationToken.None);

        Assert.IsTrue(result.IsSuccess);
        Assert.IsNotNull(result.Metrics);
        Assert.IsNotNull(result.FeatureExtraction);
        Assert.IsTrue(result.TrimmedSamples.Length > 0);
        Assert.IsTrue(result.Metrics!.DurationMs >= 100);
    }

    [TestMethod]
    public async Task ProcessAsync_FailsWhenNoEvent()
    {
        var processor = new ClickSampleProcessor(new StubFeatureExtractor(), new TestLogger());
        var samples = new float[16000];
        var result = await processor.ProcessAsync(VocalAction.LeftClick, samples, 16000, FrameProcessingSettings.CreateDefault(), CancellationToken.None);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual("No click event detected", result.FailureReason);
    }

    [TestMethod]
    public async Task ProcessAsync_FailsForTooShortEvent()
    {
        var processor = new ClickSampleProcessor(new StubFeatureExtractor(), new TestLogger());
        var samples = BuildSamplesWithImpulse(16000, 0, 400, 0.9f);
        var result = await processor.ProcessAsync(VocalAction.LeftClick, samples, 16000, FrameProcessingSettings.CreateDefault(), CancellationToken.None);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual("Click duration is too short", result.FailureReason);
    }

    [TestMethod]
    public async Task ProcessAsync_AllowsDoubleClickEvent()
    {
        var processor = new ClickSampleProcessor(new StubFeatureExtractor(), new TestLogger());
        var samples = BuildSamplesWithDoubleImpulse(16000, 0, 400, 0.2f, 0.8f, 40);
        var result = await processor.ProcessAsync(VocalAction.DoubleClick, samples, 16000, FrameProcessingSettings.CreateDefault(), CancellationToken.None);

        Assert.IsTrue(result.IsSuccess);
        Assert.IsNotNull(result.Metrics);
    }

    private static float[] BuildSamplesWithImpulse(int sampleRate, int startOffset, int length, float amplitude)
    {
        var total = sampleRate;
        var samples = new float[total];
        var start = Math.Clamp(startOffset, 0, total - 1);
        var end = Math.Min(total, start + length);
        for (var i = start; i < end; i++)
        {
            samples[i] = amplitude;
        }

        return samples;
    }

    private static float[] BuildSamplesWithDoubleImpulse(int sampleRate, int firstStart, int firstLength, float firstAmplitude, float secondAmplitude, int gapSamples)
    {
        var total = sampleRate;
        var samples = new float[total];
        var firstEnd = Math.Min(total, firstStart + firstLength);
        for (var i = firstStart; i < firstEnd; i++)
        {
            samples[i] = firstAmplitude;
        }

        var secondStart = Math.Min(total, firstEnd + gapSamples);
        var secondEnd = Math.Min(total, secondStart + firstLength);
        for (var i = secondStart; i < secondEnd; i++)
        {
            samples[i] = secondAmplitude;
        }

        return samples;
    }

    private sealed class StubFeatureExtractor : IFeatureExtractor
    {
        public Task<FeatureExtractionResult> ExtractFeaturesAsync(AudioBuffer buffer, CancellationToken cancellationToken)
        {
            var rms = buffer.Samples.Length == 0 ? 0 : Math.Sqrt(buffer.Samples.Average(sample => sample * sample));
            var directional = new DirectionalFeatureVector(
                Enumerable.Repeat(0d, 13).ToArray(),
                new FormantResult(0, 0),
                rms,
                0,
                0,
                false,
                0,
                0,
                0);
            var summary = new SampleFeatureSummary(rms, 0, 0, 0, 0, 0, 0, directional);
            var extraction = new FeatureExtractionResult(Array.Empty<float>(), summary, directional);
            return Task.FromResult(extraction);
        }
    }

    private sealed class TestLogger : ILogger
    {
        public void LogDebug(string message) { }
        public void LogInfo(string message) { }
        public void LogWarning(string message) { }
        public void LogError(string message, Exception? exception = null) { }
    }
}
