using System;

namespace VocalJoystick.Core.Models;

public sealed record AudioBuffer(float[] Samples, int SampleRate)
{
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class AudioBufferEventArgs : EventArgs
{
    public AudioBufferEventArgs(AudioBuffer buffer) => Buffer = buffer;
    public AudioBuffer Buffer { get; }
}
