using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VocalJoystick.Core.Models;

namespace VocalJoystick.Tests;

[TestClass]
public sealed class AppSettingsTests
{
    [TestMethod]
    public void CreateDefault_UsesIdleModeAndDefaultMovementSettings()
    {
        var settings = AppSettings.CreateDefault();

        Assert.AreEqual(AppMode.Idle, settings.LastMode);
        Assert.AreEqual(320, settings.MovementStartSpeed);
        Assert.AreEqual(320, settings.MovementEndSpeed);
        Assert.AreEqual(1, settings.MovementAccelerationSeconds);
        Assert.IsTrue(settings.RequireClickSilence);
        Assert.IsTrue(settings.LastUpdated <= DateTimeOffset.UtcNow);
    }

    [TestMethod]
    public void WithMovementStartSpeed_ClampsNegativeValues()
    {
        var settings = AppSettings.CreateDefault().WithMovementStartSpeed(-80);

        Assert.AreEqual(0, settings.MovementStartSpeed);
    }

    [TestMethod]
    public void WithMovementEndSpeed_ClampsNegativeValues()
    {
        var settings = AppSettings.CreateDefault().WithMovementEndSpeed(-40);

        Assert.AreEqual(0, settings.MovementEndSpeed);
    }

    [TestMethod]
    public void WithMovementAccelerationSeconds_ClampsNegativeValues()
    {
        var settings = AppSettings.CreateDefault().WithMovementAccelerationSeconds(-2);

        Assert.AreEqual(0, settings.MovementAccelerationSeconds);
    }

    [TestMethod]
    public void WithRequireClickSilence_TogglesValue()
    {
        var settings = AppSettings.CreateDefault().WithRequireClickSilence(false);

        Assert.IsFalse(settings.RequireClickSilence);
    }
}
