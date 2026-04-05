using VocalJoystick.Core.Models;

namespace VocalJoystick.Core.Interfaces;

public interface IDirectionalTrainingService
{
    void AddSample(VocalAction action, DirectionalFeatureVector feature);
    bool TryBuildTemplate(VocalAction action, out DirectionalTemplate template);
    IReadOnlyDictionary<VocalAction, DirectionalTemplate> GetTemplates();
    IReadOnlyDictionary<VocalAction, int> GetSampleCounts();
    int MinimumSamples { get; }
    int MaximumSamples { get; }
    bool ValidateSample(DirectionalFeatureVector feature, out string? failureReason);
    void Reset();
}
