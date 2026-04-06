using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VocalJoystick.Core.Interfaces;
using VocalJoystick.Core.Models;
using VocalJoystick.Core.Services;

namespace VocalJoystick.Recognition;

public sealed class ShortClickRecognitionEngine : IShortClickRecognitionEngine
{
    private readonly ShortEventSegmenter _segmenter;
    private readonly ClickMetricsExtractor _metricsExtractor;
    private readonly ClickClassifier _classifier;
    private readonly ILogger _logger;
    private DateTimeOffset _lastRecognition = DateTimeOffset.MinValue;

    public ShortClickRecognitionEngine(IFeatureExtractor featureExtractor, ClickClassifier classifier, ILogger logger, ShortEventSegmenter? segmenter = null)
    {
        _segmenter = segmenter ?? new ShortEventSegmenter();
        _metricsExtractor = new ClickMetricsExtractor(featureExtractor);
        _classifier = classifier;
        _logger = logger;
    }

    public async Task<RecognitionResult?> ProcessBufferAsync(AudioBuffer buffer, IReadOnlyDictionary<VocalAction, ClickPrototype> prototypes, double minSimilarity, double margin, TimeSpan cooldown, bool logDebug, CancellationToken cancellationToken)
    {
        if (prototypes.Count == 0)
        {
            return null;
        }

        var events = _segmenter.Segment(buffer);
        foreach (var audioEvent in events)
        {
            if ((audioEvent.Start - _lastRecognition) < cooldown)
            {
                continue;
            }

            if (audioEvent.Samples.Length == 0)
            {
                continue;
            }

            var extracted = await _metricsExtractor.ExtractAsync(audioEvent.Samples, audioEvent.SampleRate, cancellationToken).ConfigureAwait(false);
            var outcome = _classifier.Classify(extracted.Metrics, prototypes, minSimilarity, margin);
            LogClickDebug(audioEvent, extracted.Metrics.DurationMs, outcome, logDebug);

            if (!outcome.IsAccepted)
            {
                continue;
            }

            _lastRecognition = audioEvent.Start;
            return new RecognitionResult
            {
                Action = outcome.Action!.Value,
                Confidence = outcome.BestSimilarity,
                Description = "Click recognition"
            };
        }

        return null;
    }

    public void Reset()
    {
        _segmenter.Reset();
        _lastRecognition = DateTimeOffset.MinValue;
    }

    private void LogClickDebug(ShortAudioEvent audioEvent, double durationMs, ClickClassificationOutcome outcome, bool logDebug)
    {
        if (!logDebug)
        {
            return;
        }

        var baseMessage = $"[ClickDebug] {audioEvent.Start:HH:mm:ss.fff} duration={durationMs:F1}ms best={outcome.BestSimilarity:P2} second={outcome.SecondBestSimilarity:P2} margin={outcome.Margin:P2}";
        var reason = outcome.Reason ?? "accepted";
        if (outcome.IsAccepted)
        {
            _logger.LogDebug($"{baseMessage} action={outcome.Action} reason={reason}");
        }
        else
        {
            _logger.LogDebug($"{baseMessage} rejected reason={reason}");
        }
    }
}
