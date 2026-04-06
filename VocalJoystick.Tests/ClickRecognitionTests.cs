using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VocalJoystick.Core.Interfaces;
using VocalJoystick.Core.Models;
using VocalJoystick.Recognition;

namespace VocalJoystick.Tests;

[TestClass]
public sealed class ClickRecognitionTests
{
    [TestMethod]
    public void ShortEventSegmenter_EmitsEventWhenThresholdExceeded()
    {
        var segmenter = new ShortEventSegmenter(0.1f, silenceSampleLimit: 2, maxEventSamples: 64);
        var samples = Enumerable.Range(0, 12).Select(i => i is >= 3 and <= 5 ? 0.2f : 0f).ToArray();
        var buffer = new AudioBuffer(samples, 16000) { Timestamp = DateTimeOffset.UtcNow };

        var events = segmenter.Segment(buffer);

        Assert.AreEqual(1, events.Count);
        Assert.IsTrue(events[0].Samples.Length >= 3);
    }

    [TestMethod]
    public void ShortClickTemplateClassifier_ReturnsActionWhenClose()
    {
        var classifier = new ShortClickTemplateClassifier();
            var summary = new SampleFeatureSummary(0.5, 0.1, 220, 5, 1200, 0.8, 0.7, null);
        var template = new ActionTemplate
        {
            SampleCount = 3,
            AverageDurationSeconds = 0.2,
            AverageRms = 0.52,
            AverageZeroCrossingRate = 0.11,
            AveragePitchHz = 215,
            PitchStdDev = 1,
            AverageSpectralCentroid = 6,
            AverageSpectralRolloff = 1180,
            VoicedRatio = 0.85
        };

        var templates = new Dictionary<VocalAction, ActionTemplate> { [VocalAction.LeftClick] = template };
        var result = classifier.Classify(summary, templates, 0);

        Assert.IsNotNull(result);
        Assert.AreEqual(VocalAction.LeftClick, result!.Action);
        Assert.IsTrue(result.Confidence > 0);
    }

    [TestMethod]
    public async Task ShortClickRecognitionEngine_RespectsCooldown()
    {
            var summary = new SampleFeatureSummary(0.4, 0.1, 180, 1, 4, 950, 0.9, null);
        var featureQueue = new Queue<FeatureExtractionResult>(new[]
        {
            new FeatureExtractionResult(Array.Empty<float>(), summary, null),
            new FeatureExtractionResult(Array.Empty<float>(), summary, null)
        });

        var extractor = new QueueFeatureExtractor(featureQueue);
        var classifier = new ClickClassifier(new ClickSimilarityCalculator());
        var segmenter = new ShortEventSegmenter(0.1f, silenceSampleLimit: 2, maxEventSamples: 64);
        var engine = new ShortClickRecognitionEngine(extractor, classifier, new TestLogger(), segmenter);
        var stats = new ClickFeatureStats(200, 100);
        var prototype = new ClickPrototype(
            VocalAction.LeftClick,
            stats,
            stats,
            stats,
            stats,
            stats,
            stats,
            stats,
            Enumerable.Repeat(0d, 13).ToArray());
        var prototypes = new Dictionary<VocalAction, ClickPrototype> { [VocalAction.LeftClick] = prototype };

        var now = DateTimeOffset.UtcNow;
        var buffer = new AudioBuffer(CreateEventSamples(), 16000) { Timestamp = now };
        var first = await engine.ProcessBufferAsync(buffer, prototypes, 0, 0, TimeSpan.FromSeconds(1), false, CancellationToken.None);

        Assert.IsNotNull(first);

        var buffer2 = new AudioBuffer(CreateEventSamples(), 16000) { Timestamp = now.AddMilliseconds(10) };
        var second = await engine.ProcessBufferAsync(buffer2, prototypes, 0, 0, TimeSpan.FromSeconds(1), false, CancellationToken.None);

        Assert.IsNull(second);
    }

    private static float[] CreateEventSamples()
    {
        var samples = new float[32];
        for (var i = 4; i < 8; i++)
        {
            samples[i] = 0.3f;
        }

        return samples;
    }

    private sealed class QueueFeatureExtractor : IFeatureExtractor
    {
        private readonly Queue<FeatureExtractionResult> _results;

        public QueueFeatureExtractor(IEnumerable<FeatureExtractionResult> results)
        {
            _results = new Queue<FeatureExtractionResult>(results);
        }

        public Task<FeatureExtractionResult> ExtractFeaturesAsync(AudioBuffer buffer, CancellationToken cancellationToken)
        {
            if (_results.Count == 0)
            {
                throw new InvalidOperationException("No feature result available");
            }

            return Task.FromResult(_results.Dequeue());
        }
    }

    private sealed class TestLogger : ILogger
    {
        public void LogInfo(string message) { }
        public void LogWarning(string message) { }
        public void LogError(string message, Exception? exception = null) { }
        public void LogDebug(string message) { }
    }
}
