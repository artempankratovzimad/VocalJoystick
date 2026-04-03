using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VocalJoystick.Core.Models;
using VocalJoystick.Recognition;

namespace VocalJoystick.Tests;

[TestClass]
public sealed class FeatureExtractorTests
{
    private static FeatureExtractor CreateExtractor() => new(new AutocorrelationPitchDetector(), new EnergyVoiceActivityDetector());

    [TestMethod]
    public async Task ExtractFeatures_ForSineWave_ReturnsVoicedSummary()
    {
        var extractor = CreateExtractor();
        var sampleRate = 16_000;
        var frequency = 220.0;
        var samples = Enumerable.Range(0, sampleRate)
            .Select(i => (float)Math.Sin(2 * Math.PI * frequency * i / sampleRate))
            .ToArray();

        var result = await extractor.ExtractFeaturesAsync(new AudioBuffer(samples, sampleRate), CancellationToken.None);

        Assert.IsTrue(result.Summary.Rms > 0.6);
        Assert.IsTrue(result.Summary.VoicedRatio > 0.4);
        Assert.IsTrue(result.Summary.PitchHz > frequency - 30);
        Assert.IsTrue(result.Summary.PitchHz < frequency + 30);
        Assert.IsTrue(result.FeatureVector.Length >= 6);
    }

    [TestMethod]
    public async Task ExtractFeatures_ForSilence_ReturnsUnvoiced()
    {
        var extractor = CreateExtractor();
        var buffer = new AudioBuffer(new float[1024], 16_000);

        var result = await extractor.ExtractFeaturesAsync(buffer, CancellationToken.None);

        Assert.AreEqual(0, result.Summary.VoicedRatio);
        Assert.AreEqual(0, result.Summary.PitchHz);
    }
}
