using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VocalJoystick.Core.Interfaces;
using VocalJoystick.Core.Models;
using VocalJoystick.Infrastructure.Recognition;
using VocalJoystick.Recognition.Directional;

namespace VocalJoystick.Tests;

[TestClass]
public sealed class VowelDirectionalRecognizerTests
{
    [TestMethod]
    public void Recognize_HonorsHysteresisAndHold()
    {
        var trainingService = new DirectionalTrainingService();
        var feature = CreateFeature();
        for (var i = 0; i < trainingService.MinimumSamples; i++)
        {
            trainingService.AddSample(VocalAction.MoveUp, feature);
        }

        trainingService.TryBuildTemplate(VocalAction.MoveUp, out _);
        var classifier = new VowelDirectionalClassifier();
        var recognizer = new VowelDirectionalRecognizer(classifier, trainingService, new TestLogger(), new DirectionalRecognitionSettings { ActivationHoldSeconds = 0.1 });
        recognizer.UpdateTemplates(trainingService.GetTemplates());

        var voice = new VoiceActivityResult(true, 0.5);
        var pitch = new PitchDetectionResult(220, 0.9, true);
        var now = DateTimeOffset.UtcNow;

        var first = recognizer.Recognize(voice, pitch, feature, now);
        Assert.IsFalse(first.IsActive);

        var second = recognizer.Recognize(voice, pitch, feature, now.AddSeconds(0.2));
        Assert.IsTrue(second.IsActive);
        Assert.AreEqual(VocalAction.MoveUp, second.ActiveDirection);
    }

    [TestMethod]
    public void Recognize_LowLatencyMode_ActivatesImmediatelyWithoutReliableGate()
    {
        var recognizer = new VowelDirectionalRecognizer(
            new FakeClassifier(new DirectionalClassificationResult(VocalAction.MoveUp, 0.4, CreateFeature(), false)),
            new DirectionalTrainingService(),
            new TestLogger(),
            new DirectionalRecognitionSettings { ActivationHoldSeconds = 0.2, ActivationConfidence = 0.62 });

        recognizer.UpdateTemplates(new Dictionary<VocalAction, DirectionalTemplate>
        {
            [VocalAction.MoveUp] = new(VocalAction.MoveUp, CreateFeature(), 5, 0.6)
        });

        var voice = new VoiceActivityResult(true, 0.4);
        var pitch = new PitchDetectionResult(210, 0.8, true);
        var now = DateTimeOffset.UtcNow;

        var defaultMode = recognizer.Recognize(voice, pitch, CreateFeature(), now);
        Assert.IsFalse(defaultMode.IsActive);

        recognizer.UseLowLatencyMode(true);
        var lowLatency = recognizer.Recognize(voice, pitch, CreateFeature(), now.AddMilliseconds(25));
        Assert.IsTrue(lowLatency.IsActive);
        Assert.AreEqual(VocalAction.MoveUp, lowLatency.ActiveDirection);
    }

    private static DirectionalFeatureVector CreateFeature()
    {
        var mfcc = Enumerable.Range(0, 13).Select(i => i * 0.1).ToArray();
        var formants = new FormantResult(400, 1200);
        return new DirectionalFeatureVector(mfcc, formants, 0.05, 600, 20, true, 220, 0.9, 1);
    }

    private sealed class TestLogger : ILogger
    {
        public void LogDebug(string message) { }
        public void LogInfo(string message) { }
        public void LogWarning(string message) { }
        public void LogError(string message, Exception? exception = null) { }
    }

    private sealed class FakeClassifier : IDirectionalClassifier
    {
        private readonly DirectionalClassificationResult _result;

        public FakeClassifier(DirectionalClassificationResult result)
        {
            _result = result;
        }

        public DirectionalClassificationResult Classify(DirectionalFeatureVector feature, IReadOnlyDictionary<VocalAction, DirectionalTemplate> templates)
        {
            return _result with { Feature = feature };
        }
    }
}
