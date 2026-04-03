using System.Threading;
using System.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using VocalJoystick.Core.Models;

namespace VocalJoystick.Core.Interfaces;

public interface IPitchDetector
{
    Task<PitchDetectionResult> DetectPitchAsync(Frame frame, CancellationToken cancellationToken);
}
