using System.IO;

namespace VocalJoystick.Infrastructure;

public sealed class AppStorageLocation : IAppStorageLocation
{
    public string BaseFolder => AppPaths.BaseFolder;
    public string ProfilesFolder => AppPaths.ProfilesFolder;
    public string LogsFolder => AppPaths.LogsFolder;
    public string SettingsFile => AppPaths.SettingsFile;

    public string GetProfileFile(string profileId)
    {
        var profileFolder = Path.Combine(ProfilesFolder, profileId);
        Directory.CreateDirectory(profileFolder);
        return Path.Combine(profileFolder, "profile.json");
    }
}
