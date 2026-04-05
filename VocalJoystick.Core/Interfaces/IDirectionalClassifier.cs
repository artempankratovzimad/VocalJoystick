using VocalJoystick.Core.Models;

namespace VocalJoystick.Core.Interfaces;

public interface IDirectionalClassifier
{
    DirectionalClassificationResult Classify(DirectionalFeatureVector feature, IReadOnlyDictionary<VocalAction, DirectionalTemplate> templates);
}
