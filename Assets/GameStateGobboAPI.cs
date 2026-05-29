using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stable doorway for systems that need gobbo save data.
/// New systems should use these methods and GobboUnitSaveData only.
/// </summary>
public static class GameStateGobboAPI
{
    public static GobboUnitSaveData GetLeader(this GameState state)
    {
        return state != null ? state.GetLeader() : null;
    }

    public static void SetLeader(this GameState state, GobboUnitSaveData leader)
    {
        if (state != null) state.SetLeader(leader);
    }

    public static List<GobboUnitSaveData> GetAllGobbos(this GameState state, bool includeLeader = true, bool includeDead = false)
    {
        List<GobboUnitSaveData> result = new List<GobboUnitSaveData>();
        if (state == null) return result;
        state.RepairRosterState();
        if (includeLeader)
        {
            GobboUnitSaveData leader = state.GetLeader();
            if (leader != null && (includeDead || !leader.isDead)) result.Add(leader);
        }
        foreach (GobboUnitSaveData unit in state.ownedGobbos)
        {
            if (unit == null) continue;
            unit.EnsureRuntimeDefaults();
            if (!includeDead && unit.isDead) continue;
            result.Add(unit);
        }
        return result;
    }

    public static GobboUnitSaveData FindGobboById(this GameState state, string gobboId)
    {
        if (state == null || string.IsNullOrWhiteSpace(gobboId)) return null;
        GobboUnitSaveData leader = state.GetLeader();
        if (leader != null && leader.uniqueId == gobboId) return leader;
        return state.FindOwnedGobbo(gobboId);
    }

    public static bool HasGobbo(this GameState state, string gobboId) => state.FindGobboById(gobboId) != null;
    public static List<GobboUnitSaveData> GetActiveSquadUnits(this GameState state) => state != null ? state.GetActiveSquadUnits() : new List<GobboUnitSaveData>();
    public static List<GobboUnitSaveData> GetReserveGobboUnits(this GameState state) => state != null ? state.GetReserveGobboUnits() : new List<GobboUnitSaveData>();
    public static GobboUnitSaveData PullFirstReserveGobbo(this GameState state) => state != null ? state.PullFirstReserveGobbo() : null;

    public static bool PromoteBuddyToLeader(this GameState state, string buddyId)
    {
        if (state == null || string.IsNullOrWhiteSpace(buddyId)) return false;
        state.RepairRosterState();
        GobboUnitSaveData successor = state.FindOwnedGobbo(buddyId);
        if (successor == null) return false;
        GobboUnitSaveData newLeader = successor.CloneUnit();
        newLeader.isLeader = true;
        newLeader.isDead = false;
        state.RemoveGobbo(successor.uniqueId);
        state.SetLeader(newLeader);
        if (state.markedSuccessorId == successor.uniqueId) state.markedSuccessorId = "";
        state.RepairRosterState();
        return true;
    }

    public static void SetMarkedSuccessorId(this GameState state, string gobboId)
    {
        if (state == null) return;
        state.RepairRosterState();
        if (string.IsNullOrWhiteSpace(gobboId))
        {
            state.markedSuccessorId = "";
            return;
        }
        GobboUnitSaveData unit = state.FindOwnedGobbo(gobboId);
        state.markedSuccessorId = unit != null ? unit.uniqueId : "";
    }

    public static string GetMarkedSuccessorId(this GameState state)
    {
        if (state == null) return "";
        state.RepairRosterState();
        return state.markedSuccessorId ?? "";
    }

    public static GobboUnitSaveData GetMarkedSuccessor(this GameState state) => state.GetMarkedSuccessorUnit();

    public static GobboUnitSaveData GetMarkedSuccessorUnit(this GameState state)
    {
        if (state == null) return null;
        state.RepairRosterState();
        return state.FindOwnedGobbo(state.markedSuccessorId);
    }

    public static GobboUnitSaveData CloneLeaderUnit(this GameState state)
    {
        GobboUnitSaveData leader = state != null ? state.GetLeader() : null;
        return leader != null ? leader.CloneUnit() : new GobboUnitSaveData { isLeader = true, displayName = "Gobbo" };
    }

    public static void RegisterGobboFound(this GameState state, GobboUnitSaveData unit)
    {
        if (state != null) state.RegisterGobboFound(unit);
    }
}
