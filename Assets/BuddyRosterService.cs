using System.Collections.Generic;
using UnityEngine;

public static class BuddyRosterService
{
    public static void RepairRosterState(GameState state)
    {
        if (state == null) return;

        state.ownedGobbos ??= new List<GobboUnitSaveData>();
        state.activeSquadIds ??= new List<string>();
        state.ownedGobbos.RemoveAll(g => g == null);

        Dictionary<string, GobboUnitSaveData> unique = new Dictionary<string, GobboUnitSaveData>();
        List<GobboUnitSaveData> repaired = new List<GobboUnitSaveData>();
        foreach (GobboUnitSaveData unit in state.ownedGobbos)
        {
            if (unit == null) continue;
            unit.isLeader = false;
            unit.EnsureRuntimeDefaults();
            if (unique.ContainsKey(unit.uniqueId)) continue;
            unique.Add(unit.uniqueId, unit);
            repaired.Add(unit);
        }
        state.ownedGobbos = repaired;

        HashSet<string> ownedIds = new HashSet<string>();
        foreach (GobboUnitSaveData unit in state.ownedGobbos)
        {
            unit.isInActiveSquad = false;
            ownedIds.Add(unit.uniqueId);
        }

        List<string> repairedActive = new List<string>();
        foreach (string id in state.activeSquadIds)
        {
            if (string.IsNullOrWhiteSpace(id)) continue;
            if (!ownedIds.Contains(id)) continue;
            if (repairedActive.Contains(id)) continue;
            if (repairedActive.Count >= Mathf.Max(1, state.maxActiveSquad)) break;
            repairedActive.Add(id);
        }
        state.activeSquadIds = repairedActive;

        foreach (GobboUnitSaveData unit in state.ownedGobbos)
            unit.isInActiveSquad = state.activeSquadIds.Contains(unit.uniqueId);

        if (!string.IsNullOrWhiteSpace(state.markedSuccessorId) && !ownedIds.Contains(state.markedSuccessorId))
            state.markedSuccessorId = "";
    }

    public static GobboUnitSaveData FindOwnedGobbo(GameState state, string gobboId)
    {
        if (state == null || string.IsNullOrWhiteSpace(gobboId)) return null;
        RepairRosterStateNoFullRepair(state);
        return FindOwnedGobboRaw(state, gobboId);
    }

    public static GobboUnitSaveData FindOwnedGobboRaw(GameState state, string gobboId)
    {
        if (state == null || string.IsNullOrWhiteSpace(gobboId) || state.ownedGobbos == null) return null;
        foreach (GobboUnitSaveData unit in state.ownedGobbos)
        {
            if (unit == null) continue;
            unit.EnsureRuntimeDefaults();
            if (unit.uniqueId == gobboId) return unit;
        }
        return null;
    }

    public static void RepairRosterStateNoFullRepair(GameState state)
    {
        if (state == null) return;
        state.ownedGobbos ??= new List<GobboUnitSaveData>();
        state.activeSquadIds ??= new List<string>();
        state.ownedGobbos.RemoveAll(g => g == null);
        foreach (GobboUnitSaveData unit in state.ownedGobbos) unit.EnsureRuntimeDefaults();
    }

    public static List<GobboUnitSaveData> GetActiveSquadUnits(GameState state)
    {
        RepairRosterState(state);
        return GetActiveSquadUnitsInternal(state);
    }

    public static List<GobboUnitSaveData> GetActiveSquadUnitsInternal(GameState state)
    {
        List<GobboUnitSaveData> result = new List<GobboUnitSaveData>();
        if (state == null || state.activeSquadIds == null) return result;

        foreach (string id in state.activeSquadIds)
        {
            GobboUnitSaveData unit = FindOwnedGobboRaw(state, id);
            if (unit != null && !unit.isDead) result.Add(unit);
        }
        return result;
    }

    public static List<GobboUnitSaveData> GetReserveGobboUnits(GameState state)
    {
        RepairRosterState(state);
        return GetReserveGobboUnitsInternal(state);
    }

    public static List<GobboUnitSaveData> GetReserveGobboUnitsInternal(GameState state)
    {
        List<GobboUnitSaveData> result = new List<GobboUnitSaveData>();
        if (state == null || state.ownedGobbos == null || state.activeSquadIds == null) return result;

        foreach (GobboUnitSaveData unit in state.ownedGobbos)
        {
            if (unit == null) continue;
            unit.EnsureRuntimeDefaults();
            if (unit.isDead) continue;
            if (!state.activeSquadIds.Contains(unit.uniqueId)) result.Add(unit);
        }
        return result;
    }

    public static bool MoveBuddyToActiveSquad(GameState state, string buddyId)
    {
        if (state == null) return false;
        RepairRosterState(state);
        GobboUnitSaveData unit = FindOwnedGobboRaw(state, buddyId);
        if (unit == null) return false;
        if (state.activeSquadIds.Contains(buddyId)) return true;
        if (state.activeSquadIds.Count >= state.maxActiveSquad) return false;
        state.activeSquadIds.Add(buddyId);
        unit.isInActiveSquad = true;
        RepairRosterState(state);
        return true;
    }

    public static bool MoveBuddyToReserve(GameState state, string buddyId)
    {
        if (state == null) return false;
        RepairRosterState(state);
        GobboUnitSaveData unit = FindOwnedGobboRaw(state, buddyId);
        if (unit == null) return false;
        state.activeSquadIds.Remove(buddyId);
        unit.isInActiveSquad = false;
        RepairRosterState(state);
        return true;
    }

    public static bool SwapBuddies(GameState state, string activeBuddyId, string reserveBuddyId)
    {
        if (state == null) return false;
        RepairRosterState(state);
        GobboUnitSaveData activeUnit = FindOwnedGobboRaw(state, activeBuddyId);
        GobboUnitSaveData reserveUnit = FindOwnedGobboRaw(state, reserveBuddyId);
        if (activeUnit == null || reserveUnit == null) return false;
        int index = state.activeSquadIds.IndexOf(activeBuddyId);
        if (index < 0) return false;
        state.activeSquadIds[index] = reserveBuddyId;
        activeUnit.isInActiveSquad = false;
        reserveUnit.isInActiveSquad = true;
        RepairRosterState(state);
        return true;
    }

    public static GobboUnitSaveData PullFirstReserveGobbo(GameState state)
    {
        List<GobboUnitSaveData> reserve = GetReserveGobboUnits(state);
        if (reserve.Count == 0) return null;
        GobboUnitSaveData unit = reserve[0];
        return MoveBuddyToActiveSquad(state, unit.uniqueId) ? unit : null;
    }

    public static bool HasReserveBuddy(GameState state)
    {
        return GetReserveGobboUnitsInternal(state).Count > 0;
    }

    public static List<string> GetOwnedGobboIds(GameState state)
    {
        RepairRosterState(state);
        List<string> result = new List<string>();
        if (state == null || state.ownedGobbos == null) return result;

        foreach (GobboUnitSaveData unit in state.ownedGobbos)
        {
            if (unit == null) continue;
            unit.EnsureRuntimeDefaults();
            result.Add(unit.uniqueId);
        }
        return result;
    }
}
