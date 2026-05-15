using System.Threading;
using System.Threading.Tasks;
using VocalJoystick.Core.Interfaces;
using VocalJoystick.Core.Models;

namespace VocalJoystick.App.Services;

public sealed class CursorExecutionActionSink : IExecutionActionSink
{
    private readonly IMouseController _mouseController;

    public CursorExecutionActionSink(IMouseController mouseController)
    {
        _mouseController = mouseController;
    }

    public string SinkName => "Working";

    public Task MoveAsync(VocalAction direction, double intensity, CancellationToken cancellationToken)
    {
        return _mouseController.MoveAsync(direction, intensity, cancellationToken);
    }

    public Task ClickAsync(VocalAction action, CancellationToken cancellationToken)
    {
        return _mouseController.ClickAsync(action, cancellationToken);
    }

    public Task DoubleClickAsync(CancellationToken cancellationToken)
    {
        return _mouseController.DoubleClickAsync(cancellationToken);
    }
}
