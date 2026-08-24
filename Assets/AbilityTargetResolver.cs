using System;
using System.Collections.Generic;
using SporeGobbo.Input;
using UnityEngine;

public enum AbilityTargetingMode
{
    PrecisePointer,
    DirectionalCone
}

public struct AbilityTargetRequest
{
    public AbilityTargetingMode Mode { get; set; }
    public Vector2 Source { get; set; }
    public Vector2 AimDirection { get; set; }
    public Vector2 PointerWorldPosition { get; set; }
    public float MaxRange { get; set; }
    public LayerMask TargetLayers { get; set; }
    public float FullConeAngle { get; set; }
    public float AlignmentWeight { get; set; }
    public float DistanceWeight { get; set; }
    public Func<Collider2D, Transform> ResolveEligibleTarget { get; set; }
    public Func<Vector2, Vector2, bool> HasLineOfSight { get; set; }
}

public readonly struct AbilityTargetResult
{
    public AbilityTargetResult(Transform target, Collider2D collider)
    {
        Target = target;
        Collider = collider;
    }

    public Transform Target { get; }
    public Collider2D Collider { get; }
    public bool HasTarget => Target != null;
}

public static class AbilityTargetResolver
{
    public static AbilityTargetResult Resolve(AbilityTargetRequest request)
    {
        return request.Mode == AbilityTargetingMode.PrecisePointer
            ? ResolvePrecisePointer(request)
            : ResolveDirectionalCone(request);
    }

    private static AbilityTargetResult ResolvePrecisePointer(AbilityTargetRequest request)
    {
        Collider2D hit = request.TargetLayers.value == 0
            ? Physics2D.OverlapPoint(request.PointerWorldPosition)
            : Physics2D.OverlapPoint(request.PointerWorldPosition, request.TargetLayers);

        return Validate(hit, request, out Transform target)
            ? new AbilityTargetResult(target, hit)
            : default;
    }

    private static AbilityTargetResult ResolveDirectionalCone(AbilityTargetRequest request)
    {
        Collider2D[] hits = request.TargetLayers.value == 0
            ? Physics2D.OverlapCircleAll(request.Source, request.MaxRange)
            : Physics2D.OverlapCircleAll(request.Source, request.MaxRange, request.TargetLayers);

        var targets = new List<Transform>();
        var colliders = new List<Collider2D>();
        var candidates = new List<TargetCandidateData>();
        var seen = new HashSet<Transform>();

        foreach (Collider2D hit in hits)
        {
            if (!Validate(hit, request, out Transform target) || !seen.Add(target))
                continue;

            targets.Add(target);
            colliders.Add(hit);
            candidates.Add(new TargetCandidateData(target.position));
        }

        int index = TargetResolutionMath.SelectBestDirectionalCandidate(
            candidates, request.Source, request.AimDirection, request.MaxRange,
            request.FullConeAngle, request.AlignmentWeight, request.DistanceWeight);

        return index >= 0 ? new AbilityTargetResult(targets[index], colliders[index]) : default;
    }

    private static bool Validate(Collider2D hit, AbilityTargetRequest request, out Transform target)
    {
        target = hit != null && request.ResolveEligibleTarget != null
            ? request.ResolveEligibleTarget(hit)
            : null;

        if (target == null || Vector2.Distance(request.Source, target.position) > request.MaxRange)
            return false;

        return request.HasLineOfSight == null || request.HasLineOfSight(request.Source, target.position);
    }
}
