using System.Threading;
using System.Threading.Tasks;
using VocalJoystick.Core.Models;

namespace VocalJoystick.Core.Interfaces;

public interface ISampleRecorder
{
    bool IsRecording { get; }
    VocalAction? CurrentAction { get; }
    Task StartRecordingAsync(string profileId, VocalAction action, FrameProcessingSettings settings, CancellationToken cancellationToken);
    Task<SampleMetadata?> StopRecordingAsync(string profileId, VocalAction action, CancellationToken cancellationToken);
    Task DeleteSampleAsync(string profileId, SampleMetadata sample, CancellationToken cancellationToken);
    Task DeleteSamplesAsync(string profileId, VocalAction action, CancellationToken cancellationToken);
}
