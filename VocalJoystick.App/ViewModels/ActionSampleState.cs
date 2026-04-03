using System.Collections.Generic;
using VocalJoystick.Core.Models;

namespace VocalJoystick.App.ViewModels;

public sealed class ActionSampleState : ViewModelBase
{
    private string _statusText = "No sample";
    private string _durationText = "Duration: 0s";
    private bool _isRecording;
    private int _sampleCount;

    public ActionSampleState(VocalAction action)
    {
        Action = action;
    }

    public VocalAction Action { get; }

    public bool IsRecording
    {
        get => _isRecording;
        set => SetProperty(ref _isRecording, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public string DurationText
    {
        get => _durationText;
        set => SetProperty(ref _durationText, value);
    }

    public bool HasSamples => SampleCount > 0;

    public int SampleCount
    {
        get => _sampleCount;
        private set => SetProperty(ref _sampleCount, value);
    }

    public void UpdateMetadata(IReadOnlyList<SampleMetadata> samples)
    {
        SampleCount = samples.Count;
        if (samples.Count == 0)
        {
            StatusText = "No sample";
            DurationText = "Duration: 0s";
            return;
        }

        var last = samples[^1];
        StatusText = $"Last: {last.RecordedAt:HH:mm:ss}";
        DurationText = $"Duration: {last.DurationSeconds:F2}s";
    }
}
