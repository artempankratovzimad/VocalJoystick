namespace VocalJoystick.Core.Models;

public sealed record FeatureExtractionResult(float[] FeatureVector, SampleFeatureSummary Summary, DirectionalFeatureVector? DirectionalFeature = null);
