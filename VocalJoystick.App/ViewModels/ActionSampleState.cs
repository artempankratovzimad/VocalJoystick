using System.Collections.Generic;
using VocalJoystick.Core.Models;

namespace VocalJoystick.App.ViewModels;

public sealed class ActionSampleState : ViewModelBase
{
    private string _statusText = "No sample";
    private string _durationText = "Duration: 0s";
    private bool _isRecording;
    private int _sampleCount;
    private string _featureSummary = "Template pending";

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

    public string FeatureSummary
    {
        get => _featureSummary;
        private set => SetProperty(ref _featureSummary, value);
    }

    public void UpdateMetadata(IReadOnlyList<SampleMetadata> samples, ActionTemplate template)
    {
        SampleCount = samples.Count;
        if (samples.Count == 0)
        {
            StatusText = "No sample";
            DurationText = "Duration: 0s";
            FeatureSummary = "Template pending";
            return;
        }

        var last = samples[^1];
        StatusText = $"Last: {last.RecordedAt:HH:mm:ss}";
        DurationText = $"Duration: {last.DurationSeconds:F2}s";
        FeatureSummary = template.SampleCount == 0
            ? "Awaiting feature summary"
            : $"Avg RMS {template.AverageRms:F2}, Pitch {template.AveragePitchHz:F0}Hz, Voiced {template.VoicedRatio:P0}";
    }
}
