using System;

namespace VocalJoystick.Core.Models;

public sealed record AppSettings(AppMode LastMode, string? ActiveProfileId, string? SelectedMicrophoneId)
{
    public DateTimeOffset LastUpdated { get; init; } = DateTimeOffset.UtcNow;

    public static AppSettings CreateDefault() => new(AppMode.Stopped, null, null);

    public AppSettings WithMode(AppMode mode, string? profileId) => this with
    {
        LastMode = mode,
        ActiveProfileId = profileId,
        LastUpdated = DateTimeOffset.UtcNow
    };

    public AppSettings WithDevice(string? deviceId) => this with
    {
        SelectedMicrophoneId = deviceId,
        LastUpdated = DateTimeOffset.UtcNow
    };
}
