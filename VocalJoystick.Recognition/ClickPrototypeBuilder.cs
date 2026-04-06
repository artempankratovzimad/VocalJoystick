using System;
using System.Collections.Generic;
using System.Linq;
using VocalJoystick.Core.Models;

namespace VocalJoystick.Recognition;

public sealed class ClickPrototypeBuilder
{
    private static readonly VocalAction[] ClickActions =
    {
        VocalAction.LeftClick,
        VocalAction.RightClick,
        VocalAction.DoubleClick
    };

    public IReadOnlyDictionary<VocalAction, ClickPrototype> Build(IEnumerable<ActionConfiguration> configurations)
    {
        var prototypes = new Dictionary<VocalAction, ClickPrototype>();
        foreach (var action in ClickActions)
        {
            var config = configurations.FirstOrDefault(c => c.Action == action);
            if (config is null)
            {
                continue;
            }

            var samples = config.Samples
                .Select(sample => sample.ClickMetrics)
                .Where(metrics => metrics is not null)
                .Select(metrics => metrics!)
                .ToArray();

            if (samples.Length == 0)
            {
                continue;
            }

            prototypes[action] = CreatePrototype(action, samples);
        }

        return prototypes;
    }

    private static ClickPrototype CreatePrototype(VocalAction action, ClickSampleMetrics[] samples)
    {
        var durationStats = ClickFeatureStats.Create(samples.Select(s => s.DurationMs).ToArray(), samples.Average(s => s.DurationMs) * 0.1 + 1);
        var attackStats = ClickFeatureStats.Create(samples.Select(s => s.AttackTimeMs).ToArray(), 1);
        var centroidStats = ClickFeatureStats.Create(samples.Select(s => s.SpectralCentroid).ToArray(), 10);
        var rolloffStats = ClickFeatureStats.Create(samples.Select(s => s.SpectralRolloff).ToArray(), 10);
        var flatnessStats = ClickFeatureStats.Create(samples.Select(s => s.SpectralFlatness).ToArray(), 0.01);
        var zeroCrossingStats = ClickFeatureStats.Create(samples.Select(s => s.ZeroCrossingRate).ToArray(), 0.01);
        var rmsStats = ClickFeatureStats.Create(samples.Select(s => s.RmsEnergy).ToArray(), 0.01);

        var mfccMean = new double[13];
        for (var i = 0; i < mfccMean.Length; i++)
        {
            mfccMean[i] = samples.Average(s => s.MfccCoefficients.Length > i ? s.MfccCoefficients[i] : 0);
        }

        return new ClickPrototype(
            action,
            durationStats,
            attackStats,
            centroidStats,
            rolloffStats,
            flatnessStats,
            zeroCrossingStats,
            rmsStats,
            mfccMean);
    }
}
