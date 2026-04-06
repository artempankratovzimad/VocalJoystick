using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VocalJoystick.Core.Interfaces;
using VocalJoystick.Core.Models;
using VocalJoystick.Infrastructure.Recording;

namespace VocalJoystick.Tests;

[TestClass]
public sealed class SampleRecorderTests
{
    [TestMethod]
    public async Task DeleteSample_RemovesFileFromStorage()
    {
        using var storage = new TestStorageLocation();
        var service = new StubAudioCaptureService();
        var logger = new StubLogger();
        var extractor = new StubFeatureExtractor();
        using var recorder = new SampleRecorder(service, storage, logger, extractor);

        const string profileId = "profile-1";
        var actionFolder = Path.Combine(storage.ProfilesFolder, profileId, "Samples", VocalAction.MoveUp.ToString());
        Directory.CreateDirectory(actionFolder);
        var filePath = Path.Combine(actionFolder, "sample.wav");
        await File.WriteAllTextAsync(filePath, "data", CancellationToken.None);

        var relativePath = Path.GetRelativePath(storage.BaseFolder, filePath);
        var metadata = new SampleMetadata(
            Path.GetFileName(filePath),
            relativePath,
            DateTimeOffset.UtcNow,
            1.0,
            new SampleFeatureSummary(0.1, 0.2, 220, 1, 0.3, 0.4, 1, null));

        await recorder.DeleteSampleAsync(profileId, metadata, CancellationToken.None);

        Assert.IsFalse(File.Exists(filePath));
    }
}

internal sealed class StubAudioCaptureService : IAudioCaptureService
{
    public bool IsCapturing { get; private set; }
    #pragma warning disable CS0067
    public event EventHandler<AudioBufferEventArgs>? BufferCaptured;
    public event EventHandler<double>? SignalLevelUpdated;
    #pragma warning restore CS0067
    public IReadOnlyList<AudioDeviceInfo> AvailableDevices { get; } = Array.Empty<AudioDeviceInfo>();
    public AudioDeviceInfo? SelectedDevice { get; private set; }
    public double CurrentSignalLevel => 0;

    public Task SelectDeviceAsync(string? deviceId, CancellationToken cancellationToken)
    {
        SelectedDevice = new AudioDeviceInfo(deviceId ?? "stub", "Stub device", 0);
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        IsCapturing = true;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        IsCapturing = false;
        return Task.CompletedTask;
    }
}

internal sealed class StubLogger : ILogger
{
    public void LogInfo(string message) { }
    public void LogWarning(string message) { }
    public void LogError(string message, Exception? exception = null) { }
}

internal sealed class StubFeatureExtractor : IFeatureExtractor
{
    public Task<FeatureExtractionResult> ExtractFeaturesAsync(AudioBuffer buffer, CancellationToken cancellationToken)
    {
        var summary = new SampleFeatureSummary(0, 0, 0, 0, 0, 0, 0, null);
        return Task.FromResult(new FeatureExtractionResult(Array.Empty<float>(), summary));
    }
}
