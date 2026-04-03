using System;

namespace VocalJoystick.Core.Models;

public sealed record SampleMetadata(string FileName, string RelativePath, DateTimeOffset RecordedAt, double DurationSeconds, double? PitchHz = null);
