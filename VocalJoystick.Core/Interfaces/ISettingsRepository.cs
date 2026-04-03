using System.Threading;
using System.Threading.Tasks;
using VocalJoystick.Core.Models;

namespace VocalJoystick.Core.Interfaces;

public interface ISettingsRepository
{
    Task<AppSettings> LoadSettingsAsync(CancellationToken cancellationToken);
    Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken);
}
