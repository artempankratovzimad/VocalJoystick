using VocalJoystick.Core.Models;

namespace VocalJoystick.Core.Interfaces;

public interface IVoiceActivityDetector
{
    VoiceActivityResult Analyze(Frame frame, FrameProcessingSettings settings);
}
