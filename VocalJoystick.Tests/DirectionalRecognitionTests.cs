using System;
using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VocalJoystick.Core.Models;
using VocalJoystick.Recognition;

namespace VocalJoystick.Tests;

[TestClass]
public sealed class DirectionalRecognitionTests
{
    private static DirectionalCommandRecognizer CreateRecognizer(DirectionalRecognitionSettings? settings = null) => new(settings);

    [TestMethod]
    public void RecognizerActivatesAfterHold()
    {
        var settings = new DirectionalRecognitionSettings
        {
            ActivationConfidence = 0.5,
            ActivationHoldSeconds = 0.2,
            HysteresisMargin = 0.1
        };
        var recognizer = CreateRecognizer(settings);
        recognizer.UpdateTemplates(new Dictionary<VocalAction, ActionTemplate>
        {
            [VocalAction.MoveUp] = CreateTemplate(220, 0.5)
        });

        var timestamp = DateTimeOffset.UtcNow;
        var first = recognizer.Recognize(CreateInput(timestamp));
        Assert.IsNull(first.ActiveDirection);

        var second = recognizer.Recognize(CreateInput(timestamp.AddSeconds(0.12)));
        Assert.IsNull(second.ActiveDirection);
        Assert.IsTrue(second.Debug.HoldSeconds > 0);

        var third = recognizer.Recognize(CreateInput(timestamp.AddSeconds(0.25)));
        Assert.AreEqual(VocalAction.MoveUp, third.ActiveDirection);
        Assert.IsTrue(third.IsActive);
        Assert.AreEqual("Active", third.Debug.Status);
    }

    [TestMethod]
    public void RecognizerClearsHoldWhenConfidenceDrops()
    {
        var settings = new DirectionalRecognitionSettings
        {
            ActivationConfidence = 0.5,
            ActivationHoldSeconds = 0.2
        };
        var recognizer = CreateRecognizer(settings);
        recognizer.UpdateTemplates(new Dictionary<VocalAction, ActionTemplate>
        {
            [VocalAction.MoveLeft] = CreateTemplate(200, 0.4)
        });

        var timestamp = DateTimeOffset.UtcNow;
        recognizer.Recognize(CreateInput(timestamp));
        recognizer.Recognize(CreateInput(timestamp.AddSeconds(0.1)));

        var reset = recognizer.Recognize(CreateInput(timestamp.AddSeconds(0.2), rms: 0.05, pitchConfidence: 0.2));
        Assert.IsNull(reset.ActiveDirection);

        var afterReset = recognizer.Recognize(CreateInput(timestamp.AddSeconds(0.25)));
        Assert.IsNull(afterReset.ActiveDirection);
        Assert.IsTrue(afterReset.Debug.HoldSeconds < settings.ActivationHoldSeconds);
    }

    [TestMethod]
    public void RecognizerStopsWhenConfidenceOrVoiceDrops()
    {
        var settings = new DirectionalRecognitionSettings
        {
            ActivationConfidence = 0.5,
            ActivationHoldSeconds = 0.1,
            HysteresisMargin = 0.15
        };
        var recognizer = CreateRecognizer(settings);
        recognizer.UpdateTemplates(new Dictionary<VocalAction, ActionTemplate>
        {
            [VocalAction.MoveDown] = CreateTemplate(180, 0.6)
        });

        var timestamp = DateTimeOffset.UtcNow;
        recognizer.Recognize(CreateInput(timestamp, pitch: 180));
        recognizer.Recognize(CreateInput(timestamp.AddSeconds(0.12), pitch: 180));
        var active = recognizer.Recognize(CreateInput(timestamp.AddSeconds(0.25), pitch: 180));
        Assert.AreEqual(VocalAction.MoveDown, active.ActiveDirection);

        var drop = recognizer.Recognize(CreateInput(timestamp.AddSeconds(0.35), rms: 0.1, pitchConfidence: 0.3));
        Assert.IsNull(drop.ActiveDirection);
        Assert.AreEqual("Confidence below threshold", drop.Debug.Status);

        var voiceOff = recognizer.Recognize(CreateInput(timestamp.AddSeconds(0.4), voiceActive: false, pitched: false));
        Assert.IsNull(voiceOff.ActiveDirection);
        Assert.AreEqual("Voice inactive", voiceOff.Debug.Status);
    }

    private static DirectionalRecognitionInput CreateInput(DateTimeOffset timestamp, bool voiceActive = true, double rms = 0.52, double pitch = 220, double pitchConfidence = 0.8, bool pitched = true)
    {
        var vad = new VoiceActivityResult(voiceActive, rms);
        var pitchResult = pitched ? new PitchDetectionResult(pitch, pitchConfidence, true) : PitchDetectionResult.Unvoiced;
        return new DirectionalRecognitionInput(vad, pitchResult, timestamp);
    }

    private static ActionTemplate CreateTemplate(double pitch, double rms) => new()
    {
        SampleCount = 3,
        AverageDurationSeconds = 0.3,
        AverageRms = rms,
        AverageZeroCrossingRate = 0.15,
        AveragePitchHz = pitch,
        PitchStdDev = 10,
        AverageSpectralCentroid = 600,
        AverageSpectralRolloff = 1200,
        VoicedRatio = 1
    };
}
