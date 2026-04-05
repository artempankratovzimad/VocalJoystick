using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VocalJoystick.Core.Models;

namespace VocalJoystick.Tests;

[TestClass]
public sealed class ProfileConfigurationTests
{
    [TestMethod]
    public void CreateDefault_RegistersEveryAction()
    {
        var metadata = new UserProfileMetadata { DisplayName = "Diagnostics" };
        var configuration = ProfileConfiguration.CreateDefault(metadata);

        Assert.AreEqual(metadata, configuration.Metadata);
        Assert.AreEqual(Enum.GetValues<VocalAction>().Length, configuration.ActionConfigurations.Count);
    }

    [TestMethod]
    public void ConfiguredActions_ReturnsCompleteActionList()
    {
        var metadata = new UserProfileMetadata();
        var configuration = ProfileConfiguration.CreateDefault(metadata);
        var actual = configuration.ConfiguredActions.Select(config => config.Action).ToArray();
        var expected = Enum.GetValues<VocalAction>().Cast<VocalAction>().ToArray();

        CollectionAssert.AreEquivalent(expected, actual);
    }
}
