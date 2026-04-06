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
    private readonly ClickSampleProcessor _clickSampleProcessor;
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
        _clickSampleProcessor = new ClickSampleProcessor(featureExtractor, logger);
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
        _activeSession = new RecordingSession(action, writer, waveFormat.SampleRate, filePath, settings);
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

        var samples = LoadSamples(session.FilePath);
        var processedSamples = samples;
        ClickSampleMetrics? clickMetrics = null;
        FeatureExtractionResult? extractionOverride = null;

        if (IsClickAction(action))
        {
            var clickResult = await _clickSampleProcessor.ProcessAsync(session.Action, samples, session.SampleRate, session.FrameSettings, cancellationToken).ConfigureAwait(false);
            if (!clickResult.IsSuccess)
            {
                if (File.Exists(session.FilePath))
                {
                    File.Delete(session.FilePath);
                }

                _logger.LogWarning($"Click sample rejected for {action}: {clickResult.FailureReason}");
                return null;
            }

            processedSamples = clickResult.TrimmedSamples;
            clickMetrics = clickResult.Metrics;
            extractionOverride = clickResult.FeatureExtraction;
            RewriteWaveFile(session.FilePath, processedSamples, session.SampleRate);
        }

        var metadata = await BuildMetadataAsync(session, processedSamples, extractionOverride, clickMetrics, cancellationToken).ConfigureAwait(false);
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

    private async Task<SampleMetadata> BuildMetadataAsync(RecordingSession session, float[] samples, FeatureExtractionResult? extractionOverride, ClickSampleMetrics? clickMetrics, CancellationToken cancellationToken)
    {
        var extraction = extractionOverride ?? await _featureExtractor.ExtractFeaturesAsync(new AudioBuffer(samples, session.SampleRate), cancellationToken).ConfigureAwait(false);
        var duration = samples.Length / (double)Math.Max(1, session.SampleRate);
        var relativePath = Path.GetRelativePath(_storageLocation.BaseFolder, session.FilePath);
        var directionalMetrics = DirectionalSampleMetrics.FromFeatureVector(extraction.DirectionalFeature);
        return new SampleMetadata(session.FileName, relativePath, DateTimeOffset.UtcNow, duration, extraction.Summary, directionalMetrics, clickMetrics);
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

    private static bool IsClickAction(VocalAction action)
        => action is VocalAction.LeftClick or VocalAction.RightClick or VocalAction.DoubleClick;

    private static void RewriteWaveFile(string path, float[] samples, int sampleRate)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        using var writer = new WaveFileWriter(path, new WaveFormat(sampleRate, 16, 1));
        var buffer = new byte[samples.Length * 2];
        var offset = 0;
        foreach (var sample in samples)
        {
            var clamped = Math.Max(short.MinValue, Math.Min(short.MaxValue, (short)(sample * 32767)));
            buffer[offset++] = (byte)(clamped & 0xFF);
            buffer[offset++] = (byte)((clamped >> 8) & 0xFF);
        }

        if (buffer.Length > 0)
        {
            writer.Write(buffer, 0, buffer.Length);
        }
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
        public RecordingSession(VocalAction action, WaveFileWriter writer, int sampleRate, string filePath, FrameProcessingSettings frameSettings)
        {
            Action = action;
            Writer = writer;
            SampleRate = sampleRate;
            FilePath = filePath;
            FileName = Path.GetFileName(filePath);
            FrameSettings = frameSettings;
        }

        public VocalAction Action { get; }
        public WaveFileWriter Writer { get; }
        public int SampleRate { get; }
        public string FilePath { get; }
        public string FileName { get; }
        public long SamplesWritten { get; private set; }
        public FrameProcessingSettings FrameSettings { get; }

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
