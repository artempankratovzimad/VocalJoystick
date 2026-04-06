namespace VocalJoystick.Core.Models;

public sealed record SampleMetadata(
    string FileName,
    string RelativePath,
    DateTimeOffset RecordedAt,
    double DurationSeconds,
    SampleFeatureSummary FeatureSummary,
    DirectionalSampleMetrics? DirectionalMetrics = null,
    ClickSampleMetrics? ClickMetrics = null);
