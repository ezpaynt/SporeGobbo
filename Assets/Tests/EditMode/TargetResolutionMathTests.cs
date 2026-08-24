using System.Collections.Generic;
using NUnit.Framework;
using SporeGobbo.Input;
using UnityEngine;

public class TargetResolutionMathTests
{
    [Test]
    public void DirectlyAheadQualifies()
    {
        Assert.That(TryScore(Vector2.right * 3f, out _), Is.True);
    }

    [Test]
    public void TargetAtConeEdgeQualifies()
    {
        Vector2 edge = DirectionAtDegrees(35f) * 3f;
        Assert.That(TryScore(edge, out _), Is.True);
    }

    [Test]
    public void TargetOutsideConeIsRejected()
    {
        Vector2 outside = DirectionAtDegrees(35.1f) * 3f;
        Assert.That(TryScore(outside, out _), Is.False);
    }

    [Test]
    public void TargetBehindIsRejected()
    {
        Assert.That(TryScore(Vector2.left * 2f, out _), Is.False);
    }

    [Test]
    public void BetterAlignmentBeatsSlightlyCloserOffAxisTarget()
    {
        var candidates = new List<TargetCandidateData>
        {
            new(Vector2.right * 3f),
            new(DirectionAtDegrees(28f) * 2.5f)
        };

        Assert.That(Select(candidates), Is.EqualTo(0));
    }

    [Test]
    public void DistanceBreaksEqualAlignmentTie()
    {
        var candidates = new List<TargetCandidateData>
        {
            new(Vector2.right * 3f),
            new(Vector2.right * 2f)
        };

        Assert.That(Select(candidates), Is.EqualTo(1));
    }

    [Test]
    public void ConfiguredWeightsPredictablyChangeSelection()
    {
        var candidates = new List<TargetCandidateData>
        {
            new(Vector2.right * 3.8f),
            new(DirectionAtDegrees(25f) * 1f)
        };

        int alignmentWeighted = Select(candidates, 0.8f, 0.2f);
        int distanceWeighted = Select(candidates, 0.1f, 0.9f);

        Assert.That(alignmentWeighted, Is.EqualTo(0));
        Assert.That(distanceWeighted, Is.EqualTo(1));
    }

    [Test]
    public void TargetBeyondMaxRangeIsRejected()
    {
        Assert.That(TryScore(Vector2.right * 4.01f, out _), Is.False);
    }

    [Test]
    public void EmptyOrFilteredCandidatesReturnNoTarget()
    {
        Assert.That(Select(new List<TargetCandidateData>()), Is.EqualTo(-1));
        Assert.That(Select(new List<TargetCandidateData>
        {
            new(Vector2.right, false)
        }), Is.EqualTo(-1));
    }

    [Test]
    public void RetainedAimDirectionCanBeUsedWithoutCurrentStickInput()
    {
        Vector2 retainedAim = Vector2.up;
        bool qualifies = TargetResolutionMath.TryScoreDirectionalCandidate(
            Vector2.zero, retainedAim, Vector2.up * 2f, 4f, 70f, 0.8f, 0.2f,
            out _, out _);

        Assert.That(qualifies, Is.True);
    }

    private static bool TryScore(Vector2 target, out float score)
    {
        return TargetResolutionMath.TryScoreDirectionalCandidate(
            Vector2.zero, Vector2.right, target, 4f, 70f, 0.8f, 0.2f,
            out score, out _);
    }

    private static int Select(IReadOnlyList<TargetCandidateData> candidates, float alignmentWeight = 0.8f, float distanceWeight = 0.2f)
    {
        return TargetResolutionMath.SelectBestDirectionalCandidate(
            candidates, Vector2.zero, Vector2.right, 4f, 70f,
            alignmentWeight, distanceWeight);
    }

    private static Vector2 DirectionAtDegrees(float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
    }
}
