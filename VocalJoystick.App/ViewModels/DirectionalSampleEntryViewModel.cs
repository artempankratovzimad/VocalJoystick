using System;
using VocalJoystick.Core.Models;

namespace VocalJoystick.App.ViewModels;

public sealed class DirectionalSampleEntryViewModel : ViewModelBase
{
    private double? _similarityPercent;
    private readonly DirectionalSampleMetrics? _sampleMetrics;

public DirectionalSampleEntryViewModel(SampleMetadata metadata)
    {
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        _sampleMetrics = metadata.DirectionalMetrics ?? DirectionalSampleMetrics.FromFeatureVector(metadata.FeatureSummary.DirectionalFeature);
    }

    public SampleMetadata Metadata { get; }

    public string RecordedAtDisplay => Metadata.RecordedAt.ToLocalTime().ToString("g");
    public string DurationDisplay => $"Duration {Metadata.DurationSeconds:F2}s";
    public string MfccDisplay => _sampleMetrics is null
        ? "MFCC pending"
        : $"MFCC avg {_sampleMetrics.MfccMean:F3}";
    public string FormantFirstDisplay => _sampleMetrics is null
        ? "F1 pending"
        : $"F1 {_sampleMetrics.FormantFirstHz:F1} Hz";
    public string FormantSecondDisplay => _sampleMetrics is null
        ? "F2 pending"
        : $"F2 {_sampleMetrics.FormantSecondHz:F1} Hz";
    public string FormantDeltaDisplay => _sampleMetrics is null
        ? "Formant delta pending"
        : $"Δ {_sampleMetrics.FormantDeltaHz:F2} Hz";
    public string SpectralDisplay => _sampleMetrics is null
        ? "Spectral centroid pending"
        : $"Spectral {_sampleMetrics.SpectralCentroid:F1}";

    public string SimilarityDisplay => _similarityPercent.HasValue
        ? $"{_similarityPercent.Value:F1}% similar"
        : "Similarity pending";

    internal void UpdateSimilarity(DirectionalSampleMetrics averageMetrics)
    {
        var similarity = DirectionalSampleMetrics.CalculateSimilarity(_sampleMetrics, averageMetrics);
        if (!similarity.HasValue)
        {
            _similarityPercent = null;
            OnPropertyChanged(nameof(SimilarityDisplay));
            return;
        }

        var normalized = Math.Clamp(similarity.Value, 0, 1);
        _similarityPercent = Math.Round(normalized * 100, 1);
        OnPropertyChanged(nameof(SimilarityDisplay));
    }

    internal void ResetSimilarity()
    {
        _similarityPercent = null;
        OnPropertyChanged(nameof(SimilarityDisplay));
    }
}
