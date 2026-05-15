using System.Threading;
using System.Threading.Tasks;
using VocalJoystick.Core.Models;

namespace VocalJoystick.App.Services;

public interface ITestOverlayController
{
    void Start();
    void Stop();
    Task MoveAsync(VocalAction direction, double intensity, CancellationToken cancellationToken);
    Task ShowClickLabelAsync(VocalAction action, CancellationToken cancellationToken);
}
