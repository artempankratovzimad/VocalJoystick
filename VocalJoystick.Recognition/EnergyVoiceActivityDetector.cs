using VocalJoystick.Core.Interfaces;
using VocalJoystick.Core.Models;

namespace VocalJoystick.Recognition;

public sealed class EnergyVoiceActivityDetector : IVoiceActivityDetector
{
    public VoiceActivityResult Analyze(Frame frame, FrameProcessingSettings settings)
    {
        var rms = AudioAnalysisHelpers.CalculateRms(frame.Samples);
        var isActive = rms >= settings.VadThreshold;
        return new VoiceActivityResult(isActive, rms);
    }
}
