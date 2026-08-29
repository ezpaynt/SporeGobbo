using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class CampResidentialPlanLobe
{
    public CampCellCoordinate center = new CampCellCoordinate();
    [Min(1)] public int radiusX = 4;
    [Min(1)] public int radiusY = 4;
}

[Serializable]
public sealed class CampResidentialPlanNeighborhood
{
    public string neighborhoodId = "";
    public string displayName = "";
    [Min(1)] public int plannedCapacity = 1;
}

[Serializable]
public sealed class CampResidentialPlanRoom
{
    public string roomId = "";
    public string displayName = "";
    public string neighborhoodId = "";
    public string neighborhoodDisplayName = "";
    [Min(1)] public int progressionOrder = 1;
    [Min(1)] public int capacity = 10;
    public bool currentlyImplemented;
    [TextArea] public string relationshipToCamp = "";
    public BoundsInt protectedBounds = new BoundsInt();
    public CampCellCoordinate breakthroughEntrance = new CampCellCoordinate();
    [Min(1)] public int connectorHalfWidth = 1;
    public List<CampCellCoordinate> connectorWaypoints = new List<CampCellCoordinate>();
    public List<CampResidentialPlanLobe> chamberLobes = new List<CampResidentialPlanLobe>();
    public List<CampCellCoordinate> slotCenters = new List<CampCellCoordinate>();
    public List<CampCellCoordinate> futureActivityPoints = new List<CampCellCoordinate>();
    [Min(0.5f)] public float approximateSlotFootprintRadiusCells = 1.5f;
}

/// <summary>
/// Editor-authoring proposal for the maximum Camp colony. Runtime residential progression does not read this asset.
/// </summary>
[CreateAssetMenu(menuName = "Spore Gobbo/Camp Residential Master Plan", fileName = "CampResidentialMasterPlan")]
public sealed class CampResidentialMasterPlan : ScriptableObject
{
    public int planRevision = 1;
    public BoundsInt sourceCampBounds = new BoundsInt();
    public BoundsInt plannedCampBounds = new BoundsInt();
    public List<CampResidentialPlanNeighborhood> neighborhoods = new List<CampResidentialPlanNeighborhood>();
    public List<CampResidentialPlanRoom> rooms = new List<CampResidentialPlanRoom>();

    public int TotalCapacity
    {
        get
        {
            int total = 0;
            foreach (CampResidentialPlanRoom room in rooms) if (room != null) total += Mathf.Max(0, room.capacity);
            return total;
        }
    }

    public int TotalAuthoredSlots
    {
        get
        {
            int total = 0;
            foreach (CampResidentialPlanRoom room in rooms)
                if (room?.slotCenters != null) total += room.slotCenters.Count;
            return total;
        }
    }

    public List<string> ValidatePlan()
    {
        List<string> issues = new List<string>();
        HashSet<string> roomIds = new HashSet<string>(StringComparer.Ordinal);
        HashSet<int> progressionOrders = new HashSet<int>();
        HashSet<Vector2Int> slots = new HashSet<Vector2Int>();
        Dictionary<string, int> neighborhoodTotals = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (CampResidentialPlanNeighborhood neighborhood in neighborhoods)
        {
            if (neighborhood == null || string.IsNullOrWhiteSpace(neighborhood.neighborhoodId))
            {
                issues.Add("Neighborhood definitions require nonempty IDs.");
                continue;
            }
            if (!neighborhoodTotals.TryAdd(neighborhood.neighborhoodId, 0))
                issues.Add("Duplicate neighborhood ID: " + neighborhood.neighborhoodId);
        }
        foreach (CampResidentialPlanRoom room in rooms)
        {
            if (room == null) { issues.Add("Null residential room entry."); continue; }
            if (string.IsNullOrWhiteSpace(room.roomId) || !roomIds.Add(room.roomId))
                issues.Add("Residential room IDs must be nonempty and unique: " + room.roomId);
            if (string.IsNullOrWhiteSpace(room.neighborhoodId))
                issues.Add(room.roomId + " requires an authored neighborhood ID.");
            else if (!neighborhoodTotals.ContainsKey(room.neighborhoodId))
                issues.Add(room.roomId + " references an undefined neighborhood: " + room.neighborhoodId);
            else
                neighborhoodTotals[room.neighborhoodId] += room.capacity;
            if (room.progressionOrder < 1 || !progressionOrders.Add(room.progressionOrder))
                issues.Add(room.roomId + " progression order must be positive and unique: " + room.progressionOrder);
            if (room.slotCenters == null || room.slotCenters.Count != room.capacity)
                issues.Add(room.roomId + " capacity " + room.capacity + " does not match its " +
                    (room.slotCenters?.Count ?? 0) + " authored slots.");
            if (room.connectorWaypoints == null || room.connectorWaypoints.Count < 2)
                issues.Add(room.roomId + " requires an authored connector with at least two waypoints.");
            if (room.slotCenters == null) continue;
            foreach (CampCellCoordinate slot in room.slotCenters)
            {
                Vector2Int cell = new Vector2Int(slot.x, slot.y);
                if (!Contains(plannedCampBounds, cell)) issues.Add(room.roomId + " slot outside planned bounds: " + cell);
                if (!Contains(room.protectedBounds, cell)) issues.Add(room.roomId + " slot outside its protected bounds: " + cell);
                if (!slots.Add(cell)) issues.Add("Duplicate residential slot center: " + cell);
            }
        }
        foreach (CampResidentialPlanNeighborhood neighborhood in neighborhoods)
            if (neighborhood != null && neighborhoodTotals.TryGetValue(neighborhood.neighborhoodId, out int actual) &&
                actual != neighborhood.plannedCapacity)
                issues.Add(neighborhood.neighborhoodId + " planned capacity " + neighborhood.plannedCapacity +
                    " does not match authored burrow capacity " + actual + ".");
        if (TotalCapacity != TotalAuthoredSlots)
            issues.Add("Total capacity does not match the number of authored slot centers.");
        if (TotalCapacity != 50)
            issues.Add("Current master plan must provide exactly 50 residential slots.");
        return issues;
    }

    static bool Contains(BoundsInt bounds, Vector2Int cell) =>
        cell.x >= bounds.xMin && cell.x < bounds.xMax && cell.y >= bounds.yMin && cell.y < bounds.yMax;
}
