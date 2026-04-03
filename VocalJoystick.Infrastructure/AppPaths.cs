using System;
using System.IO;

namespace VocalJoystick.Infrastructure;

internal static class AppPaths
{
    private static readonly Lazy<string> BaseFolderValue = new(() =>
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VocalJoystick");
        Directory.CreateDirectory(path);
        return path;
    });

    public static string BaseFolder => BaseFolderValue.Value;
    public static string ProfilesFolder => CreateSubFolder("Profiles");
    public static string LogsFolder => CreateSubFolder("Logs");
    public static string SettingsFile => Path.Combine(BaseFolder, "settings.json");

    private static string CreateSubFolder(string name)
    {
        var folder = Path.Combine(BaseFolder, name);
        Directory.CreateDirectory(folder);
        return folder;
    }
}
