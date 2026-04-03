using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VocalJoystick.Core.Interfaces;
using VocalJoystick.Core.Models;

namespace VocalJoystick.Recognition;

public sealed class ShortClickRecognitionEngine : IShortClickRecognitionEngine
{
    private readonly ShortEventSegmenter _segmenter;
    private readonly ShortClickTemplateClassifier _classifier = new();
    private readonly IFeatureExtractor _featureExtractor;
    private DateTimeOffset _lastRecognition = DateTimeOffset.MinValue;

    public ShortClickRecognitionEngine(IFeatureExtractor featureExtractor, ShortEventSegmenter? segmenter = null)
    {
        _featureExtractor = featureExtractor;
        _segmenter = segmenter ?? new ShortEventSegmenter();
    }

    public async Task<RecognitionResult?> ProcessBufferAsync(AudioBuffer buffer, IReadOnlyDictionary<VocalAction, ActionTemplate> templates, double confidenceThreshold, TimeSpan cooldown, CancellationToken cancellationToken)
    {
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

            var extraction = await _featureExtractor.ExtractFeaturesAsync(new AudioBuffer(audioEvent.Samples, audioEvent.SampleRate), cancellationToken).ConfigureAwait(false);
            var result = _classifier.Classify(extraction.Summary, templates, confidenceThreshold);
            if (result is not null)
            {
                _lastRecognition = audioEvent.Start;
                return result;
            }
        }

        return null;
    }

    public void Reset()
    {
        _segmenter.Reset();
        _lastRecognition = DateTimeOffset.MinValue;
    }
}
