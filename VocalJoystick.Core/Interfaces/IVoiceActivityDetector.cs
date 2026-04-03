using VocalJoystick.Core.Models;

namespace VocalJoystick.Core.Interfaces;

public interface IVoiceActivityDetector
{
    bool IsVoiceActivity(AudioBuffer buffer);
}
