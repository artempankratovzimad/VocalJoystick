using System;
using System.Collections.Generic;
using VocalJoystick.Core.Models;

namespace VocalJoystick.Recognition;

public sealed class ClickClassifier
{
    private readonly ClickSimilarityCalculator _similarityCalculator;

    public ClickClassifier(ClickSimilarityCalculator similarityCalculator)
    {
        _similarityCalculator = similarityCalculator;
    }

    public ClickClassificationOutcome Classify(ClickSampleMetrics sample, IReadOnlyDictionary<VocalAction, ClickPrototype> prototypes, double minSimilarity, double minMargin)
    {
        if (prototypes.Count == 0)
        {
            return ClickClassificationOutcome.Rejected("No prototypes available");
        }

        VocalAction? bestAction = null;
        ClickSimilarityResult? bestSimilarityResult = null;
        var bestValue = 0d;
        var secondBest = 0d;

        foreach (var kvp in prototypes)
        {
            var similarity = _similarityCalculator.Calculate(sample, kvp.Value);
            var score = similarity.OverallSimilarity;
            if (score > bestValue)
            {
                secondBest = bestValue;
                bestValue = score;
                bestAction = kvp.Key;
                bestSimilarityResult = similarity;
            }
            else if (score > secondBest)
            {
                secondBest = score;
            }
        }

        if (bestAction is null || bestSimilarityResult is null)
        {
            return ClickClassificationOutcome.Rejected("No candidate matches");
        }

        var margin = bestValue - secondBest;
        if (bestValue < minSimilarity)
        {
            return ClickClassificationOutcome.Rejected("Similarity below threshold", bestAction, bestValue, secondBest, margin, bestSimilarityResult);
        }

        if (margin < minMargin)
        {
            return ClickClassificationOutcome.Rejected("Margin too small", bestAction, bestValue, secondBest, margin, bestSimilarityResult);
        }

        return ClickClassificationOutcome.Accepted(bestAction.Value, bestValue, secondBest, margin, bestSimilarityResult);
    }
}

public sealed record ClickClassificationOutcome
{
    private ClickClassificationOutcome(bool accepted, string? reason, VocalAction? action, double best, double second, double margin, ClickSimilarityResult? similarity)
    {
        IsAccepted = accepted;
        Reason = reason;
        Action = action;
        BestSimilarity = best;
        SecondBestSimilarity = second;
        Margin = margin;
        SimilarityResult = similarity;
    }

    public bool IsAccepted { get; }
    public string? Reason { get; }
    public VocalAction? Action { get; }
    public double BestSimilarity { get; }
    public double SecondBestSimilarity { get; }
    public double Margin { get; }
    public ClickSimilarityResult? SimilarityResult { get; }

    public static ClickClassificationOutcome Rejected(string reason, VocalAction? action = null, double best = 0, double second = 0, double margin = 0, ClickSimilarityResult? similarity = null)
        => new(false, reason, action, best, second, margin, similarity);

    public static ClickClassificationOutcome Accepted(VocalAction action, double best, double second, double margin, ClickSimilarityResult similarity)
        => new(true, null, action, best, second, margin, similarity);
}
