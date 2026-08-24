using UnityEngine;

namespace SporeGobbo.Input
{
    public static class CorePlayerControlMath
    {
        private const float MinimumAttackSpeed = 0.01f;
        private const float DefaultMoveThreshold = 0.2f;

        public static float GetEffectiveAttackInterval(float baseInterval, float attackSpeed)
        {
            return Mathf.Max(0f, baseInterval) / Mathf.Max(MinimumAttackSpeed, attackSpeed);
        }

        public static Vector2 ResolveDashDirection(
            Vector2 move,
            Vector2 aim,
            Vector2 fallback,
            float moveThreshold = DefaultMoveThreshold)
        {
            float thresholdSquared = Mathf.Max(0f, moveThreshold) * Mathf.Max(0f, moveThreshold);
            if (move.sqrMagnitude >= thresholdSquared)
                return move.normalized;
            if (aim.sqrMagnitude > 0.0001f)
                return aim.normalized;
            if (fallback.sqrMagnitude > 0.0001f)
                return fallback.normalized;
            return Vector2.down;
        }
    }
}
