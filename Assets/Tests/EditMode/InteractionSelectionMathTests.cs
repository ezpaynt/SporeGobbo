using System.Collections.Generic;
using NUnit.Framework;
using SporeGobbo.Input;

public class InteractionSelectionMathTests
{
    [Test]
    public void HigherPriorityBeatsNearerCandidate()
    {
        var candidates = Candidates(
            new InteractionCandidateData(1, 0, 0.5f, 1f),
            new InteractionCandidateData(2, 1, 1f, -1f));

        Assert.That(Select(candidates), Is.EqualTo(1));
    }

    [Test]
    public void EqualPriorityChoosesNearerOutsideFacingTie()
    {
        var candidates = Candidates(
            new InteractionCandidateData(1, 0, 0.5f, -1f),
            new InteractionCandidateData(2, 0, 0.8f, 1f));

        Assert.That(Select(candidates), Is.EqualTo(0));
    }

    [Test]
    public void FacingBreaksNearDistanceTie()
    {
        var candidates = Candidates(
            new InteractionCandidateData(1, 0, 0.6f, -0.5f),
            new InteractionCandidateData(2, 0, 0.7f, 0.9f));

        Assert.That(Select(candidates), Is.EqualTo(1));
    }

    [Test]
    public void FacingDoesNotOverrideMeaningfulDistanceDifference()
    {
        var candidates = Candidates(
            new InteractionCandidateData(1, 0, 0.5f, -1f),
            new InteractionCandidateData(2, 0, 0.71f, 1f));

        Assert.That(Select(candidates), Is.EqualTo(0));
    }

    [Test]
    public void CurrentCandidateSurvivesTinyDistanceChanges()
    {
        var candidates = Candidates(
            new InteractionCandidateData(1, 0, 0.65f, 0f),
            new InteractionCandidateData(2, 0, 0.6f, 1f));

        Assert.That(Select(candidates, 1), Is.EqualTo(0));
    }

    [Test]
    public void MeaningfullyBetterCandidateBreaksRetention()
    {
        var candidates = Candidates(
            new InteractionCandidateData(1, 0, 0.8f, 1f),
            new InteractionCandidateData(2, 0, 0.55f, 0f));

        Assert.That(Select(candidates, 1), Is.EqualTo(1));
    }

    [Test]
    public void InvalidCandidatesAreExcluded()
    {
        var candidates = Candidates(
            new InteractionCandidateData(1, 10, 0.1f, 1f, false),
            new InteractionCandidateData(2, 0, 0.8f, 0f));

        Assert.That(Select(candidates), Is.EqualTo(1));
    }

    [Test]
    public void EmptyCandidatesReturnNone()
    {
        Assert.That(Select(new List<InteractionCandidateData>()), Is.EqualTo(-1));
    }

    private static List<InteractionCandidateData> Candidates(params InteractionCandidateData[] values)
    {
        return new List<InteractionCandidateData>(values);
    }

    private static int Select(IReadOnlyList<InteractionCandidateData> candidates, int currentId = int.MinValue)
    {
        return InteractionSelectionMath.Select(candidates, currentId, 0.2f, 0.1f);
    }
}
