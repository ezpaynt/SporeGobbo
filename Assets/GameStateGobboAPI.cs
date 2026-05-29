using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stable doorway for systems that need gobbo save data.
/// Scene objects should ask GameState through this API instead of poking old player/buddy fields directly.
/// </summary>
public static class GameStateGobboAPI
{
    public static GobboUnitSaveData GetLeader(this GameState state)
    {
        if (state == null) return null;
        if (state.gobbo == null) state.gobbo = new GobboSaveData();
        state.gobbo.isLeader = true;
        state.gobbo.EnsureLeaderIdentity(state.gobbo.displayName);
        return state.gobbo;
    }

    public static void SetLeader(this GameState state, GobboUnitSaveData leader)
    {
        if (state == null) return;
        if (leader == null)
        {
            state.gobbo = new GobboSaveData();
            state.gobbo.EnsureLeaderIdentity("Gobbo");
            return;
        }

        leader.isLeader = true;
        leader.isDead = false;
        leader.EnsureRuntimeDefaults();
        state.gobbo = leader.ToLeaderSave();
        state.gobbo.isLeader = true;
        state.gobbo.EnsureLeaderIdentity(state.gobbo.displayName);
    }

    public static List<GobboUnitSaveData> GetAllGobbos(this GameState state, bool includeLeader = true, bool includeDead = false)
    {
        List<GobboUnitSaveData> result = new List<GobboUnitSaveData>();
        if (state == null) return result;
        state.EnsureRuntimeDefaults();

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
        state.EnsureRuntimeDefaults();

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
        state.EnsureRuntimeDefaults();
        return state.GetActiveSquadUnitsInternal();
    }

    public static List<GobboUnitSaveData> GetReserveGobboUnits(this GameState state)
    {
        if (state == null) return new List<GobboUnitSaveData>();
        state.EnsureRuntimeDefaults();
        return state.GetReserveGobboUnitsInternal();
    }

    public static bool PromoteBuddyToLeader(this GameState state, string buddyId)
    {
        if (state == null || string.IsNullOrWhiteSpace(buddyId)) return false;
        state.EnsureRuntimeDefaults();

        GobboUnitSaveData successor = state.FindOwnedGobbo(buddyId);
        if (successor == null) return false;

        successor.EnsureRuntimeDefaults();
        GobboUnitSaveData newLeader = successor.CloneUnit();
        newLeader.uniqueId = successor.uniqueId;
        newLeader.displayName = string.IsNullOrWhiteSpace(successor.displayName) ? "Gobbo" : successor.displayName;
        newLeader.isLeader = true;
        newLeader.isDead = false;
        newLeader.health = Mathf.Max(1, newLeader.maxHealth);
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
        state.EnsureRuntimeDefaults();

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
        state.EnsureRuntimeDefaults();
        return state.markedSuccessorId ?? "";
    }

    public static BuddyData GetMarkedSuccessor(this GameState state)
    {
        GobboUnitSaveData unit = state.GetMarkedSuccessorUnit();
        return unit != null ? unit.AsBuddyData() : null;
    }

    public static GobboUnitSaveData GetMarkedSuccessorUnit(this GameState state)
    {
        if (state == null) return null;
        state.EnsureRuntimeDefaults();
        return state.FindOwnedGobbo(state.markedSuccessorId);
    }

    public static GobboUnitSaveData CloneLeaderUnit(this GameState state)
    {
        GobboUnitSaveData leader = state.GetLeader();
        return leader != null ? leader.CloneUnit() : new GobboUnitSaveData { isLeader = true };
    }
}
