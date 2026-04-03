namespace VocalJoystick.Core.Models;

using System.Collections.Generic;
using System.Linq;

public sealed record ActionConfiguration
{
    public VocalAction Action { get; init; }
    public string? Alias { get; init; }
    public List<SampleMetadata> Samples { get; init; } = new();
    public bool HasSamples => Samples.Count > 0;
    public SampleMetadata? LatestSample => Samples.LastOrDefault();
}
