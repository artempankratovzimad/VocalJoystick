using System.Threading;
using System.Threading.Tasks;
using VocalJoystick.Core.Models;

namespace VocalJoystick.App.Services;

public interface IExecutionActionSink
{
    string SinkName { get; }
    Task MoveAsync(VocalAction direction, double intensity, CancellationToken cancellationToken);
    Task ClickAsync(VocalAction action, CancellationToken cancellationToken);
    Task DoubleClickAsync(CancellationToken cancellationToken);
}
