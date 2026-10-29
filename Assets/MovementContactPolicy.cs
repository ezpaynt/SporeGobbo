using UnityEngine;

public enum MovementContactRole { Strict, CampResident, CampSpectator }
public enum MovementResolutionState { Idle, Moving, QueuedForAccess, Yielding, Settling, Arrived, Blocked }

/// <summary>The existing session owns membership. Presence of a session is not a fighter role.</summary>
public interface IMovementContactSession
{
    bool IsMovementParticipant(GameObject actor);
}

/// <summary>Pure peer geometry. Never changes terrain extents, colliders or combat queries.</summary>
public static class MovementContactPolicy
{
    // Initial development tolerance: at most 8% of the smaller body's short dimension.
    // A resting destination still requires zero peer overlap.
    public const float CampOverlapFraction = .08f;
    public static bool SoftPair(MovementContactRole a, MovementContactRole b)
        => a != MovementContactRole.Strict && b != MovementContactRole.Strict;

    public static float Allowance(Vector2 a, Vector2 b, bool soft)
        => soft ? 2 * Mathf.Min(Mathf.Min(a.x, a.y), Mathf.Min(b.x, b.y)) * CampOverlapFraction : 0;

    public static float Penetration(Vector2 relative, Vector2 halfSum)
        => Mathf.Max(0, Mathf.Min(halfSum.x - Mathf.Abs(relative.x), halfSum.y - Mathf.Abs(relative.y)));

    // Relative swept AABB test. Endpoint separation alone permits fast actors to phase through.
    public static bool SweepClear(Vector2 relative, Vector2 delta, Vector2 halfSum, float allowance)
    {
        Vector2 core = Vector2.Max(Vector2.zero, halfSum - Vector2.one * (allowance + .0001f));
        bool inside = Mathf.Abs(relative.x) < core.x && Mathf.Abs(relative.y) < core.y;
        if (inside)
        {
            // Existing solver-contact jitter may separate, but never deepen or cross the other body.
            Vector2 end = relative + delta;
            float before = Penetration(relative, core), after = Penetration(end, core);
            int nearest=core.x-Mathf.Abs(relative.x)<=core.y-Mathf.Abs(relative.y)?0:1;
            return relative[nearest]*delta[nearest]>=-.000001f && after <= before + .000001f;
        }
        float enter = 0, exit = 1;
        for (int axis = 0; axis < 2; axis++)
        {
            float p = relative[axis], d = delta[axis], extent = core[axis];
            if (Mathf.Abs(d) < .000001f) { if (Mathf.Abs(p) >= extent) return true; continue; }
            float a = (-extent - p) / d, b = (extent - p) / d;
            if (a > b) { float swap = a; a = b; b = swap; }
            enter = Mathf.Max(enter, a); exit = Mathf.Min(exit, b);
            if (enter >= exit) return true;
        }
        return enter >= 1 || exit <= 0;
    }
}
