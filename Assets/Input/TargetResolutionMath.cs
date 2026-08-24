using System.Collections.Generic;
using UnityEngine;

namespace SporeGobbo.Input
{
    public readonly struct TargetCandidateData
    {
        public TargetCandidateData(Vector2 position, bool isValid = true)
        {
            Position = position;
            IsValid = isValid;
        }

        public Vector2 Position { get; }
        public bool IsValid { get; }
    }

    public static class TargetResolutionMath
    {
        private const float ScoreTieTolerance = 0.0001f;

        public static bool TryScoreDirectionalCandidate(
            Vector2 source,
            Vector2 aimDirection,
            Vector2 target,
            float maxRange,
            float fullConeAngle,
            float alignmentWeight,
            float distanceWeight,
            out float score,
            out float distance)
        {
            score = float.NegativeInfinity;
            Vector2 offset = target - source;
            distance = offset.magnitude;
            if (maxRange <= 0f || distance > maxRange || distance <= Mathf.Epsilon || aimDirection.sqrMagnitude <= Mathf.Epsilon)
                return false;

            float minimumAlignment = Mathf.Cos(Mathf.Clamp(fullConeAngle, 0f, 360f) * 0.5f * Mathf.Deg2Rad);
            float alignment = Vector2.Dot(aimDirection.normalized, offset / distance);
            if (alignment + Mathf.Epsilon < minimumAlignment)
                return false;

            float alignment01 = Mathf.InverseLerp(minimumAlignment, 1f, alignment);
            float inverseDistance01 = 1f - Mathf.Clamp01(distance / maxRange);
            score = alignment01 * Mathf.Max(0f, alignmentWeight) +
                    inverseDistance01 * Mathf.Max(0f, distanceWeight);
            return true;
        }

        public static int SelectBestDirectionalCandidate(
            IReadOnlyList<TargetCandidateData> candidates,
            Vector2 source,
            Vector2 aimDirection,
            float maxRange,
            float fullConeAngle,
            float alignmentWeight,
            float distanceWeight)
        {
            int bestIndex = -1;
            float bestScore = float.NegativeInfinity;
            float bestDistance = float.PositiveInfinity;

            if (candidates == null)
                return bestIndex;

            for (int i = 0; i < candidates.Count; i++)
            {
                TargetCandidateData candidate = candidates[i];
                if (!candidate.IsValid || !TryScoreDirectionalCandidate(
                        source, aimDirection, candidate.Position, maxRange, fullConeAngle,
                        alignmentWeight, distanceWeight, out float score, out float distance))
                    continue;

                bool betterScore = score > bestScore + ScoreTieTolerance;
                bool tiedButCloser = Mathf.Abs(score - bestScore) <= ScoreTieTolerance && distance < bestDistance;
                if (!betterScore && !tiedButCloser)
                    continue;

                bestIndex = i;
                bestScore = score;
                bestDistance = distance;
            }

            return bestIndex;
        }
    }
}
