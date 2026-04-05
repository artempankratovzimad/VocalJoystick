namespace VocalJoystick.Core.Models;

public sealed record DirectionalFeatureVector(
    double[] MfccCoefficients,
    FormantResult Formants,
    double Rms,
    double SpectralCentroid,
    double SpectralSpread,
    bool IsVoiced,
    double PitchHz,
    double PitchConfidence,
    double Power)
{
    public DirectionalFeatureVector WithMfcc(double[] mfcc) => this with { MfccCoefficients = mfcc }; // copy semantics

    public double DistanceTo(DirectionalFeatureVector other)
    {
        if (other is null)
        {
            return double.MaxValue;
        }

        var mfccDistance = EuclideanDistance(MfccCoefficients, other.MfccCoefficients);
        var formantDistance = Math.Abs(Formants.FirstFormantHz - other.Formants.FirstFormantHz)
            + Math.Abs(Formants.SecondFormantHz - other.Formants.SecondFormantHz);
        var energyDistance = Math.Abs(Rms - other.Rms);
        var centroidDistance = Math.Abs(SpectralCentroid - other.SpectralCentroid);
        return mfccDistance + formantDistance * 0.5 + energyDistance * 5 + centroidDistance;
    }

    private static double EuclideanDistance(double[] a, double[] b)
    {
        if (a is null || b is null || a.Length == 0 || b.Length == 0)
        {
            return double.MaxValue;
        }

        var len = Math.Min(a.Length, b.Length);
        var sum = 0d;
        for (var i = 0; i < len; i++)
        {
            var diff = a[i] - b[i];
            sum += diff * diff;
        }

        return Math.Sqrt(sum);
    }
}
