using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VocalJoystick.Core.Models;

namespace VocalJoystick.Recognition;

public interface IShortClickRecognitionEngine
{
    Task<RecognitionResult?> ProcessBufferAsync(AudioBuffer buffer, IReadOnlyDictionary<VocalAction, ClickPrototype> prototypes, double minSimilarity, double margin, TimeSpan cooldown, bool logDebug, CancellationToken cancellationToken);

    void Reset();
}
