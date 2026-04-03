using System;
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
    private RecordingSession? _activeSession;

    public SampleRecorder(IAudioCaptureService audioCaptureService, IAppStorageLocation storageLocation, ILogger logger)
    {
        _audioCaptureService = audioCaptureService;
        _storageLocation = storageLocation;
        _logger = logger;
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

    public Task<SampleMetadata?> StopRecordingAsync(string profileId, VocalAction action, CancellationToken cancellationToken)
    {
        if (_activeSession is null || _activeSession.Action != action)
        {
            return Task.FromResult<SampleMetadata?>(null);
        }

        _activeSession.Dispose();
        var session = _activeSession;
        _activeSession = null;
        var duration = session.SamplesWritten / (double)session.SampleRate;
        var relativePath = Path.GetRelativePath(_storageLocation.BaseFolder, session.FilePath);
        var metadata = new SampleMetadata(session.FileName, relativePath, DateTimeOffset.UtcNow, duration);
        _logger.LogInfo($"Recording stopped for {action}, duration {duration:F2}s");
        return Task.FromResult<SampleMetadata?>(metadata);
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

    private void OnBufferCaptured(object? sender, AudioBufferEventArgs args)
    {
        _activeSession?.Write(args.Buffer.Samples);
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
