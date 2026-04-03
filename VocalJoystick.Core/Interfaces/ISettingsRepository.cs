using System.Threading;
using System.Threading.Tasks;
using VocalJoystick.Core.Models;

namespace VocalJoystick.Core.Interfaces;

public interface ISettingsRepository
{
    Task<SettingsSnapshot?> LoadSettingsAsync(CancellationToken cancellationToken);
    Task SaveSettingsAsync(SettingsSnapshot snapshot, CancellationToken cancellationToken);
}
