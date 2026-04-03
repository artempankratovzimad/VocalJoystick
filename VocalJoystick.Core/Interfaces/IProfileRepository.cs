using System.Collections.Generic;
using System.Threading;
using VocalJoystick.Core.Models;

namespace VocalJoystick.Core.Interfaces;

public interface IProfileRepository
{
    Task<UserProfileMetadata?> GetActiveProfileAsync(CancellationToken cancellationToken);
    Task<IEnumerable<UserProfileMetadata>> ListProfilesAsync(CancellationToken cancellationToken);
    Task SaveProfileAsync(UserProfileMetadata profile, CancellationToken cancellationToken);
    Task SetActiveProfileAsync(UserProfileMetadata profile, CancellationToken cancellationToken);
}
