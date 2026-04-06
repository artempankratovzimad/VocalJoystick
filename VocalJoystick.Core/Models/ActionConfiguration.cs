namespace VocalJoystick.Core.Models;

using System.Collections.Generic;
using System.Linq;
using VocalJoystick.Core.Models;

public sealed record ActionConfiguration
{
    public VocalAction Action { get; init; }
    public string? Alias { get; init; }
    public List<SampleMetadata> Samples { get; init; } = new();
    public ActionTemplate Template { get; set; } = ActionTemplate.Empty;
    public DirectionalTemplate? DirectionalTemplate { get; set; }
    public DirectionalSampleMetrics? DirectionalMetricsAverage { get; private set; }
    public bool HasSamples => Samples.Count > 0;
    public SampleMetadata? LatestSample => Samples.LastOrDefault();

    public void RefreshTemplate()
    {
        Template = ActionTemplate.Create(Samples);
        RefreshDirectionalMetrics();
    }

    private void RefreshDirectionalMetrics()
    {
        var metrics = Samples
            .Select(sample => sample.DirectionalMetrics)
            .Where(metric => metric is not null)
            .Cast<DirectionalSampleMetrics>()
            .ToArray();

        if (metrics.Length == 0)
        {
            DirectionalMetricsAverage = null;
            return;
        }

        DirectionalMetricsAverage = new DirectionalSampleMetrics(
            metrics.Average(metric => metric.MfccMean),
            metrics.Average(metric => metric.FormantFirstHz),
            metrics.Average(metric => metric.FormantSecondHz),
            metrics.Average(metric => metric.FormantDeltaHz),
            metrics.Average(metric => metric.SpectralCentroid));
    }
}
