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
    private readonly string _settingsPath = AppPaths.SettingsFile;

    public async Task<SettingsSnapshot?> LoadSettingsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(_settingsPath))
        {
            return null;
        }

        var content = await File.ReadAllTextAsync(_settingsPath, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<SettingsSnapshot>(content, _options);
    }

    public async Task SaveSettingsAsync(SettingsSnapshot snapshot, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var content = JsonSerializer.Serialize(snapshot, _options);
        await File.WriteAllTextAsync(_settingsPath, content, cancellationToken).ConfigureAwait(false);
    }
}
