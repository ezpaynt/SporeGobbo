using System;
using System.Collections.Generic;

namespace SporeGobbo.CampLifecycle
{
    public enum ResidentialClearanceTier
    {
        Small,
        HypotheticalLarger
    }

    public readonly struct CampResidentialClearanceProfile
    {
        public readonly ResidentialClearanceTier Tier;
        public readonly double RadiusInCells;
        public readonly bool UseCompatibilityFootprint;

        public CampResidentialClearanceProfile(ResidentialClearanceTier tier, double radiusInCells,
            bool useCompatibilityFootprint = false)
        {
            Tier = tier;
            RadiusInCells = Math.Max(0d, radiusInCells);
            UseCompatibilityFootprint = useCompatibilityFootprint;
        }

        public static CampResidentialClearanceProfile CurrentBaby =>
            new CampResidentialClearanceProfile(ResidentialClearanceTier.Small,
                CampSpatialPolicy.ResidentialPocketRadiusInCells, true);
    }

    public sealed class CampResidentialSlotDefinition
    {
        public int GlobalSlotId { get; }
        public int DependencyGlobalSlotId { get; }
        public (int x, int y) Center { get; }
        public (int x, int y) RestCell { get; }
        public (int x, int y) Approach { get; }
        public IReadOnlyList<(int x, int y)> DigTargets { get; }
        public IReadOnlyList<(int x, int y)> ExcavationFootprint { get; }
        public IReadOnlyList<(int x, int y)> ConstructionRoute { get; internal set; }
        public string SleepingClusterId { get; internal set; } = "";
        public IReadOnlyList<(int x, int y)> AuthoredRouteSpine { get; internal set; } =
            Array.Empty<(int x, int y)>();
        public IReadOnlyList<(int x, int y)> ReservedExpansionEnvelope { get; internal set; } =
            Array.Empty<(int x, int y)>();
        public IReadOnlyList<(int x, int y)> ResidentialAuthorizationEnvelope { get; internal set; } =
            Array.Empty<(int x, int y)>();

        internal CampResidentialSlotDefinition(ResidentialSlotRecord slot,
            IReadOnlyList<(int x, int y)> footprint)
        {
            GlobalSlotId = slot.SlotIndex;
            DependencyGlobalSlotId = slot.DependencySlotIndex;
            Center = slot.Center;
            RestCell = slot.Center;
            Approach = slot.Approach;
            DigTargets = slot.DigTargets;
            ExcavationFootprint = footprint;
            ConstructionRoute = Array.Empty<(int x, int y)>();
        }

        public CampResidentialSlotDefinition(int globalSlotId, int dependencyGlobalSlotId,
            (int x, int y) center, (int x, int y) restCell, (int x, int y) approach,
            IReadOnlyList<(int x, int y)> digTargets,
            IReadOnlyList<(int x, int y)> excavationFootprint,
            IReadOnlyList<(int x, int y)> constructionRoute)
        {
            GlobalSlotId = globalSlotId;
            DependencyGlobalSlotId = dependencyGlobalSlotId;
            Center = center;
            RestCell = restCell;
            Approach = approach;
            DigTargets = digTargets ?? Array.Empty<(int x, int y)>();
            ExcavationFootprint = excavationFootprint ?? Array.Empty<(int x, int y)>();
            ConstructionRoute = constructionRoute ?? Array.Empty<(int x, int y)>();
            AuthoredRouteSpine = ConstructionRoute;
            ReservedExpansionEnvelope = ExcavationFootprint;
            ResidentialAuthorizationEnvelope = ExcavationFootprint;
        }

        public List<(int x, int y)> GetRequiredOpenCells(CampResidentialClearanceProfile profile)
        {
            if (profile.UseCompatibilityFootprint)
                return new List<(int x, int y)>(ExcavationFootprint);

            HashSet<(int x, int y)> reserved = new HashSet<(int x, int y)>(ReservedExpansionEnvelope);
            List<(int x, int y)> desired = GetDesiredOpenCells(profile);
            HashSet<(int x, int y)> required = new HashSet<(int x, int y)>();
            foreach ((int x, int y) cell in desired) if (reserved.Contains(cell)) required.Add(cell);
            return new List<(int x, int y)>(required);
        }

        public List<(int x, int y)> GetDesiredOpenCells(CampResidentialClearanceProfile profile)
        {
            if (profile.UseCompatibilityFootprint)
                return new List<(int x, int y)>(ExcavationFootprint);
            HashSet<(int x, int y)> domain = new HashSet<(int x, int y)>(ResidentialAuthorizationEnvelope);
            HashSet<(int x, int y)> desired = new HashSet<(int x, int y)>();
            double radiusSquared = profile.RadiusInCells * profile.RadiusInCells;
            int range = (int)Math.Ceiling(profile.RadiusInCells);
            foreach ((int x, int y) spine in AuthoredRouteSpine)
            for (int x = spine.x - range; x <= spine.x + range; x++)
            for (int y = spine.y - range; y <= spine.y + range; y++)
                if (domain.Contains((x, y)) &&
                    Math.Pow(x - spine.x, 2) + Math.Pow(y - spine.y, 2) <= radiusSquared)
                    desired.Add((x, y));
            return new List<(int x, int y)>(desired);
        }

        public List<(int x, int y)> GetClearanceDeficitCells(CampResidentialClearanceProfile profile)
        {
            HashSet<(int x, int y)> reserved = new HashSet<(int x, int y)>(ReservedExpansionEnvelope);
            List<(int x, int y)> result = new List<(int x, int y)>();
            foreach ((int x, int y) cell in GetDesiredOpenCells(profile))
                if (!reserved.Contains(cell)) result.Add(cell);
            return result;
        }

        public ResidentialSlotRecord ToRecord()
        {
            var targets = new (int x, int y)[DigTargets.Count];
            for (int i = 0; i < targets.Length; i++) targets[i] = DigTargets[i];
            return new ResidentialSlotRecord(GlobalSlotId, DependencyGlobalSlotId, Center, Approach, targets);
        }
    }

    public sealed class CampResidentialSleepingClusterDefinition
    {
        public string ClusterId { get; }
        public string DebugName { get; }
        public IReadOnlyList<int> MemberGlobalSlotIds { get; }
        public IReadOnlyList<(int x, int y)> SharedChamberEnvelope { get; }
        public IReadOnlyList<(int x, int y)> ReservedExpansionEnvelope { get; }

        public CampResidentialSleepingClusterDefinition(string clusterId, string debugName,
            IReadOnlyList<int> memberGlobalSlotIds,
            IReadOnlyList<(int x, int y)> sharedChamberEnvelope,
            IReadOnlyList<(int x, int y)> reservedExpansionEnvelope)
        {
            ClusterId = clusterId ?? "";
            DebugName = debugName ?? "";
            MemberGlobalSlotIds = memberGlobalSlotIds ?? Array.Empty<int>();
            SharedChamberEnvelope = sharedChamberEnvelope ?? Array.Empty<(int x, int y)>();
            ReservedExpansionEnvelope = reservedExpansionEnvelope ?? Array.Empty<(int x, int y)>();
        }
    }

    public sealed class CampResidentialRoomDefinition
    {
        public string RoomId { get; }
        public string DebugName { get; }
        public int ProgressionIndex { get; }
        public CampCellRect ProtectedEnvelope { get; }
        public CampCellRect Entrance { get; }
        public (int x, int y) ExteriorStagingCell { get; }
        public (int x, int y) FirstLockedEntranceCell { get; }
        public bool RequiresBreakthrough { get; }
        public IReadOnlyList<CampResidentialSlotDefinition> Slots { get; }
        public IReadOnlyList<CampResidentialSleepingClusterDefinition> SleepingClusters { get; }
        public int Capacity => Slots.Count;

        public CampResidentialRoomDefinition(string roomId, string debugName, int progressionIndex,
            CampCellRect protectedEnvelope, CampCellRect entrance,
            IReadOnlyList<CampResidentialSlotDefinition> slots, bool requiresBreakthrough = false,
            IReadOnlyList<CampResidentialSleepingClusterDefinition> sleepingClusters = null,
            (int x, int y)? exteriorStagingCell = null,
            (int x, int y)? firstLockedEntranceCell = null)
        {
            RoomId = roomId;
            DebugName = debugName;
            ProgressionIndex = progressionIndex;
            ProtectedEnvelope = protectedEnvelope;
            Entrance = entrance;
            Slots = slots ?? Array.Empty<CampResidentialSlotDefinition>();
            ExteriorStagingCell = exteriorStagingCell ??
                (Slots.Count > 0 ? Slots[0].Approach : (entrance.X - 1, entrance.Y));
            FirstLockedEntranceCell = firstLockedEntranceCell ??
                (Slots.Count > 0 && Slots[0].DigTargets.Count > 0
                    ? Slots[0].DigTargets[0] : (entrance.X, entrance.Y));
            RequiresBreakthrough = requiresBreakthrough;
            SleepingClusters = sleepingClusters ?? Array.Empty<CampResidentialSleepingClusterDefinition>();
        }
    }

    /// <summary>Build-available residential runtime authority.</summary>
    public sealed class CampResidentialCatalog
    {
        // Current production content, not an architectural maximum.
        public const int CurrentRuntimeCapacity = 10;
        public IReadOnlyList<CampResidentialRoomDefinition> Rooms { get; }
        public int TotalCapacity { get; }

        public CampResidentialCatalog(IReadOnlyList<CampResidentialRoomDefinition> rooms)
        {
            Rooms = rooms ?? Array.Empty<CampResidentialRoomDefinition>();
            int total = 0;
            HashSet<int> globalSlots = new HashSet<int>();
            foreach (CampResidentialRoomDefinition room in Rooms)
            {
                if (room == null) throw new ArgumentException("Residential catalogs cannot contain null rooms.");
                foreach (CampResidentialSlotDefinition slot in room.Slots)
                {
                    if (slot == null || slot.GlobalSlotId <= 0 || !globalSlots.Add(slot.GlobalSlotId))
                        throw new ArgumentException("Residential global slot IDs must be positive and unique.");
                    total++;
                }
            }
            for (int slotId = 1; slotId <= total; slotId++)
                if (!globalSlots.Contains(slotId))
                    throw new ArgumentException("Residential global slot IDs must be contiguous from 1 through capacity.");
            TotalCapacity = total;
        }

        public static CampResidentialCatalog CreateCurrent()
        {
            CampCellRect entrance = new CampCellRect(60, 35, 2, 3);
            List<ResidentialSlotRecord> records = BuildRoomOneRecords();
            List<CampResidentialSlotDefinition> definitions = new List<CampResidentialSlotDefinition>(records.Count);
            foreach (ResidentialSlotRecord record in records)
                definitions.Add(new CampResidentialSlotDefinition(record,
                    BuildFootprint(record)));

            foreach (CampResidentialSlotDefinition definition in definitions)
            {
                definition.ConstructionRoute = BuildConstructionRoute(
                    definition.GlobalSlotId, definitions);
                definition.AuthoredRouteSpine = BuildRouteSpine(definition);
                definition.ReservedExpansionEnvelope = BuildReservedExpansionEnvelope(definition);
                definition.SleepingClusterId = ClusterIdForRoomOneSlot(definition.GlobalSlotId);
            }

            List<(int x, int y)> residentialDomain = BuildResidentialDomain(definitions);
            foreach (CampResidentialSlotDefinition definition in definitions)
                definition.ResidentialAuthorizationEnvelope = residentialDomain;

            List<CampResidentialSleepingClusterDefinition> clusters = BuildRoomOneClusters(
                definitions);

            return new CampResidentialCatalog(new[]
            {
                new CampResidentialRoomDefinition("first-burrow", "First Burrow", 1,
                    new CampCellRect(55, 13, 63, 49),
                    entrance, definitions, false, clusters,
                    exteriorStagingCell: (58, 36), firstLockedEntranceCell: (60, 36))
            });
        }

        public CampResidentialRoomDefinition GetRoom(string roomId)
        {
            foreach (CampResidentialRoomDefinition room in Rooms)
                if (string.Equals(room.RoomId, roomId, StringComparison.Ordinal)) return room;
            return null;
        }

        public CampResidentialSlotDefinition GetSlot(int globalSlotId)
        {
            foreach (CampResidentialRoomDefinition room in Rooms)
            foreach (CampResidentialSlotDefinition slot in room.Slots)
                if (slot.GlobalSlotId == globalSlotId) return slot;
            return null;
        }

        public bool TryGetSlot(int globalSlotId, out CampResidentialSlotDefinition slot)
        {
            slot = GetSlot(globalSlotId);
            return slot != null;
        }

        public bool TryGetRoomForSlot(int globalSlotId, out CampResidentialRoomDefinition room)
        {
            foreach (CampResidentialRoomDefinition candidate in Rooms)
            foreach (CampResidentialSlotDefinition slot in candidate.Slots)
                if (slot.GlobalSlotId == globalSlotId)
                {
                    room = candidate;
                    return true;
                }
            room = null;
            return false;
        }

        public bool IsValidGlobalSlot(int globalSlotId) => TryGetSlot(globalSlotId, out _);

        public int NormalizeGlobalSlotId(int globalSlotId) =>
            IsValidGlobalSlot(globalSlotId) ? globalSlotId : 0;

        public List<CampResidentialSlotDefinition> GetEstablishedSlots(int establishedCount)
        {
            int count = Math.Min(Math.Max(0, establishedCount), TotalCapacity);
            List<CampResidentialSlotDefinition> result = new List<CampResidentialSlotDefinition>(count);
            for (int slotId = 1; slotId <= count; slotId++)
                if (TryGetSlot(slotId, out CampResidentialSlotDefinition slot)) result.Add(slot);
            return result;
        }

        public CampResidentialSleepingClusterDefinition GetSleepingCluster(string clusterId)
        {
            foreach (CampResidentialRoomDefinition room in Rooms)
            foreach (CampResidentialSleepingClusterDefinition cluster in room.SleepingClusters)
                if (string.Equals(cluster.ClusterId, clusterId, StringComparison.Ordinal)) return cluster;
            return null;
        }

        public HashSet<(int x, int y)> GetSharedRouteCells()
        {
            Dictionary<(int x, int y), int> useCounts = new Dictionary<(int x, int y), int>();
            foreach (CampResidentialRoomDefinition room in Rooms)
            foreach (CampResidentialSlotDefinition slot in room.Slots)
            foreach ((int x, int y) cell in new HashSet<(int x, int y)>(slot.AuthoredRouteSpine))
                useCounts[cell] = useCounts.TryGetValue(cell, out int count) ? count + 1 : 1;
            HashSet<(int x, int y)> result = new HashSet<(int x, int y)>();
            foreach (KeyValuePair<(int x, int y), int> pair in useCounts)
                if (pair.Value > 1) result.Add(pair.Key);
            return result;
        }

        public HashSet<(int x, int y)> GetResidentialAuthorizationCells()
        {
            HashSet<(int x, int y)> result = new HashSet<(int x, int y)>();
            foreach (CampResidentialRoomDefinition room in Rooms)
            foreach (CampResidentialSlotDefinition slot in room.Slots)
                foreach ((int x, int y) cell in slot.ResidentialAuthorizationEnvelope) result.Add(cell);
            return result;
        }

        public HashSet<(int x, int y)> GetCurrentBabyResidentialCells()
        {
            HashSet<(int x, int y)> result = new HashSet<(int x, int y)>();
            foreach (CampResidentialRoomDefinition room in Rooms)
            foreach (CampResidentialSlotDefinition slot in room.Slots)
                foreach ((int x, int y) cell in slot.ExcavationFootprint) result.Add(cell);
            return result;
        }

        public static HashSet<(int x, int y)> GetOptionalPlayerDigCells()
        {
            HashSet<(int x, int y)> result = new HashSet<(int x, int y)>();
            AddDilatedSpine(result, RasterizeWaypoints(new[]
            {
                (82,39), (84,41), (84,43)
            }), CampSpatialPolicy.ResidentialPocketRadiusInCells);
            AddDilatedSpine(result, RasterizeWaypoints(new[]
            {
                (99,23), (103,23), (107,19), (107,18)
            }), CampSpatialPolicy.ResidentialPocketRadiusInCells);
            return result;
        }

        public List<string> ValidateGeometry(CampResidentialClearanceProfile profile)
        {
            List<string> issues = new List<string>();
            foreach (CampResidentialRoomDefinition room in Rooms)
            {
                if (string.IsNullOrWhiteSpace(room.RoomId))
                    issues.Add("Residential rooms require a stable non-empty ID.");
                if (room.Slots.Count > 0)
                {
                    CampResidentialSlotDefinition first = room.Slots[0];
                    if (room.ExteriorStagingCell == room.FirstLockedEntranceCell)
                        issues.Add("Room " + room.RoomId + " must distinguish exterior staging from locked entrance dirt.");
                    if (first.Approach != room.ExteriorStagingCell)
                        issues.Add("Room " + room.RoomId + " first slot must approach from its exterior staging cell.");
                    if (!new HashSet<(int x, int y)>(first.ExcavationFootprint)
                            .Contains(room.FirstLockedEntranceCell))
                        issues.Add("Room " + room.RoomId + " first slot must excavate its locked entrance cell.");
                    if (first.DependencyGlobalSlotId != 0)
                        issues.Add("Room " + room.RoomId + " first slot cannot require another neighborhood for access.");
                }
            foreach (CampResidentialSlotDefinition slot in room.Slots)
            {
                CampResidentialSleepingClusterDefinition cluster = GetSleepingCluster(slot.SleepingClusterId);
                if (cluster == null) issues.Add("Slot " + slot.GlobalSlotId + " references missing cluster " + slot.SleepingClusterId + ".");
                else if (!new HashSet<int>(cluster.MemberGlobalSlotIds).Contains(slot.GlobalSlotId))
                    issues.Add("Cluster " + cluster.ClusterId + " omits member slot " + slot.GlobalSlotId + ".");
                if (slot.AuthoredRouteSpine.Count == 0) issues.Add("Slot " + slot.GlobalSlotId + " has no authored route spine.");
                if (!new HashSet<(int x, int y)>(slot.ReservedExpansionEnvelope).Contains(slot.RestCell))
                    issues.Add("Slot " + slot.GlobalSlotId + " rest cell is outside its reserved envelope.");
                HashSet<(int x, int y)> reserved = new HashSet<(int x, int y)>(slot.ReservedExpansionEnvelope);
                foreach ((int x, int y) cell in slot.GetRequiredOpenCells(profile))
                    if (!reserved.Contains(cell)) issues.Add("Slot " + slot.GlobalSlotId + " required cell escapes its reserved envelope.");
                foreach ((int x, int y) cell in slot.GetClearanceDeficitCells(profile))
                    issues.Add("Slot " + slot.GlobalSlotId + " reserved envelope is insufficient at (" +
                        cell.x + "," + cell.y + ").");
            }
            }
            return issues;
        }

        static List<ResidentialSlotRecord> BuildRoomOneRecords()
        {
            return new List<ResidentialSlotRecord>
            {
                // (58,36) is the first Camp-side cell with full Baby-body clearance
                // while the canonical entrance remains locked at (60,36).
                // residential route still begins at Entrance (60,36), which is dirt
                // until Slot 1 performs its first authorized Dig.
                BuildAuthoredSlot(1, 0, (69, 36), (58, 36),
                    (59,36), (60,36), (63,36), (66,35), (69,36)),
                BuildAuthoredSlot(2, 1, (70, 22), (69, 36),
                    (69,36), (67,34), (67,29), (70,26), (70,22)),
                BuildAuthoredSlot(3, 1, (71, 50), (69, 36),
                    (69,36), (68,37), (68,43), (70,45), (70,49), (71,50)),
                BuildAuthoredSlot(4, 3, (86, 56), (71, 50),
                    (71,50), (76,50), (80,54), (86,54), (86,56)),
                BuildAuthoredSlot(5, 4, (94, 48), (80, 54),
                    (80,54), (84,50), (90,50), (92,48), (94,48)),
                BuildAuthoredSlot(6, 1, (91, 36), (69, 36),
                    (69,36), (74,36), (77,39), (82,39), (85,36), (91,36)),
                BuildAuthoredSlot(7, 6, (99, 23), (91, 36),
                    (91,36), (94,33), (94,28), (99,23)),
                BuildAuthoredSlot(8, 6, (102, 42), (91, 36),
                    (91,36), (95,36), (99,40), (102,40), (102,42)),
                BuildAuthoredSlot(9, 2, (86, 18), (70, 22),
                    (70,22), (75,22), (79,18), (86,18)),
                BuildAuthoredSlot(10, 6, (112, 35), (91, 36),
                    (91,36), (97,36), (100,33), (106,33), (108,35), (112,35))
            };
        }

        static ResidentialSlotRecord BuildAuthoredSlot(int id, int dependency,
            (int x, int y) center, (int x, int y) approach, params (int x, int y)[] waypoints)
        {
            List<(int x, int y)> cells = RasterizeWaypoints(waypoints);
            if (cells.Count > 0 && cells[0] == approach) cells.RemoveAt(0);
            return new ResidentialSlotRecord(id, dependency, center, approach, cells.ToArray());
        }

        static List<(int x, int y)> RasterizeWaypoints(IReadOnlyList<(int x, int y)> waypoints)
        {
            List<(int x, int y)> result = new List<(int x, int y)>();
            if (waypoints == null || waypoints.Count == 0) return result;
            result.Add(waypoints[0]);
            for (int i = 1; i < waypoints.Count; i++)
            {
                int x = waypoints[i - 1].x;
                int y = waypoints[i - 1].y;
                int targetX = waypoints[i].x;
                int targetY = waypoints[i].y;
                int dx = Math.Abs(targetX - x);
                int sx = x < targetX ? 1 : -1;
                int dy = -Math.Abs(targetY - y);
                int sy = y < targetY ? 1 : -1;
                int error = dx + dy;
                while (x != targetX || y != targetY)
                {
                    int twiceError = 2 * error;
                    bool stepX = twiceError >= dy;
                    bool stepY = twiceError <= dx;
                    if (stepX) { error += dy; x += sx; }
                    // Preserve the approved polyline while spelling diagonal raster steps as
                    // two cardinal cells. Residential construction advances one cell at a time,
                    // and this keeps every intermediate Dig/advance target exact.
                    if (stepX && stepY && result[result.Count - 1] != (x, y)) result.Add((x, y));
                    if (stepY) { error += dx; y += sy; }
                    if (result[result.Count - 1] != (x, y)) result.Add((x, y));
                }
            }
            return result;
        }

        static ResidentialSlotRecord BuildConnectedSlot(int id, int dependency,
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
            return new ResidentialSlotRecord(id, dependency, center, approach, targets.ToArray());
        }

        static List<(int x, int y)> BuildFootprint(ResidentialSlotRecord slot)
        {
            HashSet<(int x, int y)> cells = new HashSet<(int x, int y)>();
            double radiusSquared = CampSpatialPolicy.ResidentialPocketRadiusInCells *
                                   CampSpatialPolicy.ResidentialPocketRadiusInCells;
            foreach ((int x, int y) target in slot.DigTargets)
            for (int x = target.x - 2; x <= target.x + 2; x++)
            for (int y = target.y - 2; y <= target.y + 2; y++)
                if (x >= 60 && Math.Pow(x - target.x, 2) + Math.Pow(y - target.y, 2) <= radiusSquared)
                    cells.Add((x, y));
            return new List<(int x, int y)>(cells);
        }

        static List<(int x, int y)> BuildConstructionRoute(int globalSlotId,
            IReadOnlyList<CampResidentialSlotDefinition> slots)
        {
            CampResidentialSlotDefinition target = null;
            foreach (CampResidentialSlotDefinition slot in slots)
                if (slot.GlobalSlotId == globalSlotId) target = slot;
            if (target == null || slots.Count == 0) return new List<(int x, int y)>();

            (int x, int y) exteriorApproach = slots[0].Approach;
            if (globalSlotId == 1) return new List<(int x, int y)> { exteriorApproach };

            // A later constructor retraces the declared dependency's authored excavation spine.
            // This is deterministic and avoids shortest-path tie breaking selecting tight boundary
            // doglegs that are cell-open but cannot accommodate the actual Baby BoxCollider.
            CampResidentialSlotDefinition dependency = null;
            foreach (CampResidentialSlotDefinition slot in slots)
                if (slot.GlobalSlotId == target.DependencyGlobalSlotId) dependency = slot;
            if (dependency != null)
            {
                List<(int x, int y)> dependencyRoute = BuildConstructionRoute(
                    dependency.GlobalSlotId, slots);
                if (dependencyRoute.Count > 0 && dependencyRoute[dependencyRoute.Count - 1] == target.Approach)
                    return dependencyRoute;
                foreach ((int x, int y) cell in dependency.DigTargets)
                {
                    if (dependencyRoute.Count == 0 || dependencyRoute[dependencyRoute.Count - 1] != cell)
                        dependencyRoute.Add(cell);
                    if (cell == target.Approach) return dependencyRoute;
                }
            }

            HashSet<(int x, int y)> open = new HashSet<(int x, int y)>();
            foreach (CampResidentialSlotDefinition slot in slots)
                if (slot.GlobalSlotId < globalSlotId)
                    foreach ((int x, int y) cell in slot.ExcavationFootprint) open.Add(cell);
            open.Add(exteriorApproach);
            open.Add(target.Approach);
            List<(int x, int y)> route = CampSpatialPolicy.BuildOpenCellRoute(
                exteriorApproach, target.Approach, open);
            if (route.Count > 0) route.Insert(0, exteriorApproach);
            return route;
        }

        static List<(int x, int y)> BuildRouteSpine(CampResidentialSlotDefinition slot)
        {
            List<(int x, int y)> result = new List<(int x, int y)>();
            foreach ((int x, int y) cell in slot.ConstructionRoute)
                if (result.Count == 0 || result[result.Count - 1] != cell) result.Add(cell);
            foreach ((int x, int y) cell in slot.DigTargets)
                if (result.Count == 0 || result[result.Count - 1] != cell) result.Add(cell);
            if (result.Count == 0 || result[result.Count - 1] != slot.RestCell) result.Add(slot.RestCell);
            return result;
        }

        static List<(int x, int y)> BuildExpansionEnvelope(IReadOnlyList<(int x, int y)> spine,
            double radiusInCells)
        {
            HashSet<(int x, int y)> result = new HashSet<(int x, int y)>();
            int range = (int)Math.Ceiling(radiusInCells);
            double radiusSquared = radiusInCells * radiusInCells;
            foreach ((int x, int y) center in spine)
            for (int x = center.x - range; x <= center.x + range; x++)
            for (int y = center.y - range; y <= center.y + range; y++)
                if (Math.Pow(x - center.x, 2) + Math.Pow(y - center.y, 2) <= radiusSquared)
                    result.Add((x, y));
            return new List<(int x, int y)>(result);
        }

        static List<(int x, int y)> BuildReservedExpansionEnvelope(CampResidentialSlotDefinition slot)
        {
            HashSet<(int x, int y)> result = new HashSet<(int x, int y)>();
            foreach ((int x, int y) cell in BuildExpansionEnvelope(
                         slot.AuthoredRouteSpine, 3d)) result.Add(cell);
            foreach ((int x, int y) cell in BuildExpansionEnvelope(
                         new[] { slot.RestCell }, 4.5d)) result.Add(cell);
            return new List<(int x, int y)>(result);
        }

        static List<(int x, int y)> BuildResidentialDomain(
            IReadOnlyList<CampResidentialSlotDefinition> definitions)
        {
            HashSet<(int x, int y)> result = new HashSet<(int x, int y)>();
            HashSet<(int x, int y)> baby = new HashSet<(int x, int y)>();
            foreach (CampResidentialSlotDefinition definition in definitions)
            {
                foreach ((int x, int y) cell in definition.ReservedExpansionEnvelope) result.Add(cell);
                foreach ((int x, int y) cell in definition.ExcavationFootprint) baby.Add(cell);
            }
            result.RemoveWhere(cell => cell.x < 60);
            result.ExceptWith(GetOptionalPlayerDigCells());
            result.UnionWith(baby);
            return new List<(int x, int y)>(result);
        }

        static void AddDilatedSpine(HashSet<(int x, int y)> result,
            IReadOnlyList<(int x, int y)> spine, double radiusInCells)
        {
            int range = (int)Math.Ceiling(radiusInCells);
            double radiusSquared = radiusInCells * radiusInCells;
            foreach ((int x, int y) center in spine)
            for (int x = center.x - range; x <= center.x + range; x++)
            for (int y = center.y - range; y <= center.y + range; y++)
                if (Math.Pow(x - center.x, 2) + Math.Pow(y - center.y, 2) <= radiusSquared)
                    result.Add((x, y));
        }

        static string ClusterIdForRoomOneSlot(int slotId)
        {
            if (slotId == 1) return "first-burrow-entry-sleeper";
            if (slotId == 2 || slotId == 5 || slotId == 9) return "first-burrow-lower-communal";
            if (slotId == 4 || slotId == 7) return "first-burrow-middle-pair";
            return "first-burrow-upper-communal";
        }

        static List<CampResidentialSleepingClusterDefinition> BuildRoomOneClusters(
            IReadOnlyList<CampResidentialSlotDefinition> slots)
        {
            string[] ids =
            {
                "first-burrow-entry-sleeper", "first-burrow-lower-communal",
                "first-burrow-middle-pair", "first-burrow-upper-communal"
            };
            string[] names =
            {
                "Entry Sleeper", "Lower Communal Nook", "Middle Pair Nook", "Upper Communal Nook"
            };
            List<CampResidentialSleepingClusterDefinition> result =
                new List<CampResidentialSleepingClusterDefinition>();
            for (int i = 0; i < ids.Length; i++)
            {
                List<int> members = new List<int>();
                HashSet<(int x, int y)> chamberCells = new HashSet<(int x, int y)>();
                HashSet<(int x, int y)> reserved = new HashSet<(int x, int y)>();
                foreach (CampResidentialSlotDefinition slot in slots)
                {
                    if (slot.SleepingClusterId != ids[i]) continue;
                    members.Add(slot.GlobalSlotId);
                    foreach ((int x, int y) cell in slot.ReservedExpansionEnvelope) reserved.Add(cell);
                    foreach ((int x, int y) cell in BuildExpansionEnvelope(
                                 new[] { slot.RestCell }, 1.5d)) chamberCells.Add(cell);
                }
                result.Add(new CampResidentialSleepingClusterDefinition(
                    ids[i], names[i], members, new List<(int x, int y)>(chamberCells),
                    new List<(int x, int y)>(reserved)));
            }
            return result;
        }
    }
}
