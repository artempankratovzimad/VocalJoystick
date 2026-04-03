using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VocalJoystick.Core.Interfaces;
using VocalJoystick.Core.Models;

namespace VocalJoystick.Infrastructure.Persistence;

public sealed class JsonSettingsRepository : ISettingsRepository
{
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };
    private readonly IAppStorageLocation _storageLocation;

    public JsonSettingsRepository(IAppStorageLocation storageLocation)
    {
        _storageLocation = storageLocation;
        var directory = Path.GetDirectoryName(storageLocation.SettingsFile);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    public async Task<AppSettings> LoadSettingsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(_storageLocation.SettingsFile))
        {
            return AppSettings.CreateDefault();
        }

        try
        {
            var content = await File.ReadAllTextAsync(_storageLocation.SettingsFile, cancellationToken).ConfigureAwait(false);
            var settings = JsonSerializer.Deserialize<AppSettings>(content, _options);
            return settings ?? AppSettings.CreateDefault();
        }
        catch (JsonException)
        {
            return AppSettings.CreateDefault();
        }
    }

    public async Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var content = JsonSerializer.Serialize(settings, _options);
        await File.WriteAllTextAsync(_storageLocation.SettingsFile, content, cancellationToken).ConfigureAwait(false);
    }
}
