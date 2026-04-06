using System;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VocalJoystick.Core.Interfaces;
using VocalJoystick.Core.Models;
using VocalJoystick.Core.Services;

namespace VocalJoystick.Infrastructure.Recording;

public sealed class ClickSampleProcessor
{
    private const double MinimumDurationMs = 50;
    private const double MaximumDurationMs = 1000;
    private const double MinimumPeakAmplitude = 0.2;
    private const double MinimumRmsThreshold = 0.01;
    private const double MinimumStartThreshold = 0.005;
    private const double EndThresholdFactor = 0.6;
    private const int PaddingMs = 10;
    private const int FrameDurationMs = 15;
    private const int RequiredBelowFrames = 2;
    private readonly IFeatureExtractor _featureExtractor;
    private readonly ClickMetricsExtractor _metricsExtractor;
    private readonly ILogger _logger;

    public ClickSampleProcessor(IFeatureExtractor featureExtractor, ILogger logger)
    {
        _featureExtractor = featureExtractor;
        _metricsExtractor = new ClickMetricsExtractor(featureExtractor);
        _logger = logger;
    }

    public async Task<ClickProcessingResult> ProcessAsync(VocalAction action, float[] samples, int sampleRate, FrameProcessingSettings frameSettings, CancellationToken cancellationToken)
    {
        if (sampleRate <= 0)
        {
            return ClickProcessingResult.Failure("Invalid sample rate");
        }

        if (samples.Length == 0)
        {
            return ClickProcessingResult.Failure("Click sample is empty");
        }

        var startThreshold = Math.Max(frameSettings.VadThreshold, MinimumStartThreshold);
        var endThreshold = Math.Max(startThreshold * EndThresholdFactor, startThreshold - 0.001);
        if (endThreshold <= 0)
        {
            endThreshold = startThreshold * EndThresholdFactor;
        }

        var maxEvents = action == VocalAction.DoubleClick ? 2 : 1;
        var segment = SegmentClickEvent(samples, sampleRate, startThreshold, endThreshold, maxEvents);
        if (!segment.IsSuccess)
        {
            return ClickProcessingResult.Failure(segment.FailureReason ?? "Click event not detected");
        }

        var trimmed = segment.TrimmedSamples;
        var clickResult = await _metricsExtractor.ExtractAsync(trimmed, sampleRate, cancellationToken).ConfigureAwait(false);
        var metrics = clickResult.Metrics;
        var durationMs = metrics.DurationMs;
        if (durationMs < MinimumDurationMs)
        {
            return ClickProcessingResult.Failure("Click duration is too short");
        }

        if (durationMs > MaximumDurationMs)
        {
            return ClickProcessingResult.Failure("Click duration is too long");
        }

        var minRms = Math.Max(startThreshold, MinimumRmsThreshold);
        var minPeak = Math.Max(MinimumPeakAmplitude, startThreshold * 2);
        if (metrics.RmsEnergy < minRms)
        {
            return ClickProcessingResult.Failure("Click is too quiet");
        }

        if (metrics.PeakAmplitude < minPeak)
        {
            return ClickProcessingResult.Failure("Click peak amplitude too low");
        }

        return ClickProcessingResult.Success(trimmed, metrics, clickResult.FeatureExtraction);
    }

    private static SegmentResult SegmentClickEvent(float[] samples, int sampleRate, double startThreshold, double endThreshold, int maxEvents)
    {
        var frameLength = Math.Clamp((int)Math.Round(sampleRate * (FrameDurationMs / 1000.0)), 64, 512);
        var hopSize = Math.Max(1, frameLength / 2);
        var paddingSamples = Math.Max(1, (int)Math.Round(sampleRate * (PaddingMs / 1000.0)));
        var frameStarts = new List<int>();
        var frameLengths = new List<int>();
        var frameRms = new List<double>();

        var index = 0;
        while (index < samples.Length)
        {
            var length = Math.Min(frameLength, samples.Length - index);
            double sumSquares = 0;
            for (var sampleIndex = 0; sampleIndex < length; sampleIndex++)
            {
                var value = samples[index + sampleIndex];
                sumSquares += value * value;
            }

            var rms = length == 0 ? 0 : Math.Sqrt(sumSquares / length);
            frameStarts.Add(index);
            frameLengths.Add(length);
            frameRms.Add(rms);

            if (length < frameLength)
            {
                break;
            }

            index += hopSize;
        }

        if (frameRms.Count == 0)
        {
            return SegmentResult.Failure("No frames available");
        }

        var startFrames = new List<int>();
        var endFrames = new List<int>();
        var inEvent = false;
        var consecutiveBelow = 0;
        var lastActiveFrame = -1;
        var frameCount = frameRms.Count;

        for (var frame = 0; frame < frameCount; frame++)
        {
            var rms = frameRms[frame];
            if (!inEvent)
            {
                if (rms >= startThreshold)
                {
                    startFrames.Add(frame);
                    inEvent = true;
                    lastActiveFrame = frame;
                    consecutiveBelow = 0;
                }
            }
            else
            {
                if (rms >= endThreshold)
                {
                    lastActiveFrame = frame;
                    consecutiveBelow = 0;
                }
                else
                {
                    consecutiveBelow++;
                    if (consecutiveBelow >= RequiredBelowFrames)
                    {
                        endFrames.Add(lastActiveFrame >= 0 ? lastActiveFrame : frame);
                        inEvent = false;
                        consecutiveBelow = 0;
                    }
                }
            }
        }

        if (inEvent && lastActiveFrame >= 0)
        {
            endFrames.Add(lastActiveFrame);
        }

        if (startFrames.Count == 0 || endFrames.Count == 0)
        {
            return SegmentResult.Failure("No click event detected");
        }

        var eventCount = startFrames.Count;
        if (eventCount > maxEvents)
        {
            return SegmentResult.Failure("Multiple click events detected");
        }

        var startSample = frameStarts[startFrames[0]];
        var lastEndFrame = endFrames[^1];
        var endSample = frameStarts[lastEndFrame] + frameLengths[lastEndFrame];
        if (endSample <= startSample)
        {
            return SegmentResult.Failure("Click window invalid");
        }

        var trimmedStart = Math.Max(0, startSample - paddingSamples);
        var trimmedEnd = Math.Min(samples.Length, endSample + paddingSamples);
        if (trimmedEnd <= trimmedStart)
        {
            return SegmentResult.Failure("Click window invalid");
        }

        var lengthTrimmed = trimmedEnd - trimmedStart;
        var trimmed = new float[lengthTrimmed];
        Array.Copy(samples, trimmedStart, trimmed, 0, lengthTrimmed);
        return SegmentResult.Success(trimmed);
    }

    private static double[] NormalizeMfcc(double[]? mfcc)
    {
        var target = 13;
        var output = new double[target];
        if (mfcc is null)
        {
            return output;
        }

        for (var i = 0; i < target; i++)
        {
            output[i] = i < mfcc.Length ? mfcc[i] : 0;
        }

        return output;
    }

    private sealed record SegmentResult(bool IsSuccess, string? FailureReason, float[] TrimmedSamples)
    {
        public static SegmentResult Failure(string reason) => new(false, reason, Array.Empty<float>());
        public static SegmentResult Success(float[] trimmed) => new(true, null, trimmed);
    }
}

public sealed class ClickProcessingResult
{
    private ClickProcessingResult(bool success, string? failureReason, float[] trimmedSamples, ClickSampleMetrics? metrics, FeatureExtractionResult? extraction)
    {
        IsSuccess = success;
        FailureReason = failureReason;
        TrimmedSamples = trimmedSamples;
        Metrics = metrics;
        FeatureExtraction = extraction;
    }

    public bool IsSuccess { get; }
    public string? FailureReason { get; }
    public float[] TrimmedSamples { get; }
    public ClickSampleMetrics? Metrics { get; }
    public FeatureExtractionResult? FeatureExtraction { get; }

    public static ClickProcessingResult Failure(string reason) => new(false, reason, Array.Empty<float>(), null, null);
    public static ClickProcessingResult Success(float[] trimmedSamples, ClickSampleMetrics metrics, FeatureExtractionResult extraction) => new(true, null, trimmedSamples, metrics, extraction);
}
