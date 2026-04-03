namespace VocalJoystick.Core.Models;

public sealed record SettingsSnapshot(AppMode? LastMode, string? ActiveProfileId);
