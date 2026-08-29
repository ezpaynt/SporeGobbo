using System;
using System.Collections.Generic;

namespace SporeGobbo.CampLifecycle
{
    public static class CampBuddyPhysicalPolicy
    {
        public const float MovementSpeedMultiplier = 0.45f;
        public const float MinimumMovementSpeed = 0.2f;
        public const float DirectedWalkTimeoutMargin = 1.25f;

        // Active/reserve is a participation distinction, not a physical-form modifier.
        public static float GetScaleMultiplier(bool activeSquad) => 1f;

        public static float GetMovementSpeed(float savedMoveSpeed, bool activeSquad)
        {
            return Math.Max(MinimumMovementSpeed, savedMoveSpeed * MovementSpeedMultiplier);
        }

        public static float GetDirectedWalkTimeout(float distance, float actualMovementSpeed, float minimumTimeout)
        {
            return Math.Max(minimumTimeout,
                Math.Max(0f, distance) / Math.Max(MinimumMovementSpeed, actualMovementSpeed) + DirectedWalkTimeoutMargin);
        }

        public static bool RequiresFullWaypointClearance(int waypointIndex, int waypointCount) =>
            waypointIndex >= 0 && waypointIndex < Math.Max(0, waypointCount);
    }

    public readonly struct CampResidentialArrivalEvaluation
    {
        public readonly int LivingBuddyCount;
        public readonly int EstablishedCapacity;
        public readonly int VacantEstablishedSlots;
        public readonly int VacancyClaims;
        public readonly int UnassignedBuddies;
        public readonly int PendingConstructionCount;
        public readonly int FirstSlot;
        public readonly bool ArrivalPhase;

        public CampResidentialArrivalEvaluation(int livingBuddyCount, int establishedCapacity,
            int vacantEstablishedSlots, int vacancyClaims, int unassignedBuddies,
            int pendingConstructionCount, int firstSlot, bool arrivalPhase)
        {
            LivingBuddyCount = livingBuddyCount;
            EstablishedCapacity = establishedCapacity;
            VacantEstablishedSlots = vacantEstablishedSlots;
            VacancyClaims = vacancyClaims;
            UnassignedBuddies = unassignedBuddies;
            PendingConstructionCount = pendingConstructionCount;
            FirstSlot = firstSlot;
            ArrivalPhase = arrivalPhase;
        }
    }

    public static class CampArrivalPolicy
    {
        public const float FirstHomeMovementMultiplier = 2f;

        public static float GetFirstHomeMovementSpeed(float normalCampSpeed) =>
            Math.Max(CampBuddyPhysicalPolicy.MinimumMovementSpeed, normalCampSpeed) *
            FirstHomeMovementMultiplier;

        public static float GetCampMovementSpeed(float normalCampSpeed, bool firstHomeArrival) =>
            firstHomeArrival
                ? GetFirstHomeMovementSpeed(normalCampSpeed)
                : Math.Max(CampBuddyPhysicalPolicy.MinimumMovementSpeed, normalCampSpeed);

        public static bool IsFirstHomeClaim(bool previouslyHomeless, int assignedSlotId) =>
            previouslyHomeless && assignedSlotId > 0;

        public static List<int> ReserveContiguousConstructionSlots(int firstSlot, int count)
        {
            List<int> result = new List<int>();
            for (int i = 0; i < Math.Max(0, count); i++) result.Add(Math.Max(1, firstSlot) + i);
            return result;
        }

        public static bool CanBeginReservedConstruction(int slotId, int dependencySlotId,
            int establishedSlots) =>
            slotId == establishedSlots + 1 && dependencySlotId <= establishedSlots;

        public static bool ShouldBegin(int vacancyClaims, int pendingConstructions) =>
            vacancyClaims > 0 || pendingConstructions > 0;

        public static bool ShouldSpawnAtPlayerArrival(bool activeSquad) => activeSquad;

        public static bool ShouldReleaseToWander(bool arrivalPhase, int remainingWork) =>
            !arrivalPhase || remainingWork <= 0;

        public static bool CanUseActivityPoint(bool residentialRest, bool available,
            int pointResidentialSlot, int gobboResidentialSlot) =>
            available && (!residentialRest || pointResidentialSlot > 0 && pointResidentialSlot == gobboResidentialSlot);

        public static CampResidentialArrivalEvaluation EvaluateResidentialWork(
            ResidentialOccupancyResolution occupancy, int livingBuddyCount, int vacancyClaims,
            int establishedSlots, int capacity)
        {
            int established = Math.Min(Math.Max(0, establishedSlots), Math.Max(0, capacity));
            int unassigned = occupancy?.UnassignedLivingBuddyIds.Count ?? 0;
            int pending = Math.Min(Math.Max(0, capacity - established), unassigned);
            int vacant = occupancy?.VacantEstablishedSlots.Count ?? 0;
            int claims = Math.Max(0, vacancyClaims);
            return new CampResidentialArrivalEvaluation(
                Math.Max(0, livingBuddyCount), established, vacant, claims, unassigned,
                pending, established + 1, ShouldBegin(claims, pending));
        }
    }

    public enum CampZoneKind
    {
        HomeCore = 0,
        PermanentExit = 1,
        PermanentMemorial = 2,
        UnstableCollapse = 9,
        IntroArrivalClearance = 10,
        NormalArrivalClearance = 11,
        CirculationClearance = 12,
        GeneralUnreserved = 13
    }

    public enum CampDigCategory
    {
        NormalCampDiggable,
        NeverDiggable,
        ResidentialReserved,
        CollapseEligible
    }

    public enum TerrainDigAuthority
    {
        Player,
        Buddy,
        ResidentialProgression
    }

    public readonly struct ResidentialSlotRecord
    {
        public readonly int SlotIndex;
        public readonly int DependencySlotIndex;
        public readonly (int x, int y) Center;
        public readonly (int x, int y) Approach;
        public readonly IReadOnlyList<(int x, int y)> DigTargets;

        public ResidentialSlotRecord(int slotIndex, int dependencySlotIndex,
            (int x, int y) center, (int x, int y) approach,
            params (int x, int y)[] digTargets)
        {
            SlotIndex = slotIndex;
            DependencySlotIndex = dependencySlotIndex;
            Center = center;
            Approach = approach;
            DigTargets = digTargets;
        }
    }

    public readonly struct ResidentialOccupantRecord
    {
        public readonly string GobboId;
        public readonly int AssignedSlotId;
        public readonly bool IsLivingBuddy;

        public ResidentialOccupantRecord(string gobboId, int assignedSlotId, bool isLivingBuddy)
        {
            GobboId = gobboId ?? "";
            AssignedSlotId = assignedSlotId;
            IsLivingBuddy = isLivingBuddy;
        }
    }

    public sealed class ResidentialOccupancyResolution
    {
        public readonly Dictionary<string, int> Assignments = new Dictionary<string, int>(StringComparer.Ordinal);
        public readonly List<int> VacantEstablishedSlots = new List<int>();
        public readonly List<string> UnassignedLivingBuddyIds = new List<string>();
    }

    [Serializable]
    public readonly struct CampCellRect
    {
        public readonly int X;
        public readonly int Y;
        public readonly int Width;
        public readonly int Height;

        public CampCellRect(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = Math.Max(0, width);
            Height = Math.Max(0, height);
        }

        public int XMax => X + Width;
        public int YMax => Y + Height;
        public bool Contains(int x, int y) => x >= X && x < XMax && y >= Y && y < YMax;
        public bool Overlaps(CampCellRect other) => X < other.XMax && XMax > other.X && Y < other.YMax && YMax > other.Y;
        public bool Touches(CampCellRect other)
        {
            bool xTouch = X <= other.XMax && XMax >= other.X;
            bool yTouch = Y <= other.YMax && YMax >= other.Y;
            return xTouch && yTouch;
        }
    }

    public readonly struct CampZoneRecord
    {
        public readonly CampZoneKind Kind;
        public readonly CampCellRect Bounds;

        public CampZoneRecord(CampZoneKind kind, CampCellRect bounds)
        {
            Kind = kind;
            Bounds = bounds;
        }
    }

    public static class CampSpatialPolicy
    {
        public static CampDigCategory Classify(IEnumerable<CampZoneKind> zones)
        {
            bool collapse = false;
            if (zones != null)
            {
                foreach (CampZoneKind zone in zones)
                {
                    if (IsPermanent(zone)) return CampDigCategory.NeverDiggable;
                    if (zone == CampZoneKind.UnstableCollapse) collapse = true;
                }
            }
            if (collapse) return CampDigCategory.CollapseEligible;
            return CampDigCategory.NormalCampDiggable;
        }

        public static bool CanApplyOrdinaryOrSavedClear(CampDigCategory category)
        {
            return category == CampDigCategory.NormalCampDiggable || category == CampDigCategory.CollapseEligible;
        }

        public const double BuddyDigRadiusInCells = 1.2;
        public const double ResidentialPocketRadiusInCells = 1.5;
        // Half-width/height of the runtime 0.75 x 0.75 Baby box on the 0.6 Camp grid.
        public const double ResidentialClearanceRadiusInCells = 0.625d;

        public static List<(int x, int y)> BuildOpenCellRoute((int x, int y) start,
            (int x, int y) goal, ISet<(int x, int y)> openCells)
        {
            List<(int x, int y)> result = new List<(int x, int y)>();
            if (openCells == null || !openCells.Contains(start) || !openCells.Contains(goal)) return result;
            if (start == goal) return result;

            Queue<(int x, int y)> frontier = new Queue<(int x, int y)>();
            Dictionary<(int x, int y), (int x, int y)> previous =
                new Dictionary<(int x, int y), (int x, int y)> { [start] = start };
            frontier.Enqueue(start);
            while (frontier.Count > 0 && !previous.ContainsKey(goal))
            {
                (int x, int y) current = frontier.Dequeue();
                List<(int x, int y)> neighbors = new List<(int x, int y)>
                {
                    (current.x + 1, current.y), (current.x - 1, current.y),
                    (current.x, current.y + 1), (current.x, current.y - 1)
                };
                neighbors.Sort((a, b) =>
                {
                    int aDistance = Math.Abs(goal.x - a.x) + Math.Abs(goal.y - a.y);
                    int bDistance = Math.Abs(goal.x - b.x) + Math.Abs(goal.y - b.y);
                    return aDistance != bDistance ? aDistance.CompareTo(bDistance) :
                        a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y);
                });
                foreach ((int x, int y) next in neighbors)
                {
                    if (!openCells.Contains(next) || previous.ContainsKey(next)) continue;
                    previous[next] = current;
                    frontier.Enqueue(next);
                }
            }
            if (!previous.ContainsKey(goal)) return result;

            (int x, int y) step = goal;
            while (step != start)
            {
                result.Insert(0, step);
                step = previous[step];
            }
            return result;
        }

        public static bool CanOccupyCellCenter((int x, int y) center,
            ISet<(int x, int y)> openCells, double bodyRadiusInCells)
        {
            if (openCells == null || !openCells.Contains(center)) return false;
            double radius = Math.Max(0d, bodyRadiusInCells);
            int range = (int)Math.Ceiling(radius + 0.5d);
            for (int x = center.x - range; x <= center.x + range; x++)
            for (int y = center.y - range; y <= center.y + range; y++)
            {
                if (openCells.Contains((x, y))) continue;
                double closestX = Math.Max(x - 0.5d, Math.Min(center.x, x + 0.5d));
                double closestY = Math.Max(y - 0.5d, Math.Min(center.y, y + 0.5d));
                double dx = closestX - center.x;
                double dy = closestY - center.y;
                if (dx * dx + dy * dy <= radius * radius) return false;
            }
            return true;
        }

        public static bool CanOccupyCellCenterBox((int x, int y) center,
            ISet<(int x, int y)> openCells, double halfWidth, double halfHeight)
        {
            if (openCells == null || !openCells.Contains(center)) return false;
            int rangeX = (int)Math.Ceiling(Math.Max(0d, halfWidth) + 0.5d);
            int rangeY = (int)Math.Ceiling(Math.Max(0d, halfHeight) + 0.5d);
            for (int x = center.x - rangeX; x <= center.x + rangeX; x++)
            for (int y = center.y - rangeY; y <= center.y + rangeY; y++)
                if (!openCells.Contains((x, y)) &&
                    Math.Abs(x - center.x) <= halfWidth + 0.5d &&
                    Math.Abs(y - center.y) <= halfHeight + 0.5d) return false;
            return true;
        }

        public static bool IsFireSocialDestinationValid(bool inBounds, bool blocked) => inBounds && !blocked;

        public static ResidentialOccupancyResolution ResolveResidentialOccupancy(
            IReadOnlyList<ResidentialOccupantRecord> occupants, int establishedSlots, int capacity)
        {
            int established = Math.Min(Math.Max(0, establishedSlots), Math.Max(0, capacity));
            ResidentialOccupancyResolution result = new ResidentialOccupancyResolution();
            HashSet<int> occupied = new HashSet<int>();
            List<ResidentialOccupantRecord> ordered = occupants != null
                ? new List<ResidentialOccupantRecord>(occupants) : new List<ResidentialOccupantRecord>();
            ordered.Sort((a, b) => string.Compare(a.GobboId, b.GobboId, StringComparison.Ordinal));
            List<ResidentialOccupantRecord> needsSlot = new List<ResidentialOccupantRecord>();

            foreach (ResidentialOccupantRecord occupant in ordered)
            {
                if (string.IsNullOrWhiteSpace(occupant.GobboId)) continue;
                if (!occupant.IsLivingBuddy)
                {
                    result.Assignments[occupant.GobboId] = 0;
                    continue;
                }
                if (occupant.AssignedSlotId >= 1 && occupant.AssignedSlotId <= established &&
                    occupied.Add(occupant.AssignedSlotId))
                    result.Assignments[occupant.GobboId] = occupant.AssignedSlotId;
                else needsSlot.Add(occupant);
            }

            Queue<int> vacancies = new Queue<int>();
            for (int slot = 1; slot <= established; slot++) if (!occupied.Contains(slot)) vacancies.Enqueue(slot);
            foreach (ResidentialOccupantRecord occupant in needsSlot)
            {
                int slot = vacancies.Count > 0 ? vacancies.Dequeue() : 0;
                result.Assignments[occupant.GobboId] = slot;
                if (slot == 0) result.UnassignedLivingBuddyIds.Add(occupant.GobboId);
                else occupied.Add(slot);
            }
            for (int slot = 1; slot <= established; slot++) if (!occupied.Contains(slot)) result.VacantEstablishedSlots.Add(slot);
            return result;
        }

        public static bool CanDig(CampDigCategory category, TerrainDigAuthority authority,
            bool authorizedResidentialCell)
        {
            if (authority == TerrainDigAuthority.ResidentialProgression && authorizedResidentialCell) return true;
            if (category == CampDigCategory.NeverDiggable || category == CampDigCategory.ResidentialReserved) return false;
            return authority == TerrainDigAuthority.Player || authority == TerrainDigAuthority.Buddy;
        }

        public static bool CanAuthorizeResidentialProgression(int requestedStage, int expectedStage,
            bool exactExpectedFootprint)
        {
            return requestedStage > 0 && requestedStage == expectedStage && exactExpectedFootprint;
        }

        public static bool CanCommitResidentialConstruction(int requiredDigActions, int successfulDigActions,
            int blockedRequiredCells, bool finalAdvanceSucceeded = true)
        {
            return requiredDigActions >= 0 && successfulDigActions == requiredDigActions &&
                   blockedRequiredCells == 0 && finalAdvanceSucceeded;
        }

        public static bool CanRunResidentialSuccessCompletion(bool slotEstablished, bool assignmentSucceeded) =>
            slotEstablished && assignmentSucceeded;

        public static bool ShouldPresentResidentialMilestone(int completedConstructions) =>
            completedConstructions > 0;

        public static bool IsPermanent(CampZoneKind zone)
        {
            return zone == CampZoneKind.HomeCore || zone == CampZoneKind.PermanentExit ||
                   zone == CampZoneKind.PermanentMemorial ||
                   zone == CampZoneKind.IntroArrivalClearance || zone == CampZoneKind.NormalArrivalClearance ||
                   zone == CampZoneKind.CirculationClearance;
        }

        public static List<string> Validate(IReadOnlyList<CampZoneRecord> zones)
        {
            List<string> issues = new List<string>();
            if (zones == null) return issues;
            for (int i = 0; i < zones.Count; i++)
            for (int j = i + 1; j < zones.Count; j++)
            {
                CampZoneRecord a = zones[i];
                CampZoneRecord b = zones[j];
                if (!a.Bounds.Overlaps(b.Bounds)) continue;
                if ((a.Kind == CampZoneKind.UnstableCollapse && IsStructuralPermanent(b.Kind)) ||
                    (b.Kind == CampZoneKind.UnstableCollapse && IsStructuralPermanent(a.Kind)))
                    issues.Add($"Collapse zone overlaps permanent {a.Kind}/{b.Kind} structure.");
            }
            return issues;
        }

        static bool IsStructuralPermanent(CampZoneKind zone)
        {
            return zone == CampZoneKind.PermanentExit || zone == CampZoneKind.PermanentMemorial;
        }
    }
}
