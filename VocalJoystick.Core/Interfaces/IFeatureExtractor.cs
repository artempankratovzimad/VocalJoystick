using System.Threading;
using System.Threading.Tasks;
using VocalJoystick.Core.Models;

namespace VocalJoystick.Core.Interfaces;

public interface IFeatureExtractor
{
    Task<float[]> ExtractFeaturesAsync(AudioBuffer buffer, CancellationToken cancellationToken);
}
