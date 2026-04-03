using System;
using System.Collections.Generic;
using VocalJoystick.Core.Models;

namespace VocalJoystick.Recognition;

public sealed class ShortEventSegmenter
{
    private readonly float _startThreshold;
    private readonly int _silenceSampleLimit;
    private readonly int _maxEventSamples;
    private readonly List<float> _eventBuffer = new();
    private bool _isInEvent;
    private int _silenceSamples;
    private DateTimeOffset _eventStartTime;
    private int _eventSampleRate;

    public ShortEventSegmenter(float startThreshold = 0.12f, int silenceSampleLimit = 800, int maxEventSamples = 8_000)
    {
        _startThreshold = startThreshold;
        _silenceSampleLimit = silenceSampleLimit;
        _maxEventSamples = maxEventSamples;
    }

    public IReadOnlyList<ShortAudioEvent> Segment(AudioBuffer buffer)
    {
        var events = new List<ShortAudioEvent>();
        var sampleRate = Math.Max(1, buffer.SampleRate);

        for (var index = 0; index < buffer.Samples.Length; index++)
        {
            var sample = buffer.Samples[index];
            var isLoud = Math.Abs(sample) >= _startThreshold;

            if (!_isInEvent)
            {
                if (!isLoud)
                {
                    continue;
                }

                _isInEvent = true;
                _eventBuffer.Clear();
                _eventSampleRate = sampleRate;
                _eventStartTime = buffer.Timestamp.AddSeconds(index / (double)sampleRate);
                _eventBuffer.Add(sample);
                _silenceSamples = 0;
                continue;
            }

            _eventBuffer.Add(sample);
            if (isLoud)
            {
                _silenceSamples = 0;
            }
            else
            {
                _silenceSamples++;
            }

            if (_silenceSamples >= _silenceSampleLimit || _eventBuffer.Count >= _maxEventSamples)
            {
                events.Add(CreateEvent());
                _isInEvent = false;
                _eventBuffer.Clear();
                _silenceSamples = 0;
            }
        }

        return events;
    }

    public void Reset()
    {
        _eventBuffer.Clear();
        _isInEvent = false;
        _silenceSamples = 0;
    }

    private ShortAudioEvent CreateEvent()
    {
        var samples = _eventBuffer.ToArray();
        return new ShortAudioEvent(samples, _eventSampleRate, _eventStartTime);
    }
}
