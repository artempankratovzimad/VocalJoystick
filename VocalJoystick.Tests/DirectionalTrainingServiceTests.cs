using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VocalJoystick.Core.Models;
using VocalJoystick.Infrastructure.Recognition;

namespace VocalJoystick.Tests;

[TestClass]
public sealed class DirectionalTrainingServiceTests
{
    [TestMethod]
    public void AddSample_BelowMinimum_DoesNotBuildTemplate()
    {
        var service = new DirectionalTrainingService();
        var feature = CreateFeature();
        service.AddSample(VocalAction.MoveUp, feature);

        Assert.IsFalse(service.TryBuildTemplate(VocalAction.MoveUp, out _));
    }

    [TestMethod]
    public void AddSample_MinSamples_BuildsTemplate()
    {
        var service = new DirectionalTrainingService();
        var feature = CreateFeature();
        for (var i = 0; i < service.MinimumSamples; i++)
        {
            service.AddSample(VocalAction.MoveUp, feature);
        }

        Assert.IsTrue(service.TryBuildTemplate(VocalAction.MoveUp, out var template));
        Assert.IsNotNull(template);
        Assert.AreEqual(service.MinimumSamples, template!.SampleCount);
    }

    [TestMethod]
    public void ValidateSample_Unvoiced_ReturnsFalse()
    {
        var service = new DirectionalTrainingService();
        var feature = CreateFeature(pitch: 0, voiced: false);

        var valid = service.ValidateSample(feature, out var reason);

        Assert.IsFalse(valid);
        Assert.AreEqual("Sample is not voiced", reason);
    }

    private static DirectionalFeatureVector CreateFeature(double pitch = 220, bool voiced = true)
    {
        var mfcc = Enumerable.Range(0, 13).Select(i => 0.1 * i).ToArray();
        var formants = new FormantResult(400, 1600);
        return new DirectionalFeatureVector(mfcc, formants, 0.05, 500, 40, voiced, pitch, 0.8, 1.0);
    }
}
