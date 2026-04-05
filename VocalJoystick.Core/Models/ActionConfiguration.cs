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
    public bool HasSamples => Samples.Count > 0;
    public SampleMetadata? LatestSample => Samples.LastOrDefault();

    public void RefreshTemplate()
    {
        Template = ActionTemplate.Create(Samples);
    }
}
