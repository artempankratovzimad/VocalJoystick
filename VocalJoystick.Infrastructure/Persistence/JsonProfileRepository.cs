using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VocalJoystick.Core.Interfaces;
using VocalJoystick.Core.Models;

namespace VocalJoystick.Infrastructure.Persistence;

public sealed class JsonProfileRepository : IProfileRepository
{
    private const string ActiveProfileFileName = "active-profile.json";
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };
    private readonly IAppStorageLocation _storageLocation;

    private string ActiveProfilePath => Path.Combine(_storageLocation.BaseFolder, ActiveProfileFileName);

    public JsonProfileRepository(IAppStorageLocation storageLocation)
    {
        _storageLocation = storageLocation;
        Directory.CreateDirectory(_storageLocation.ProfilesFolder);
    }

    public async Task<IEnumerable<UserProfileMetadata>> ListProfilesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Directory.Exists(_storageLocation.ProfilesFolder))
        {
            return Array.Empty<UserProfileMetadata>();
        }

        var results = new List<UserProfileMetadata>();
        foreach (var directory in Directory.EnumerateDirectories(_storageLocation.ProfilesFolder))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var candidate = Path.Combine(directory, "profile.json");
            if (!File.Exists(candidate))
            {
                continue;
            }

            try
            {
                var content = await File.ReadAllTextAsync(candidate, cancellationToken).ConfigureAwait(false);
                var config = JsonSerializer.Deserialize<ProfileConfiguration>(content, _options);
                if (config is not null)
                {
                    results.Add(config.Metadata);
                }
            }
            catch
            {
                // ignore corrupted files for now
            }
        }

        return results;
    }

    public async Task<UserProfileMetadata?> GetActiveProfileAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(ActiveProfilePath))
        {
            return null;
        }

        var id = await File.ReadAllTextAsync(ActiveProfilePath, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var config = await LoadProfileConfigurationAsync(id.Trim(), cancellationToken).ConfigureAwait(false);
        return config?.Metadata;
    }

    public async Task SaveProfileAsync(UserProfileMetadata profile, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var configuration = await LoadOrCreateProfileConfigurationAsync(profile, cancellationToken).ConfigureAwait(false);
        configuration.Metadata = profile;
        await SaveProfileConfigurationAsync(configuration, cancellationToken).ConfigureAwait(false);
    }

    public async Task SetActiveProfileAsync(UserProfileMetadata profile, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await File.WriteAllTextAsync(ActiveProfilePath, profile.Id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProfileConfiguration?> LoadProfileConfigurationAsync(string profileId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = _storageLocation.GetProfileFile(profileId);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var content = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            var configuration = JsonSerializer.Deserialize<ProfileConfiguration>(content, _options);
            return configuration;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async Task<ProfileConfiguration> LoadOrCreateProfileConfigurationAsync(UserProfileMetadata profile, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var existing = await LoadProfileConfigurationAsync(profile.Id, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            existing.Metadata = profile;
            return existing;
        }

        var configuration = ProfileConfiguration.CreateDefault(profile);
        await SaveProfileConfigurationAsync(configuration, cancellationToken).ConfigureAwait(false);
        return configuration;
    }

    public async Task SaveProfileConfigurationAsync(ProfileConfiguration configuration, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = _storageLocation.GetProfileFile(configuration.Metadata.Id);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var content = JsonSerializer.Serialize(configuration, _options);
        await File.WriteAllTextAsync(path, content, cancellationToken).ConfigureAwait(false);
    }
}
