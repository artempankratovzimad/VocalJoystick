using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VocalJoystick.Core.Models;

namespace VocalJoystick.Core.Interfaces;

public interface IAudioCaptureService
{
    bool IsCapturing { get; }
    event EventHandler<AudioBufferEventArgs>? BufferCaptured;
    event EventHandler<double>? SignalLevelUpdated;

    IReadOnlyList<AudioDeviceInfo> AvailableDevices { get; }
    AudioDeviceInfo? SelectedDevice { get; }
    double CurrentSignalLevel { get; }

    Task SelectDeviceAsync(string? deviceId, CancellationToken cancellationToken);
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}
