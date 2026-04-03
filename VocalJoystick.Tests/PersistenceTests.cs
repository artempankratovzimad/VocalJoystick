using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VocalJoystick.Core.Interfaces;
using VocalJoystick.Core.Models;
using VocalJoystick.Infrastructure;
using VocalJoystick.Infrastructure.Persistence;

namespace VocalJoystick.Tests;

[TestClass]
public sealed class PersistenceTests
{
    [TestMethod]
    public async Task Settings_SaveAndLoad_MatchesOriginal()
    {
        using var storage = new TestStorageLocation();
        var repo = new JsonSettingsRepository(storage);
        var settings = AppSettings.CreateDefault().WithMode(AppMode.Working, "profile-1");

        await repo.SaveSettingsAsync(settings, CancellationToken.None);
        var reloaded = await repo.LoadSettingsAsync(CancellationToken.None);

        Assert.AreEqual(settings.LastMode, reloaded.LastMode);
        Assert.AreEqual(settings.ActiveProfileId, reloaded.ActiveProfileId);
        Assert.IsTrue(reloaded.LastUpdated >= settings.LastUpdated);
    }

    [TestMethod]
    public async Task Settings_MissingFile_ReturnsDefaults()
    {
        using var storage = new TestStorageLocation();
        var repo = new JsonSettingsRepository(storage);

        var latest = await repo.LoadSettingsAsync(CancellationToken.None);

        Assert.AreEqual(AppMode.Stopped, latest.LastMode);
        Assert.IsNull(latest.ActiveProfileId);
    }

    [TestMethod]
    public async Task Settings_CorruptedFile_ReturnsDefaults()
    {
        using var storage = new TestStorageLocation();
        File.WriteAllText(storage.SettingsFile, "not-json");
        var repo = new JsonSettingsRepository(storage);

        var latest = await repo.LoadSettingsAsync(CancellationToken.None);

        Assert.AreEqual(AppMode.Stopped, latest.LastMode);
    }

    [TestMethod]
    public async Task Profile_SaveAndLoadConfiguration_PersistsFile()
    {
        using var storage = new TestStorageLocation();
        var repo = new JsonProfileRepository(storage);
        var profile = new UserProfileMetadata { DisplayName = "Tester" };

        await repo.SaveProfileAsync(profile, CancellationToken.None);
        var loaded = await repo.LoadProfileConfigurationAsync(profile.Id, CancellationToken.None);

        Assert.IsNotNull(loaded);
        Assert.AreEqual(profile.Id, loaded.Metadata.Id);
        Assert.AreEqual(Enum.GetValues<VocalAction>().Length, loaded.ActionConfigurations.Count);
    }

    [TestMethod]
    public async Task Profile_LoadOrCreateDefaults_WhenMissingConfiguration()
    {
        using var storage = new TestStorageLocation();
        var repo = new JsonProfileRepository(storage);
        var profile = new UserProfileMetadata { DisplayName = "Fallback" };

        var configuration = await repo.LoadOrCreateProfileConfigurationAsync(profile, CancellationToken.None);

        Assert.IsNotNull(configuration);
        Assert.AreEqual(profile.DisplayName, configuration.Metadata.DisplayName);
        var expectedActions = Enum.GetValues<VocalAction>().Cast<VocalAction>().ToArray();
        CollectionAssert.AreEquivalent(expectedActions, configuration.ActionConfigurations.Keys.ToArray());
    }

    [TestMethod]
    public async Task Profile_LoadOrCreate_DiscardCorruptedConfiguration()
    {
        using var storage = new TestStorageLocation();
        var profile = new UserProfileMetadata { DisplayName = "Corrupt" };
        var repo = new JsonProfileRepository(storage);
        var path = storage.GetProfileFile(profile.Id);
        File.WriteAllText(path, "corrupt");

        var configuration = await repo.LoadOrCreateProfileConfigurationAsync(profile, CancellationToken.None);

        Assert.IsNotNull(configuration);
        Assert.AreEqual(Enum.GetValues<VocalAction>().Length, configuration.ActionConfigurations.Count);
    }
}

internal sealed class TestStorageLocation : IAppStorageLocation, IDisposable
{
    private readonly string _baseFolder;

    public TestStorageLocation()
    {
        _baseFolder = Path.Combine(Path.GetTempPath(), "VocalJoystick.Tests", Path.GetRandomFileName());
        Directory.CreateDirectory(_baseFolder);
    }

    public string BaseFolder => _baseFolder;
    public string ProfilesFolder => Path.Combine(BaseFolder, "Profiles");
    public string LogsFolder => Path.Combine(BaseFolder, "Logs");
    public string SettingsFile => Path.Combine(BaseFolder, "settings.json");

    public string GetProfileFile(string profileId)
    {
        var folder = Path.Combine(ProfilesFolder, profileId);
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, "profile.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(BaseFolder))
        {
            Directory.Delete(BaseFolder, true);
        }
    }
}
