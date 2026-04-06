using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VocalJoystick.Core.Models;

namespace VocalJoystick.App.ViewModels;

public sealed class SampleListRequestEventArgs : EventArgs
{
    public SampleListRequestEventArgs(
        VocalAction action,
        IReadOnlyList<SampleMetadata> samples,
        DirectionalTemplate? template,
        DirectionalSampleMetrics? averageMetrics,
        Func<SampleMetadata, Task<bool>> deleteSample)
    {
        Action = action;
        Samples = samples ?? throw new ArgumentNullException(nameof(samples));
        Template = template;
        AverageMetrics = averageMetrics;
        DeleteSampleCallback = deleteSample ?? throw new ArgumentNullException(nameof(deleteSample));
    }

    public VocalAction Action { get; }
    public IReadOnlyList<SampleMetadata> Samples { get; }
    public DirectionalTemplate? Template { get; }
    public DirectionalSampleMetrics? AverageMetrics { get; }
    public Func<SampleMetadata, Task<bool>> DeleteSampleCallback { get; }
}
