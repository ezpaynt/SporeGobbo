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

        public static float GetConstructionEdgeArrivalDistance(float bodyRadius, float cellSize,
            float normalArrivalDistance = 0.18f)
        {
            float halfCell = Math.Max(0f, cellSize) * 0.5f;
            return Math.Max(normalArrivalDistance, Math.Min(Math.Max(0f, bodyRadius), halfCell));
        }
    }

    public static class CampArrivalPolicy
    {
        public static bool ShouldBegin(int vacancyClaims, int pendingConstructions) =>
            vacancyClaims > 0 || pendingConstructions > 0;

        public static bool ShouldSpawnAtPlayerArrival(bool activeSquad) => activeSquad;

        public static bool ShouldReleaseToWander(bool arrivalPhase, int remainingWork) =>
            !arrivalPhase || remainingWork <= 0;

        public static bool CanUseActivityPoint(bool residentialRest, bool available,
            int pointResidentialSlot, int gobboResidentialSlot) =>
            available && (!residentialRest || pointResidentialSlot > 0 && pointResidentialSlot == gobboResidentialSlot);
    }

    public enum CampZoneKind
    {
        HomeCore,
        PermanentExit,
        PermanentMemorial,
        ResidentialStage1,
        ResidentialStage2,
        ResidentialStage3,
        ResidentialStage4,
        ResidentialStage5,
        ResidentialEntrance,
        UnstableCollapse,
        IntroArrivalClearance,
        NormalArrivalClearance,
        CirculationClearance,
        GeneralUnreserved
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
            bool residential = false;
            bool collapse = false;
            if (zones != null)
            {
                foreach (CampZoneKind zone in zones)
                {
                    if (IsPermanent(zone)) return CampDigCategory.NeverDiggable;
                    if (IsResidential(zone)) residential = true;
                    if (zone == CampZoneKind.UnstableCollapse) collapse = true;
                }
            }
            if (residential) return CampDigCategory.ResidentialReserved;
            if (collapse) return CampDigCategory.CollapseEligible;
            return CampDigCategory.NormalCampDiggable;
        }

        public static bool CanApplyOrdinaryOrSavedClear(CampDigCategory category)
        {
            return category == CampDigCategory.NormalCampDiggable || category == CampDigCategory.CollapseEligible;
        }

        public static bool IsResidentialTerrainZone(CampZoneKind zone)
        {
            return IsResidential(zone) || zone == CampZoneKind.ResidentialEntrance;
        }

        public const int StageOneSlotCapacity = 10;
        public const double BuddyDigRadiusInCells = 1.2;
        public const double ResidentialPocketRadiusInCells = 1.5;
        // 0.4 world units on the current 0.6 grid: clears the observed 0.375 body with modest form headroom.
        public const double ResidentialClearanceRadiusInCells = 2d / 3d;

        public static List<ResidentialSlotRecord> BuildStageOneSlots(CampCellRect entrance, CampCellRect chamber)
        {
            int midY = entrance.Y + entrance.Height / 2;
            (int x, int y) s1 = (chamber.X + 2, chamber.Y + 4);
            return new List<ResidentialSlotRecord>
            {
                new ResidentialSlotRecord(1, 0, s1, (entrance.X - 1, midY),
                    (entrance.X, midY), (entrance.X + 1, midY),
                    (chamber.X, midY), (chamber.X + 1, midY), s1),
                BuildConnectedSlot(2, 1, (chamber.X + 3, chamber.Y + 2), s1),
                BuildConnectedSlot(3, 1, (chamber.X + 3, chamber.Y + 6), s1),
                BuildConnectedSlot(4, 1, (chamber.X + 5, chamber.Y + 4), s1),
                BuildConnectedSlot(5, 2, (chamber.X + 5, chamber.Y + 1), (chamber.X + 3, chamber.Y + 2)),
                BuildConnectedSlot(6, 3, (chamber.X + 5, chamber.Y + 6), (chamber.X + 3, chamber.Y + 6)),
                BuildConnectedSlot(7, 4, (chamber.X + 6, chamber.Y + 3), (chamber.X + 5, chamber.Y + 4)),
                BuildConnectedSlot(8, 4, (chamber.X + 6, chamber.Y + 6), (chamber.X + 5, chamber.Y + 4)),
                BuildConnectedSlot(9, 2, (chamber.X + 1, chamber.Y + 1), (chamber.X + 3, chamber.Y + 2)),
                BuildConnectedSlot(10, 3, (chamber.X + 1, chamber.Y + 6), (chamber.X + 3, chamber.Y + 6))
            };
        }

        static ResidentialSlotRecord BuildConnectedSlot(int slotIndex, int dependencySlotIndex,
            (int x, int y) center, (int x, int y) approach)
        {
            List<(int x, int y)> targets = new List<(int x, int y)>();
            (int x, int y) current = approach;
            while (current.y != center.y)
            {
                current = (current.x, current.y + Math.Sign(center.y - current.y));
                targets.Add(current);
            }
            while (current.x != center.x)
            {
                current = (current.x + Math.Sign(center.x - current.x), current.y);
                targets.Add(current);
            }
            return new ResidentialSlotRecord(slotIndex, dependencySlotIndex, center, approach, targets.ToArray());
        }

        public static List<(int x, int y)> BuildStageOneConstructionRoute(int slotIndex,
            CampCellRect entrance, CampCellRect chamber)
        {
            List<ResidentialSlotRecord> slots = BuildStageOneSlots(entrance, chamber);
            ResidentialSlotRecord target = slots.Find(slot => slot.SlotIndex == slotIndex);
            if (target.SlotIndex == 0) return new List<(int x, int y)>();

            (int x, int y) exteriorApproach = slots[0].Approach;
            if (slotIndex == 1) return new List<(int x, int y)> { exteriorApproach };

            HashSet<(int x, int y)> open = BuildEstablishedSlotFootprint(
                slots, slotIndex - 1, entrance, chamber);
            open.Add(exteriorApproach);
            open.Add(target.Approach);
            List<(int x, int y)> route = BuildOpenCellRoute(exteriorApproach, target.Approach, open);
            if (route.Count == 0) return route;
            route.Insert(0, exteriorApproach);
            return route;
        }

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

        public static List<(int x, int y)> BuildSlotFootprint(ResidentialSlotRecord slot,
            CampCellRect entrance, CampCellRect chamber)
        {
            HashSet<(int x, int y)> cells = new HashSet<(int x, int y)>();
            double radiusSquared = ResidentialPocketRadiusInCells * ResidentialPocketRadiusInCells;
            foreach ((int x, int y) target in slot.DigTargets)
            for (int x = target.x - 2; x <= target.x + 2; x++)
            for (int y = target.y - 2; y <= target.y + 2; y++)
                if ((entrance.Contains(x, y) || chamber.Contains(x, y)) &&
                    Math.Pow(x - target.x, 2) + Math.Pow(y - target.y, 2) <= radiusSquared)
                    cells.Add((x, y));
            return new List<(int x, int y)>(cells);
        }

        public static int ResidentialStageForEstablishedSlots(int currentStage, int establishedSlots) =>
            establishedSlots > 0 ? Math.Max(1, currentStage) : Math.Max(0, currentStage);

        public static bool ShouldExposeResidentialSlot(int slotIndex, int residentialStage, int establishedSlots) =>
            residentialStage >= 1 && slotIndex >= 1 && slotIndex <= establishedSlots;

        public static HashSet<(int x, int y)> BuildEstablishedSlotFootprint(IReadOnlyList<ResidentialSlotRecord> slots,
            int establishedSlots, CampCellRect entrance, CampCellRect chamber)
        {
            HashSet<(int x, int y)> result = new HashSet<(int x, int y)>();
            if (slots == null) return result;
            foreach (ResidentialSlotRecord slot in slots)
                if (slot.SlotIndex <= establishedSlots)
                    foreach ((int x, int y) cell in BuildSlotFootprint(slot, entrance, chamber)) result.Add(cell);
            return result;
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
            return requestedStage == expectedStage && requestedStage == 1 && exactExpectedFootprint;
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
                   zone == CampZoneKind.PermanentMemorial || zone == CampZoneKind.ResidentialEntrance ||
                   zone == CampZoneKind.IntroArrivalClearance || zone == CampZoneKind.NormalArrivalClearance ||
                   zone == CampZoneKind.CirculationClearance;
        }

        public static bool IsResidential(CampZoneKind zone)
        {
            return zone >= CampZoneKind.ResidentialStage1 && zone <= CampZoneKind.ResidentialStage5;
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
                if (IsResidential(a.Kind) && IsForbiddenResidentialOverlap(b.Kind) ||
                    IsResidential(b.Kind) && IsForbiddenResidentialOverlap(a.Kind))
                    issues.Add($"Residential zone overlaps forbidden {a.Kind}/{b.Kind} space.");
                if ((a.Kind == CampZoneKind.UnstableCollapse && IsStructuralPermanent(b.Kind)) ||
                    (b.Kind == CampZoneKind.UnstableCollapse && IsStructuralPermanent(a.Kind)))
                    issues.Add($"Collapse zone overlaps permanent {a.Kind}/{b.Kind} structure.");
            }
            return issues;
        }

        public static bool IsOrderedAndConnected(IReadOnlyList<CampCellRect> stages)
        {
            if (stages == null || stages.Count != 5) return false;
            for (int i = 1; i < stages.Count; i++)
                if (!stages[i - 1].Touches(stages[i])) return false;
            return true;
        }

        static bool IsForbiddenResidentialOverlap(CampZoneKind zone)
        {
            return IsStructuralPermanent(zone) || zone == CampZoneKind.UnstableCollapse ||
                   zone == CampZoneKind.IntroArrivalClearance || zone == CampZoneKind.NormalArrivalClearance ||
                   zone == CampZoneKind.CirculationClearance || zone == CampZoneKind.HomeCore;
        }

        static bool IsStructuralPermanent(CampZoneKind zone)
        {
            return zone == CampZoneKind.PermanentExit || zone == CampZoneKind.PermanentMemorial;
        }
    }
}
