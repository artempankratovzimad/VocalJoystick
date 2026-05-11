using System;

namespace VocalJoystick.Core.Models;

public sealed record AppSettings(AppMode LastMode, string? ActiveProfileId, string? SelectedMicrophoneId, FrameProcessingSettings FrameSettings)
{
    public DateTimeOffset LastUpdated { get; init; } = DateTimeOffset.UtcNow;
    public double ClickConfidenceThreshold { get; init; } = 0.7;
    public double ClickMarginThreshold { get; init; } = 0.1;
    public int ClickCooldownMs { get; init; } = 400;
    public double MovementStartSpeed { get; init; } = 320;
    public double MovementEndSpeed { get; init; } = 320;
    public double MovementAccelerationSeconds { get; init; } = 1;
    public bool RequireClickSilence { get; init; } = true;

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

    public AppSettings WithClickMarginThreshold(double margin) => this with
    {
        ClickMarginThreshold = Math.Clamp(margin, 0, 1),
        LastUpdated = DateTimeOffset.UtcNow
    };

    public AppSettings WithClickCooldownMs(int cooldownMs) => this with
    {
        ClickCooldownMs = Math.Max(0, cooldownMs),
        LastUpdated = DateTimeOffset.UtcNow
    };

    public AppSettings WithMovementStartSpeed(double startSpeed) => this with
    {
        MovementStartSpeed = Math.Max(0, startSpeed),
        LastUpdated = DateTimeOffset.UtcNow
    };

    public AppSettings WithMovementEndSpeed(double endSpeed) => this with
    {
        MovementEndSpeed = Math.Max(0, endSpeed),
        LastUpdated = DateTimeOffset.UtcNow
    };

    public AppSettings WithMovementAccelerationSeconds(double seconds) => this with
    {
        MovementAccelerationSeconds = Math.Max(0, seconds),
        LastUpdated = DateTimeOffset.UtcNow
    };

    public AppSettings WithRequireClickSilence(bool enabled) => this with
    {
        RequireClickSilence = enabled,
        LastUpdated = DateTimeOffset.UtcNow
    };
}
