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
            using var document = JsonDocument.Parse(content);
            var settings = JsonSerializer.Deserialize<AppSettings>(content, _options);
            if (settings is null)
            {
                return AppSettings.CreateDefault();
            }

            return ApplyLegacyMovementSpeed(document.RootElement, settings);
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

    private static AppSettings ApplyLegacyMovementSpeed(JsonElement root, AppSettings settings)
    {
        if (root.TryGetProperty("MovementStartSpeed", out _) || root.TryGetProperty("MovementEndSpeed", out _))
        {
            return settings;
        }

        if (!root.TryGetProperty("MovementSpeed", out var legacySpeedElement))
        {
            return settings;
        }

        if (!legacySpeedElement.TryGetDouble(out var legacySpeed))
        {
            return settings;
        }

        return settings with
        {
            MovementStartSpeed = legacySpeed,
            MovementEndSpeed = legacySpeed
        };
    }
}
