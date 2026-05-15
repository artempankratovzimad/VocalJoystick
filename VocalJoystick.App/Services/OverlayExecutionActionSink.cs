using System.Threading;
using System.Threading.Tasks;
using VocalJoystick.Core.Models;

namespace VocalJoystick.App.Services;

public sealed class OverlayExecutionActionSink : IExecutionActionSink
{
    private readonly ITestOverlayController _testOverlayController;

    public OverlayExecutionActionSink(ITestOverlayController testOverlayController)
    {
        _testOverlayController = testOverlayController;
    }

    public string SinkName => "Test";

    public Task MoveAsync(VocalAction direction, double intensity, CancellationToken cancellationToken)
    {
        return _testOverlayController.MoveAsync(direction, intensity, cancellationToken);
    }

    public Task ClickAsync(VocalAction action, CancellationToken cancellationToken)
    {
        return _testOverlayController.ShowClickLabelAsync(action, cancellationToken);
    }

    public Task DoubleClickAsync(CancellationToken cancellationToken)
    {
        return _testOverlayController.ShowClickLabelAsync(VocalAction.DoubleClick, cancellationToken);
    }
}
