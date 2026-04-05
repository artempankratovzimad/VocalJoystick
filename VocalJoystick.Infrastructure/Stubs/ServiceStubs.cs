using System;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VocalJoystick.Core.Interfaces;
using VocalJoystick.Core.Models;

namespace VocalJoystick.Infrastructure.Stubs;

public sealed class StubAudioCaptureService : IAudioCaptureService
{
    private readonly List<AudioDeviceInfo> _devices = new()
    {
        new("stub-1", "Stub Microphone", 0)
    };

    private double _signalLevel;

    public bool IsCapturing { get; private set; }
    public event EventHandler<AudioBufferEventArgs>? BufferCaptured;
    public event EventHandler<double>? SignalLevelUpdated;

    public IReadOnlyList<AudioDeviceInfo> AvailableDevices => _devices;
    public AudioDeviceInfo? SelectedDevice { get; private set; }
    public double CurrentSignalLevel => _signalLevel;

    public StubAudioCaptureService()
    {
        SelectedDevice = _devices.First();
    }

    public Task SelectDeviceAsync(string? deviceId, CancellationToken cancellationToken)
    {
        SelectedDevice = _devices.FirstOrDefault(device => device.Id == deviceId) ?? _devices.First();
        SignalLevelUpdated?.Invoke(this, _signalLevel);
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        IsCapturing = true;
        _signalLevel = 0.5;
        SignalLevelUpdated?.Invoke(this, _signalLevel);
        BufferCaptured?.Invoke(this, new AudioBufferEventArgs(new AudioBuffer(Array.Empty<float>(), 0)));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        IsCapturing = false;
        _signalLevel = 0;
        SignalLevelUpdated?.Invoke(this, _signalLevel);
        return Task.CompletedTask;
    }
}

public sealed class StubVoiceActivityDetector : IVoiceActivityDetector
{
    public VoiceActivityResult Analyze(Frame frame, FrameProcessingSettings settings)
    {
        var samples = frame.Samples;
        double sumSquares = 0;
        foreach (var sample in samples)
        {
            sumSquares += sample * sample;
        }

        var rms = samples.Length > 0 ? Math.Sqrt(sumSquares / samples.Length) : 0d;
        return new VoiceActivityResult(rms >= settings.VadThreshold, rms);
    }
}

public sealed class StubPitchDetector : IPitchDetector
{
    public Task<PitchDetectionResult> DetectPitchAsync(Frame frame, CancellationToken cancellationToken)
    {
        return Task.FromResult(PitchDetectionResult.Unvoiced);
    }
}

public sealed class StubFeatureExtractor : IFeatureExtractor
{
    public Task<FeatureExtractionResult> ExtractFeaturesAsync(AudioBuffer buffer, CancellationToken cancellationToken)
    {
        var magnitude = buffer.Samples.Take(8).Select(s => Math.Abs(s)).Select(Convert.ToSingle).ToArray();
        var rms = buffer.Samples.Length == 0 ? 0d : Math.Sqrt(buffer.Samples.Sum(sample => sample * sample) / buffer.Samples.Length);
        var summary = new SampleFeatureSummary(rms, 0, 0, 0, 0, 0, 0, null);
        var featureVector = magnitude.Length == 0 ? Array.Empty<float>() : magnitude;
        var result = new FeatureExtractionResult(featureVector, summary, null);
        return Task.FromResult(result);
    }
}

public sealed class StubMouseController : IMouseController
{
    public Task ClickAsync(VocalAction action, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task DoubleClickAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task MoveAsync(VocalAction direction, double intensity, CancellationToken cancellationToken) => Task.CompletedTask;
}
