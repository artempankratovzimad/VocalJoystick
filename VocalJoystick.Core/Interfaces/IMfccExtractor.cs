namespace VocalJoystick.Core.Interfaces;

public interface IMfccExtractor
{
    double[] ExtractMfcc(float[] samples, int sampleRate, int coefficientCount = 13);
}
