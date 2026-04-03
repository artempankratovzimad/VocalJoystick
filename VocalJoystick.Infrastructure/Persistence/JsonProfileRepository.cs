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

    private string ActiveProfilePath => Path.Combine(AppPaths.BaseFolder, ActiveProfileFileName);

    public async Task<IEnumerable<UserProfileMetadata>> ListProfilesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var folder = AppPaths.ProfilesFolder;
        if (!Directory.Exists(folder))
        {
            return Array.Empty<UserProfileMetadata>();
        }

        var results = new List<UserProfileMetadata>();
        foreach (var file in Directory.EnumerateFiles(folder, "*.json"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var content = await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);
                var profile = JsonSerializer.Deserialize<UserProfileMetadata>(content, _options);
                if (profile is not null)
                {
                    results.Add(profile);
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

        var path = GetProfilePath(id.Trim());
        if (!File.Exists(path))
        {
            return null;
        }

        var content = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<UserProfileMetadata>(content, _options);
    }

    public async Task SaveProfileAsync(UserProfileMetadata profile, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = GetProfilePath(profile.Id);
        var content = JsonSerializer.Serialize(profile, _options);
        await File.WriteAllTextAsync(path, content, cancellationToken).ConfigureAwait(false);
    }

    public async Task SetActiveProfileAsync(UserProfileMetadata profile, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await File.WriteAllTextAsync(ActiveProfilePath, profile.Id, cancellationToken).ConfigureAwait(false);
    }

    private static string GetProfilePath(string id) => Path.Combine(AppPaths.ProfilesFolder, $"{id}.json");
}
