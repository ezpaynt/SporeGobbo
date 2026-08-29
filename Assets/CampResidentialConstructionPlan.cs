using System;
using System.Collections.Generic;
using UnityEngine;
using SporeGobbo.CampLifecycle;

public sealed class ResidentialDigStep
{
    public Vector2Int AdvanceCell { get; }
    public IReadOnlyList<Vector2Int> DigCenters { get; }
    public IReadOnlyList<IReadOnlyList<Vector2Int>> ExpectedRemovedCells { get; }

    public ResidentialDigStep(Vector2Int advanceCell, List<Vector2Int> centers,
        List<IReadOnlyList<Vector2Int>> expectedRemoved)
    {
        AdvanceCell = advanceCell;
        DigCenters = centers;
        ExpectedRemovedCells = expectedRemoved;
    }
}

/// <summary>An immutable, validated interpretation of one authored residential slot.</summary>
public sealed class ResidentialConstructionPlan
{
    public int SlotId { get; }
    public int ProgressionIndex { get; }
    public string RoomId { get; }
    public IReadOnlyList<Vector2Int> ApproachRoute { get; }
    public IReadOnlyList<ResidentialDigStep> DigSteps { get; }
    public IReadOnlyList<Vector2Int> ReturnRoute { get; }
    public IReadOnlyList<Vector2Int> Footprint { get; }

    ResidentialConstructionPlan(int slotId, int progressionIndex, string roomId,
        List<Vector2Int> approach, List<ResidentialDigStep> steps,
        List<Vector2Int> returnRoute, List<Vector2Int> footprint)
    {
        SlotId = slotId;
        ProgressionIndex = progressionIndex;
        RoomId = roomId;
        ApproachRoute = approach;
        DigSteps = steps;
        ReturnRoute = returnRoute;
        Footprint = footprint;
    }

    public static bool TryBuild(HandcraftedCampTerrain terrain, int slotId,
        Vector2 halfExtents, float digRadius, out ResidentialConstructionPlan plan, out string failure,
        int bodyLayer = -1)
    {
        plan = null;
        failure = null;
        CampResidentialCatalog catalog = terrain != null ? terrain.GetResidentialCatalog() : null;
        CampResidentialSlotDefinition definition = catalog?.GetSlot(slotId);
        if (definition == null || !catalog.TryGetRoomForSlot(slotId, out CampResidentialRoomDefinition room))
        { failure = "catalog slot/room is missing"; return false; }

        List<Vector2Int> footprint = terrain.GetResidentialSlotFootprint(slotId);
        List<Vector2Int> approach = terrain.GetResidentialConstructionRoute(slotId);
        if (approach.Count == 0) { failure = "authored approach route is empty"; return false; }
        foreach (Vector2Int cell in approach)
            if (terrain.IsBlocked(cell) || !CanOccupy(terrain, cell, halfExtents, null, null))
            { failure = "approach cell " + cell + " lacks full body clearance"; return false; }
        for (int i = 0; i < approach.Count; i++)
        {
            Vector2Int from = i == 0 ? approach[i] : approach[i - 1];
            if (!ValidateStaticPhysicsSegment(terrain, from, approach[i], halfExtents, bodyLayer,
                    out string blocker))
            { failure = "approach segment " + from + " -> " + approach[i] + " hits " + blocker; return false; }
        }

        HashSet<Vector2Int> domain = new HashSet<Vector2Int>(footprint);
        HashSet<Vector2Int> simulatedBlocked = new HashSet<Vector2Int>();
        foreach (Vector2Int cell in footprint)
        {
            if (terrain.GetSpatialDigCategory(cell) != CampDigCategory.ResidentialReserved)
            { failure = "footprint cell " + cell + " is outside ResidentialProgression authority"; return false; }
            if (terrain.IsBlocked(cell)) simulatedBlocked.Add(cell);
        }

        List<ResidentialDigStep> steps = new List<ResidentialDigStep>();
        Vector2Int stand = approach[approach.Count - 1];
        float reach = digRadius + Mathf.Max(halfExtents.x, halfExtents.y);
        foreach ((int x, int y) authored in definition.DigTargets)
        {
            Vector2Int target = new Vector2Int(authored.x, authored.y);
            List<Vector2Int> centers = new List<Vector2Int>();
            List<IReadOnlyList<Vector2Int>> expected = new List<IReadOnlyList<Vector2Int>>();
            int guard = footprint.Count + 1;
            while ((!CanOccupy(terrain, target, halfExtents, domain, simulatedBlocked) ||
                    !CanTraverse(terrain, stand, target, halfExtents, domain, simulatedBlocked)) && guard-- > 0)
            {
                Vector2Int center = ChooseDigCenter(terrain, footprint, simulatedBlocked,
                    stand, target, reach, digRadius);
                if (center.x == int.MinValue)
                { failure = "no authored Dig center can make " + stand + " -> " + target + " traversable"; return false; }
                List<Vector2Int> removed = ApplySimulatedDig(terrain, domain, simulatedBlocked, center, digRadius);
                if (removed.Count == 0) { failure = "planned Dig at " + center + " removes no cells"; return false; }
                centers.Add(center); expected.Add(removed);
            }
            if (guard < 0) { failure = "Dig planning exceeded finite footprint at " + target; return false; }
            if (!ValidateStaticPhysicsSegment(terrain, stand, target, halfExtents, bodyLayer,
                    out string blocker))
            { failure = "advance segment " + stand + " -> " + target + " hits " + blocker; return false; }
            steps.Add(new ResidentialDigStep(target, centers, expected));
            stand = target;
        }
        if (steps.Count == 0) { failure = "slot has no authored Dig steps"; return false; }
        List<Vector2Int> finalCenters = new List<Vector2Int>();
        List<IReadOnlyList<Vector2Int>> finalExpected = new List<IReadOnlyList<Vector2Int>>();
        int pocketGuard = footprint.Count + 1;
        while (simulatedBlocked.Count > 0 && pocketGuard-- > 0)
        {
            Vector2Int center = ChooseDigCenter(terrain, footprint, simulatedBlocked,
                stand, stand, reach, digRadius);
            if (center.x == int.MinValue)
            { failure = "final rest position cannot reach " + simulatedBlocked.Count + " required pocket cells"; return false; }
            List<Vector2Int> removed = ApplySimulatedDig(terrain, domain, simulatedBlocked, center, digRadius);
            finalCenters.Add(center);
            finalExpected.Add(removed);
        }
        if (simulatedBlocked.Count > 0)
        { failure = "authored Dig steps leave " + simulatedBlocked.Count + " required footprint cells blocked"; return false; }
        if (finalCenters.Count > 0)
            steps.Add(new ResidentialDigStep(stand, finalCenters, finalExpected));

        List<Vector2Int> returnRoute = new List<Vector2Int>();
        Vector2Int returnStart = steps[steps.Count - 1].AdvanceCell;
        for (int i = steps.Count - 2; i >= 0; i--)
            if (steps[i].AdvanceCell != returnStart)
            {
                returnRoute.Add(steps[i].AdvanceCell);
                returnStart = steps[i].AdvanceCell;
            }
        for (int i = approach.Count - 1; i >= 0; i--)
            if (returnRoute.Count == 0 || returnRoute[returnRoute.Count - 1] != approach[i]) returnRoute.Add(approach[i]);
        Vector2Int returnFrom = steps[steps.Count - 1].AdvanceCell;
        foreach (Vector2Int returnTo in returnRoute)
        {
            if (!ValidateStaticPhysicsSegment(terrain, returnFrom, returnTo, halfExtents, bodyLayer,
                    out string blocker))
            { failure = "return segment " + returnFrom + " -> " + returnTo + " hits " + blocker; return false; }
            returnFrom = returnTo;
        }
        plan = new ResidentialConstructionPlan(slotId, room.ProgressionIndex, room.RoomId,
            approach, steps, returnRoute, footprint);
        return true;
    }

    public static bool ValidateStaticPhysicsSegment(HandcraftedCampTerrain terrain,
        Vector2Int fromCell, Vector2Int toCell, Vector2 halfExtents, int bodyLayer, out string blocker)
    {
        blocker = null;
        if (terrain == null) { blocker = "missing terrain"; return false; }
        int resolvedBodyLayer = bodyLayer >= 0 ? bodyLayer : LayerMask.NameToLayer("Buddy");
        int collisionMask = resolvedBodyLayer >= 0
            ? Physics2D.GetLayerCollisionMask(resolvedBodyLayer) : Physics2D.AllLayers;
        Vector2 from = terrain.CellToWorld(fromCell);
        Vector2 to = terrain.CellToWorld(toCell);
        Vector2 delta = to - from;
        RaycastHit2D[] hits = Physics2D.BoxCastAll(from, halfExtents * 2f, 0f,
            delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector2.right,
            Mathf.Max(0.001f, delta.magnitude), collisionMask);
        foreach (RaycastHit2D hit in hits)
        {
            Collider2D collider = hit.collider;
            if (collider == null || !collider.enabled || collider.isTrigger ||
                collider is UnityEngine.Tilemaps.TilemapCollider2D) continue;
            Rigidbody2D attached = collider.attachedRigidbody;
            if (attached != null && attached.bodyType != RigidbodyType2D.Static) continue;
            blocker = GetHierarchyPath(collider.transform) + " (" + collider.GetType().Name +
                      ", layer=" + LayerMask.LayerToName(collider.gameObject.layer) +
                      ", bounds=" + collider.bounds + ")";
            return false;
        }
        return true;
    }

    static string GetHierarchyPath(Transform transform)
    {
        string path = transform != null ? transform.name : "missing";
        while (transform != null && transform.parent != null)
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }
        return path;
    }

    static Vector2Int ChooseDigCenter(HandcraftedCampTerrain terrain, List<Vector2Int> footprint,
        HashSet<Vector2Int> blocked, Vector2Int stand, Vector2Int desired, float reach, float radius)
    {
        Vector2Int best = new Vector2Int(int.MinValue, int.MinValue);
        float bestScore = float.PositiveInfinity;
        HashSet<Vector2Int> candidates = new HashSet<Vector2Int>();
        int range = Mathf.CeilToInt(radius / terrain.CellSize);
        foreach (Vector2Int blockedCell in blocked)
            for (int x = blockedCell.x - range; x <= blockedCell.x + range; x++)
            for (int y = blockedCell.y - range; y <= blockedCell.y + range; y++)
                candidates.Add(new Vector2Int(x, y));
        foreach (Vector2Int candidate in candidates)
        {
            float standDistance = Vector2.Distance(terrain.CellToWorld(stand), terrain.CellToWorld(candidate));
            if (standDistance > reach + 0.0001f || !RemovesAny(terrain, blocked, candidate, radius)) continue;
            float score = Vector2.Distance(terrain.CellToWorld(candidate), terrain.CellToWorld(desired)) +
                          standDistance * 0.01f;
            if (score < bestScore - 0.0001f || Mathf.Abs(score - bestScore) <= 0.0001f &&
                (candidate.x < best.x || candidate.x == best.x && candidate.y < best.y))
            { best = candidate; bestScore = score; }
        }
        return best;
    }

    static bool RemovesAny(HandcraftedCampTerrain terrain, HashSet<Vector2Int> blocked,
        Vector2Int center, float radius)
    {
        foreach (Vector2Int cell in blocked)
            if (Vector2.Distance(terrain.CellToWorld(center), terrain.CellToWorld(cell)) <= radius + 0.0001f)
                return true;
        return false;
    }

    static List<Vector2Int> ApplySimulatedDig(HandcraftedCampTerrain terrain, HashSet<Vector2Int> domain,
        HashSet<Vector2Int> blocked, Vector2Int center, float radius)
    {
        List<Vector2Int> removed = new List<Vector2Int>();
        foreach (Vector2Int cell in domain)
            if (blocked.Contains(cell) &&
                Vector2.Distance(terrain.CellToWorld(center), terrain.CellToWorld(cell)) <= radius + 0.0001f)
                removed.Add(cell);
        removed.Sort(CompareCells);
        foreach (Vector2Int cell in removed) blocked.Remove(cell);
        return removed;
    }

    static bool CanOccupy(HandcraftedCampTerrain terrain, Vector2Int center, Vector2 extents,
        HashSet<Vector2Int> domain, HashSet<Vector2Int> simulatedBlocked)
    {
        Vector2 world = terrain.CellToWorld(center);
        float halfCell = terrain.CellSize * 0.5f;
        int rx = Mathf.CeilToInt((extents.x + halfCell) / terrain.CellSize) + 1;
        int ry = Mathf.CeilToInt((extents.y + halfCell) / terrain.CellSize) + 1;
        for (int x = center.x - rx; x <= center.x + rx; x++)
        for (int y = center.y - ry; y <= center.y + ry; y++)
        {
            Vector2Int cell = new Vector2Int(x, y);
            bool blocked = domain != null && domain.Contains(cell)
                ? simulatedBlocked.Contains(cell) : terrain.IsBlocked(cell);
            if (!blocked) continue;
            Vector2 cellWorld = terrain.CellToWorld(cell);
            if (Mathf.Abs(world.x - cellWorld.x) <= extents.x + halfCell &&
                Mathf.Abs(world.y - cellWorld.y) <= extents.y + halfCell) return false;
        }
        return true;
    }

    static bool CanTraverse(HandcraftedCampTerrain terrain, Vector2Int from, Vector2Int to, Vector2 extents,
        HashSet<Vector2Int> domain, HashSet<Vector2Int> simulatedBlocked)
    {
        Vector2 start = terrain.CellToWorld(from), end = terrain.CellToWorld(to);
        int count = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(start, end) /
            Mathf.Max(0.01f, terrain.CellSize * 0.125f)));
        for (int i = 0; i <= count; i++)
        {
            Vector2 sample = Vector2.Lerp(start, end, i / (float)count);
            Vector2Int sampleCell = terrain.WorldToCell(sample);
            if (!CanOccupyWorld(terrain, sample, sampleCell, extents, domain, simulatedBlocked)) return false;
        }
        return true;
    }

    static bool CanOccupyWorld(HandcraftedCampTerrain terrain, Vector2 world, Vector2Int center,
        Vector2 extents, HashSet<Vector2Int> domain, HashSet<Vector2Int> simulatedBlocked)
    {
        float halfCell = terrain.CellSize * 0.5f;
        int range = Mathf.CeilToInt((Mathf.Max(extents.x, extents.y) + halfCell) / terrain.CellSize) + 1;
        for (int x = center.x - range; x <= center.x + range; x++)
        for (int y = center.y - range; y <= center.y + range; y++)
        {
            Vector2Int cell = new Vector2Int(x, y);
            bool blocked = domain != null && domain.Contains(cell)
                ? simulatedBlocked.Contains(cell) : terrain.IsBlocked(cell);
            if (!blocked) continue;
            Vector2 c = terrain.CellToWorld(cell);
            if (Mathf.Abs(world.x - c.x) <= extents.x + halfCell &&
                Mathf.Abs(world.y - c.y) <= extents.y + halfCell) return false;
        }
        return true;
    }

    static int CompareCells(Vector2Int a, Vector2Int b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y);
}
