namespace VocalJoystick.Core.Models;

public sealed record ActionConfiguration
{
    public VocalAction Action { get; init; }
    public string? Alias { get; init; }
    public SampleMetadata? Sample { get; init; }
    public bool IsConfigured => Sample is not null;
}
