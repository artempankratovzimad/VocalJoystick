using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VocalJoystick.Core.Interfaces;
using VocalJoystick.Core.Models;

namespace VocalJoystick.Infrastructure.Stubs;

public sealed class StubAudioCaptureService : IAudioCaptureService
{
    public bool IsCapturing { get; private set; }
    public event EventHandler<AudioBufferEventArgs>? BufferCaptured;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        IsCapturing = true;
        BufferCaptured?.Invoke(this, new AudioBufferEventArgs(new AudioBuffer(Array.Empty<float>(), 0)));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        IsCapturing = false;
        return Task.CompletedTask;
    }
}

public sealed class StubVoiceActivityDetector : IVoiceActivityDetector
{
    public bool IsVoiceActivity(AudioBuffer buffer)
    {
        return buffer.Samples.Any(sample => Math.Abs(sample) > 0.001f);
    }
}

public sealed class StubPitchDetector : IPitchDetector
{
    public Task<double?> DetectPitchAsync(AudioBuffer buffer, CancellationToken cancellationToken)
    {
        return Task.FromResult<double?>(418.0);
    }
}

public sealed class StubFeatureExtractor : IFeatureExtractor
{
    public Task<float[]> ExtractFeaturesAsync(AudioBuffer buffer, CancellationToken cancellationToken)
    {
        var feature = buffer.Samples.Take(8).Select(s => Math.Abs(s)).Select(Convert.ToSingle).ToArray();
        return Task.FromResult(feature.Length == 0 ? new float[0] : feature);
    }
}

public sealed class StubCommandRecognizer : ICommandRecognizer
{
    public Task<RecognitionResult?> RecognizeAsync(float[] features, CancellationToken cancellationToken)
    {
        if (features.Length == 0)
        {
            return Task.FromResult<RecognitionResult?>(null);
        }

        return Task.FromResult<RecognitionResult?>(new RecognitionResult
        {
            Action = VocalAction.MoveRight,
            Confidence = 0.42,
            Description = "Stub command recognizes MoveRight"
        });
    }
}

public sealed class StubMouseController : IMouseController
{
    public Task ClickAsync(VocalAction action, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task DoubleClickAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task MoveAsync(VocalAction direction, double intensity, CancellationToken cancellationToken) => Task.CompletedTask;
}
