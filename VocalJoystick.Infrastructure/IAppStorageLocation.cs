namespace VocalJoystick.Infrastructure;

public interface IAppStorageLocation
{
    string BaseFolder { get; }
    string ProfilesFolder { get; }
    string LogsFolder { get; }
    string SettingsFile { get; }
    string GetProfileFile(string profileId);
}
