using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stable doorway for systems that need gobbo save data.
/// New systems should use these methods and GobboUnitSaveData instead of BuddyData.
/// </summary>
public static class GameStateGobboAPI
{
    public static GobboUnitSaveData GetLeader(this GameState state)
    {
        if (state == null) return null;

        if (state.gobbo == null) state.gobbo = new GobboSaveData();
        state.gobbo.isLeader = true;
        state.gobbo.isDead = false;
        state.gobbo.EnsureIdentity(string.IsNullOrWhiteSpace(state.gobbo.displayName) ? "Gobbo" : state.gobbo.displayName);
        state.gobbo.EnsureRuntimeDefaults();
        return state.gobbo;
    }

    public static void SetLeader(this GameState state, GobboUnitSaveData leader)
    {
        if (state == null) return;

        if (leader == null)
        {
            state.gobbo = new GobboSaveData();
            state.gobbo.EnsureIdentity("Gobbo");
            return;
        }

        leader.isLeader = true;
        leader.isDead = false;
        leader.EnsureIdentity(string.IsNullOrWhiteSpace(leader.displayName) ? "Gobbo" : leader.displayName);
        leader.EnsureRuntimeDefaults();

        state.gobbo = leader.ToLeaderSave();
        state.gobbo.isLeader = true;
        state.gobbo.isDead = false;
        state.gobbo.EnsureIdentity(state.gobbo.displayName);
        state.gobbo.EnsureRuntimeDefaults();
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

        if (state.ownedGobbos != null)
        {
            foreach (GobboUnitSaveData unit in state.ownedGobbos)
            {
                if (unit == null) continue;
                unit.EnsureRuntimeDefaults();
                if (!includeDead && unit.isDead) continue;
                result.Add(unit);
            }
        }

        return result;
    }

    public static GobboUnitSaveData FindGobboById(this GameState state, string gobboId)
    {
        if (state == null || string.IsNullOrWhiteSpace(gobboId)) return null;

        GobboUnitSaveData leader = state.GetLeader();
        if (leader != null)
        {
            leader.EnsureRuntimeDefaults();
            if (leader.uniqueId == gobboId) return leader;
        }

        return state.FindOwnedGobbo(gobboId);
    }

    public static bool HasGobbo(this GameState state, string gobboId)
    {
        return state.FindGobboById(gobboId) != null;
    }

    public static List<GobboUnitSaveData> GetActiveSquadUnits(this GameState state)
    {
        if (state == null) return new List<GobboUnitSaveData>();
        state.RepairRosterState();
        return state.GetActiveSquadUnitsInternal();
    }

    public static List<GobboUnitSaveData> GetReserveGobboUnits(this GameState state)
    {
        if (state == null) return new List<GobboUnitSaveData>();
        state.RepairRosterState();
        return state.GetReserveGobboUnitsInternal();
    }

    public static GobboUnitSaveData PullFirstReserveGobbo(this GameState state)
    {
        if (state == null) return null;
        List<GobboUnitSaveData> reserve = state.GetReserveGobboUnits();
        if (reserve.Count == 0) return null;

        GobboUnitSaveData unit = reserve[0];
        if (unit == null) return null;
        if (!state.MoveBuddyToActiveSquad(unit.uniqueId)) return null;
        return unit;
    }

    public static bool PromoteBuddyToLeader(this GameState state, string buddyId)
    {
        if (state == null || string.IsNullOrWhiteSpace(buddyId)) return false;

        state.RepairRosterState();
        GobboUnitSaveData successor = state.FindOwnedGobbo(buddyId);
        if (successor == null) return false;

        successor.EnsureRuntimeDefaults();

        GobboUnitSaveData newLeader = successor.CloneUnit();
        newLeader.isLeader = true;
        newLeader.isDead = false;
        newLeader.EnsureRuntimeDefaults();

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

    // Compatibility return for old callers. Prefer GetMarkedSuccessorUnit().
    public static BuddyData GetMarkedSuccessor(this GameState state)
    {
        GobboUnitSaveData unit = state.GetMarkedSuccessorUnit();
        return unit != null ? unit.AsBuddyData() : null;
    }

    public static GobboUnitSaveData GetMarkedSuccessorUnit(this GameState state)
    {
        if (state == null) return null;
        state.RepairRosterState();
        return state.FindOwnedGobbo(state.markedSuccessorId);
    }

    public static GobboUnitSaveData CloneLeaderUnit(this GameState state)
    {
        GobboUnitSaveData leader = state.GetLeader();
        return leader != null ? leader.CloneUnit() : new GobboUnitSaveData { isLeader = true };
    }

    public static void RegisterGobboFound(this GameState state, GobboUnitSaveData unit)
    {
        if (state == null) return;
        state.EnsureRuntimeDefaults();
        if (unit == null) return;

        unit.EnsureRuntimeDefaults();
        string label = GetUnitLabel(unit);
        if (!state.lastRun.newBuddyNames.Contains(label)) state.lastRun.newBuddyNames.Add(label);
        state.lastRun.buddiesFound = Mathf.Max(state.lastRun.buddiesFound, state.lastRun.newBuddyNames.Count);
    }

    private static string GetUnitLabel(GobboUnitSaveData unit)
    {
        if (unit == null) return "Unknown Gobbo";
        unit.EnsureRuntimeDefaults();
        return unit.displayName + " the " + unit.gobboType;
    }
}
