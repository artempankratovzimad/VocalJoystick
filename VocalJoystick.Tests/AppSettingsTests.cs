using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VocalJoystick.Core.Models;

namespace VocalJoystick.Tests;

[TestClass]
public sealed class AppSettingsTests
{
    [TestMethod]
    public void CreateDefault_UsesIdleModeAndDefaultSpeed()
    {
        var settings = AppSettings.CreateDefault();

        Assert.AreEqual(AppMode.Idle, settings.LastMode);
        Assert.AreEqual(320, settings.MovementSpeed);
        Assert.IsTrue(settings.LastUpdated <= DateTimeOffset.UtcNow);
    }

    [TestMethod]
    public void WithMovementSpeed_ClampsNegativeValues()
    {
        var settings = AppSettings.CreateDefault().WithMovementSpeed(-80);

        Assert.AreEqual(0, settings.MovementSpeed);
    }
}
