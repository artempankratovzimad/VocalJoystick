using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VocalJoystick.Core.Models;

namespace VocalJoystick.Recognition;

public interface IShortClickRecognitionEngine
{
    Task<RecognitionResult?> ProcessBufferAsync(AudioBuffer buffer, IReadOnlyDictionary<VocalAction, ActionTemplate> templates, double confidenceThreshold, TimeSpan cooldown, CancellationToken cancellationToken);

    void Reset();
}
