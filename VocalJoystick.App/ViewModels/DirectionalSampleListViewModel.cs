using System;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using VocalJoystick.Core.Models;

namespace VocalJoystick.App.ViewModels;

public sealed class DirectionalSampleListViewModel : ViewModelBase
{
    private readonly DirectionalSampleMetrics? _averageMetrics;
    private readonly Func<SampleMetadata, Task<bool>> _deleteSampleCallback;
    private string _statusMessage = "Select a sample to inspect.";
    private readonly ObservableCollection<DirectionalSampleEntryViewModel> _samples;

    public DirectionalSampleListViewModel(
        VocalAction action,
        IReadOnlyList<SampleMetadata> samples,
        DirectionalTemplate? template,
        DirectionalSampleMetrics? averageMetrics,
        Func<SampleMetadata, Task<bool>> deleteSampleCallback)
    {
        Action = action;
        _averageMetrics = averageMetrics;
        _deleteSampleCallback = deleteSampleCallback ?? throw new ArgumentNullException(nameof(deleteSampleCallback));
        var sampleList = samples ?? throw new ArgumentNullException(nameof(samples));
        _samples = new ObservableCollection<DirectionalSampleEntryViewModel>(
            sampleList.Select(sample => new DirectionalSampleEntryViewModel(sample)));
        DeleteSampleCommand = new DelegateCommand<DirectionalSampleEntryViewModel>(entry => _ = DeleteSampleAsync(entry));
        CloseCommand = new DelegateCommand(() => RequestClose?.Invoke());
        Title = $"{action} samples";
        TemplateStatus = template is not null ? "Template ready" : "Record at least five samples to enable scoring";
        RecalculateMatchPercents();
    }

    public VocalAction Action { get; }
    public string Title { get; }
    public string TemplateStatus { get; }
    public string AverageStatus => _averageMetrics is not null ? "Average template metrics" : "Awaiting samples";
    public string AverageMfccDisplay => _averageMetrics is null
        ? "MFCC average pending"
        : $"MFCC avg {_averageMetrics.MfccMean:F3}";
    public string AverageFormantFirstDisplay => _averageMetrics is null
        ? "F1 pending"
        : $"F1 {_averageMetrics.FormantFirstHz:F1} Hz";
    public string AverageFormantSecondDisplay => _averageMetrics is null
        ? "F2 pending"
        : $"F2 {_averageMetrics.FormantSecondHz:F1} Hz";
    public string AverageFormantDeltaDisplay => _averageMetrics is null
        ? "Formant delta pending"
        : $"Δ {_averageMetrics.FormantDeltaHz:F2} Hz";
    public string AverageSpectralDisplay => _averageMetrics is null
        ? "Spectral centroid pending"
        : $"Spectral {_averageMetrics.SpectralCentroid:F1}";
    public ObservableCollection<DirectionalSampleEntryViewModel> Samples => _samples;
    public DelegateCommand<DirectionalSampleEntryViewModel> DeleteSampleCommand { get; }
    public DelegateCommand CloseCommand { get; }
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public event Action? RequestClose;

    private void RecalculateMatchPercents()
    {
        if (_averageMetrics is null)
        {
            foreach (var entry in _samples)
            {
                entry.ResetSimilarity();
            }

            return;
        }

        foreach (var entry in _samples)
        {
            entry.UpdateSimilarity(_averageMetrics);
        }
    }

    private async Task DeleteSampleAsync(DirectionalSampleEntryViewModel? entry)
    {
        if (entry is null)
        {
            return;
        }

        try
        {
            var success = await _deleteSampleCallback(entry.Metadata).ConfigureAwait(true);
            if (!success)
            {
                StatusMessage = "Unable to delete the selected sample.";
                return;
            }
        }
        catch (Exception)
        {
            StatusMessage = "Unable to delete the selected sample.";
            return;
        }

        _samples.Remove(entry);
        RecalculateMatchPercents();
        StatusMessage = "Sample removed.";

        if (_samples.Count == 0)
        {
            RequestClose?.Invoke();
        }
    }
}
