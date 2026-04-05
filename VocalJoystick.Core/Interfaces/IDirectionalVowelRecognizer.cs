using VocalJoystick.Core.Models;

namespace VocalJoystick.Core.Interfaces;

public interface IDirectionalVowelRecognizer
{
    DirectionalRecognitionResult Recognize(VoiceActivityResult voiceActivity, PitchDetectionResult pitch, DirectionalFeatureVector feature, DateTimeOffset timestamp);
    void UpdateTemplates(IReadOnlyDictionary<VocalAction, DirectionalTemplate> templates);
    void Reset();
}
