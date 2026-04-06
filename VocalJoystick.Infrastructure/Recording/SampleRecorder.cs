using System;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using VocalJoystick.Core.Interfaces;
using VocalJoystick.Core.Models;

namespace VocalJoystick.Infrastructure.Recording;

public sealed class SampleRecorder : ISampleRecorder, IDisposable
{
    private readonly IAudioCaptureService _audioCaptureService;
    private readonly IAppStorageLocation _storageLocation;
    private readonly ILogger _logger;
    private readonly IFeatureExtractor _featureExtractor;
    private RecordingSession? _activeSession;

    public SampleRecorder(
        IAudioCaptureService audioCaptureService,
        IAppStorageLocation storageLocation,
        ILogger logger,
        IFeatureExtractor featureExtractor)
    {
        _audioCaptureService = audioCaptureService;
        _storageLocation = storageLocation;
        _logger = logger;
        _featureExtractor = featureExtractor;
        _audioCaptureService.BufferCaptured += OnBufferCaptured;
    }

    public bool IsRecording => _activeSession is not null;
    public VocalAction? CurrentAction => _activeSession?.Action;

    public Task StartRecordingAsync(string profileId, VocalAction action, FrameProcessingSettings settings, CancellationToken cancellationToken)
    {
        if (IsRecording)
        {
            throw new InvalidOperationException("Already recording another action");
        }

        var folder = GetActionFolder(profileId, action);
        Directory.CreateDirectory(folder);
        var fileName = $"sample-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.wav";
        var filePath = Path.Combine(folder, fileName);
        var waveFormat = new WaveFormat(16_000, 16, 1);
        var writer = new WaveFileWriter(filePath, waveFormat);
        _activeSession = new RecordingSession(action, writer, waveFormat.SampleRate, filePath);
        _logger.LogInfo($"Recording started for {action} -> {filePath}");
        return Task.CompletedTask;
    }

    public async Task<SampleMetadata?> StopRecordingAsync(string profileId, VocalAction action, CancellationToken cancellationToken)
    {
        if (_activeSession is null || _activeSession.Action != action)
        {
            return null;
        }

        var session = _activeSession;
        _activeSession = null;
        session.Dispose();

        var metadata = await BuildMetadataAsync(session, cancellationToken).ConfigureAwait(false);
        _logger.LogInfo($"Recording stopped for {action}, duration {metadata.DurationSeconds:F2}s");
        return metadata;
    }

    public Task DeleteSamplesAsync(string profileId, VocalAction action, CancellationToken cancellationToken)
    {
        var folder = GetActionFolder(profileId, action);
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, true);
            _logger.LogInfo($"Deleted samples for {action}");
        }

        return Task.CompletedTask;
    }

    public Task DeleteSampleAsync(string profileId, SampleMetadata sample, CancellationToken cancellationToken)
    {
        if (sample is null)
        {
            throw new ArgumentNullException(nameof(sample));
        }

        var path = Path.Combine(_storageLocation.BaseFolder, sample.RelativePath ?? string.Empty);
        if (File.Exists(path))
        {
            File.Delete(path);
            _logger.LogInfo($"Deleted sample file {sample.FileName}");
        }
        else
        {
            _logger.LogWarning($"Sample file not found for deletion: {sample.RelativePath}");
        }

        return Task.CompletedTask;
    }

    private async Task<SampleMetadata> BuildMetadataAsync(RecordingSession session, CancellationToken cancellationToken)
    {
        var samples = LoadSamples(session.FilePath);
        var buffer = new AudioBuffer(samples, session.SampleRate);
        var extraction = await _featureExtractor.ExtractFeaturesAsync(buffer, cancellationToken).ConfigureAwait(false);
        var duration = samples.Length / (double)Math.Max(1, session.SampleRate);
        var relativePath = Path.GetRelativePath(_storageLocation.BaseFolder, session.FilePath);
        var directionalMetrics = DirectionalSampleMetrics.FromFeatureVector(extraction.DirectionalFeature);
        return new SampleMetadata(session.FileName, relativePath, DateTimeOffset.UtcNow, duration, extraction.Summary, directionalMetrics);
    }

    private static float[] LoadSamples(string path)
    {
        if (!File.Exists(path))
        {
            return Array.Empty<float>();
        }

        using var reader = new WaveFileReader(path);
        var samples = new List<float>();
        while (reader.Position < reader.Length)
        {
            var frame = reader.ReadNextSampleFrame();
            if (frame is null || frame.Length == 0)
            {
                break;
            }

            samples.Add(frame[0]);
        }

        return samples.ToArray();
    }

    private void OnBufferCaptured(object? sender, AudioBufferEventArgs args)
    {
        if (_activeSession is not null)
        {
            _logger.LogInfo($"Recording session {_activeSession.Action}: writing {args.Buffer.Samples.Length} samples at {args.Buffer.SampleRate}Hz");
            _activeSession.Write(args.Buffer.Samples);
        }
    }

    private string GetActionFolder(string profileId, VocalAction action)
    {
        return Path.Combine(_storageLocation.ProfilesFolder, profileId, "Samples", action.ToString());
    }

    public void Dispose()
    {
        _audioCaptureService.BufferCaptured -= OnBufferCaptured;
        _activeSession?.Dispose();
    }

    private sealed class RecordingSession : IDisposable
    {
        public RecordingSession(VocalAction action, WaveFileWriter writer, int sampleRate, string filePath)
        {
            Action = action;
            Writer = writer;
            SampleRate = sampleRate;
            FilePath = filePath;
            FileName = Path.GetFileName(filePath);
        }

        public VocalAction Action { get; }
        public WaveFileWriter Writer { get; }
        public int SampleRate { get; }
        public string FilePath { get; }
        public string FileName { get; }
        public long SamplesWritten { get; private set; }

        public void Write(float[] samples)
        {
            var buffer = new byte[samples.Length * 2];
            var offset = 0;
            foreach (var sample in samples)
            {
                var clamped = Math.Max(short.MinValue, Math.Min(short.MaxValue, (short)(sample * 32767)));
                buffer[offset++] = (byte)(clamped & 0xFF);
                buffer[offset++] = (byte)((clamped >> 8) & 0xFF);
            }

            Writer.Write(buffer, 0, buffer.Length);
            SamplesWritten += samples.Length;
        }

        public void Dispose()
        {
            Writer.Dispose();
        }
    }
}
