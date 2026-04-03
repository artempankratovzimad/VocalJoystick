using System;

namespace VocalJoystick.Core.Models;

public sealed record ShortAudioEvent(float[] Samples, int SampleRate, DateTimeOffset Start)
{
    public TimeSpan Duration => TimeSpan.FromSeconds(Samples.Length / (double)Math.Max(1, SampleRate));
}
