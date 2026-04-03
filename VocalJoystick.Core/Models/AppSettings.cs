using System;

namespace VocalJoystick.Core.Models;

public sealed record AppSettings(AppMode LastMode, string? ActiveProfileId, string? SelectedMicrophoneId, FrameProcessingSettings FrameSettings)
{
    public DateTimeOffset LastUpdated { get; init; } = DateTimeOffset.UtcNow;
    public double ClickConfidenceThreshold { get; init; } = 0.7;
    public int ClickCooldownMs { get; init; } = 400;
    public double MovementSpeed { get; init; } = 320;

    public static AppSettings CreateDefault() => new(AppMode.Idle, null, null, FrameProcessingSettings.CreateDefault());

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

    public AppSettings WithFrameSettings(FrameProcessingSettings settings) => this with
    {
        FrameSettings = settings,
        LastUpdated = DateTimeOffset.UtcNow
    };

    public AppSettings WithClickConfidenceThreshold(double threshold) => this with
    {
        ClickConfidenceThreshold = Math.Clamp(threshold, 0, 1),
        LastUpdated = DateTimeOffset.UtcNow
    };

    public AppSettings WithClickCooldownMs(int cooldownMs) => this with
    {
        ClickCooldownMs = Math.Max(0, cooldownMs),
        LastUpdated = DateTimeOffset.UtcNow
    };

    public AppSettings WithMovementSpeed(double speed) => this with
    {
        MovementSpeed = Math.Max(0, speed),
        LastUpdated = DateTimeOffset.UtcNow
    };
}
