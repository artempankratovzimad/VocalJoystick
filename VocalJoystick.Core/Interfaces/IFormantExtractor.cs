using VocalJoystick.Core.Models;

namespace VocalJoystick.Core.Interfaces;

public interface IFormantExtractor
{
    FormantResult ExtractFormants(float[] samples, int sampleRate);
}
