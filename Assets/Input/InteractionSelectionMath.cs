using System.Collections.Generic;

namespace SporeGobbo.Input
{
    public readonly struct InteractionCandidateData
    {
        public InteractionCandidateData(int id, int priority, float distance, float facingAlignment, bool isValid = true)
        {
            Id = id;
            Priority = priority;
            Distance = distance;
            FacingAlignment = facingAlignment;
            IsValid = isValid;
        }

        public int Id { get; }
        public int Priority { get; }
        public float Distance { get; }
        public float FacingAlignment { get; }
        public bool IsValid { get; }
    }

    public static class InteractionSelectionMath
    {
        public static int Select(
            IReadOnlyList<InteractionCandidateData> candidates,
            int currentId,
            float facingTieDistance,
            float switchDistanceAdvantage)
        {
            int bestIndex = -1;
            int currentIndex = -1;

            if (candidates == null)
                return bestIndex;

            for (int i = 0; i < candidates.Count; i++)
            {
                InteractionCandidateData candidate = candidates[i];
                if (!candidate.IsValid)
                    continue;

                if (candidate.Id == currentId)
                    currentIndex = i;

                if (bestIndex < 0 || IsBetter(candidate, candidates[bestIndex], facingTieDistance))
                    bestIndex = i;
            }

            if (bestIndex < 0 || currentIndex < 0 || bestIndex == currentIndex)
                return bestIndex;

            InteractionCandidateData best = candidates[bestIndex];
            InteractionCandidateData current = candidates[currentIndex];
            if (best.Priority > current.Priority)
                return bestIndex;
            if (best.Priority < current.Priority)
                return currentIndex;

            return best.Distance + switchDistanceAdvantage < current.Distance
                ? bestIndex
                : currentIndex;
        }

        private static bool IsBetter(InteractionCandidateData candidate, InteractionCandidateData incumbent, float facingTieDistance)
        {
            if (candidate.Priority != incumbent.Priority)
                return candidate.Priority > incumbent.Priority;

            float distanceDifference = candidate.Distance - incumbent.Distance;
            if (System.Math.Abs(distanceDifference) > facingTieDistance)
                return distanceDifference < 0f;

            if (candidate.FacingAlignment != incumbent.FacingAlignment)
                return candidate.FacingAlignment > incumbent.FacingAlignment;

            if (candidate.Distance != incumbent.Distance)
                return candidate.Distance < incumbent.Distance;

            return candidate.Id < incumbent.Id;
        }
    }
}
