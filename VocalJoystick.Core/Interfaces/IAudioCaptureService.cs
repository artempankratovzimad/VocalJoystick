using VocalJoystick.Core.Models;

using System.Threading;
using System.Threading.Tasks;

namespace VocalJoystick.Core.Interfaces;

public interface IAudioCaptureService
{
    bool IsCapturing { get; }
    event EventHandler<AudioBufferEventArgs>? BufferCaptured;
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}
