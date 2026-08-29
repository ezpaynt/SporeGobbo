using System.Collections.Generic;
using UnityEngine;
using SporeGobbo.CampLifecycle;

public readonly struct CampResidentialOccupancyRepair
{
    public readonly ResidentialOccupancyResolution Resolution;
    public readonly bool Changed;
    public CampResidentialOccupancyRepair(ResidentialOccupancyResolution resolution, bool changed)
    {
        Resolution = resolution;
        Changed = changed;
    }
}

/// <summary>Single authority for deriving and repairing living Camp residential occupancy.</summary>
public static class CampResidentialOccupancyResolver
{
    public static CampResidentialOccupancyRepair Repair(GameState gameState)
    {
        return Repair(gameState, CampResidentialCatalog.CurrentRuntimeCapacity);
    }

    public static CampResidentialOccupancyRepair Repair(GameState gameState, int residentialCapacity)
    {
        int established = gameState?.campTerrainState != null
            ? gameState.campTerrainState.residentialSlotsEstablished : 0;
        List<ResidentialOccupantRecord> records = new List<ResidentialOccupantRecord>();
        if (gameState?.ownedGobbos != null)
            foreach (GobboUnitSaveData gobbo in gameState.ownedGobbos)
                if (gobbo != null)
                    records.Add(new ResidentialOccupantRecord(gobbo.uniqueId, gobbo.campResidentialSlotId,
                        IsLivingBuddy(gobbo)));

        ResidentialOccupancyResolution resolution = CampSpatialPolicy.ResolveResidentialOccupancy(
            records, established, residentialCapacity);
        bool changed = false;
        if (gameState?.leader != null && gameState.leader.campResidentialSlotId != 0)
        {
            gameState.leader.campResidentialSlotId = 0;
            changed = true;
        }
        if (gameState?.ownedGobbos != null)
            foreach (GobboUnitSaveData gobbo in gameState.ownedGobbos)
            {
                if (gobbo == null || !resolution.Assignments.TryGetValue(gobbo.uniqueId, out int slot)) continue;
                if (gobbo.campResidentialSlotId == slot) continue;
                gobbo.campResidentialSlotId = slot;
                changed = true;
            }
        foreach (BuddyUnit live in Object.FindObjectsByType<BuddyUnit>(FindObjectsSortMode.None))
            if (live != null && live.unitData != null &&
                resolution.Assignments.TryGetValue(live.unitData.uniqueId, out int liveSlot))
                live.unitData.campResidentialSlotId = liveSlot;
        return new CampResidentialOccupancyRepair(resolution, changed);
    }

    public static bool AssignEstablishedSlot(GameState gameState, string gobboId, int slotId)
    {
        if (gameState?.campTerrainState == null ||
            !CanAssignSlot(gameState, gobboId, slotId,
                gameState.campTerrainState.residentialSlotsEstablished)) return false;
        GobboUnitSaveData gobbo = gameState.ownedGobbos?.Find(unit => unit != null && unit.uniqueId == gobboId);
        gobbo.campResidentialSlotId = slotId;
        return true;
    }

    public static bool CanAssignNextSlot(GameState gameState, string gobboId, int slotId)
    {
        return gameState?.campTerrainState != null &&
               slotId == gameState.campTerrainState.residentialSlotsEstablished + 1 &&
               CanAssignSlot(gameState, gobboId, slotId, slotId);
    }

    static bool CanAssignSlot(GameState gameState, string gobboId, int slotId, int maximumSlot)
    {
        if (gameState?.ownedGobbos == null || slotId < 1 || slotId > maximumSlot) return false;
        GobboUnitSaveData gobbo = gameState.ownedGobbos.Find(unit => unit != null && unit.uniqueId == gobboId);
        if (!IsLivingBuddy(gobbo)) return false;
        foreach (GobboUnitSaveData other in gameState.ownedGobbos)
            if (other != null && other != gobbo && IsLivingBuddy(other) &&
                other.campResidentialSlotId == slotId) return false;
        return true;
    }

    public static HashSet<int> GetOccupiedEstablishedSlots(GameState gameState)
    {
        HashSet<int> occupied = new HashSet<int>();
        int established = gameState?.campTerrainState != null
            ? gameState.campTerrainState.residentialSlotsEstablished : 0;
        if (gameState?.ownedGobbos == null) return occupied;
        foreach (GobboUnitSaveData gobbo in gameState.ownedGobbos)
            if (IsLivingBuddy(gobbo) && gobbo.campResidentialSlotId >= 1 &&
                gobbo.campResidentialSlotId <= established) occupied.Add(gobbo.campResidentialSlotId);
        return occupied;
    }

    public static Transform GetAssignedRestPoint(GobboUnitSaveData gobbo, CampResidentialPresentation presentation)
    {
        if (!IsLivingBuddy(gobbo) || presentation == null || gobbo.campResidentialSlotId <= 0) return null;
        return presentation.GetRestPoint(gobbo.campResidentialSlotId);
    }

    public static bool IsLivingBuddy(GobboUnitSaveData gobbo)
    {
        return gobbo != null && !gobbo.isLeader && !gobbo.isDead && gobbo.health > 0 &&
               !string.IsNullOrWhiteSpace(gobbo.uniqueId);
    }
}
