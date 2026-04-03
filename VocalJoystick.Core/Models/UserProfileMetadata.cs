using System;
using System.Collections.Generic;

namespace VocalJoystick.Core.Models;

public sealed class UserProfileMetadata
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string DisplayName { get; init; } = "Default";
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public AppMode LastMode { get; set; } = AppMode.Stopped;
    public Dictionary<VocalAction, string>? ActionAliases { get; init; }
}
