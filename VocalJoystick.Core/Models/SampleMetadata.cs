using System;

namespace VocalJoystick.Core.Models;

public sealed record SampleMetadata(string FileName, DateTimeOffset RecordedAt, double PitchHz);
