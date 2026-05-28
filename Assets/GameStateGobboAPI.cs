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

        if (includeLeader)
        {
            GobboUnitSaveData leader = state.GetLeader();
            if (leader != null && (includeDead || !leader.isDead))
                result.Add(leader);
        }

        if (state.ownedBuddies != null)
        {
            foreach (BuddyData buddy in state.ownedBuddies)
            {
                if (buddy == null) continue;
                buddy.EnsureId();
                buddy.EnsureRuntimeDefaults();
                if (!includeDead && buddy.isDead) continue;
                result.Add(buddy);
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

        BuddyData buddy = state.FindBuddy(gobboId);
        if (buddy != null) return buddy;

        return null;
    }

    public static bool HasGobbo(this GameState state, string gobboId)
    {
        return state.FindGobboById(gobboId) != null;
    }

    public static List<GobboUnitSaveData> GetActiveSquadUnits(this GameState state)
    {
        List<GobboUnitSaveData> result = new List<GobboUnitSaveData>();
        if (state == null) return result;

        state.RepairRosterState();
        foreach (string id in state.activeSquadIds)
        {
            if (string.IsNullOrWhiteSpace(id)) continue;
            GobboUnitSaveData unit = state.FindGobboById(id);
            if (unit != null && !unit.isLeader && !unit.isDead)
                result.Add(unit);
        }

        return result;
    }

    public static List<GobboUnitSaveData> GetReserveGobboUnits(this GameState state)
    {
        List<GobboUnitSaveData> result = new List<GobboUnitSaveData>();
        if (state == null) return result;

        state.RepairRosterState();
        HashSet<string> active = new HashSet<string>(state.activeSquadIds ?? new List<string>());

        if (state.ownedBuddies != null)
        {
            foreach (BuddyData buddy in state.ownedBuddies)
            {
                if (buddy == null) continue;
                buddy.EnsureId();
                buddy.EnsureRuntimeDefaults();
                if (buddy.isDead) continue;
                if (active.Contains(buddy.uniqueId)) continue;
                result.Add(buddy);
            }
        }

        return result;
    }

    public static bool PromoteBuddyToLeader(this GameState state, string buddyId)
    {
        if (state == null || string.IsNullOrWhiteSpace(buddyId)) return false;

        state.RepairRosterState();
        BuddyData successor = state.FindBuddy(buddyId);
        if (successor == null) return false;

        successor.EnsureId();
        successor.EnsureRuntimeDefaults();

        GobboUnitSaveData newLeader = successor.CloneUnit();
        newLeader.uniqueId = successor.uniqueId;
        newLeader.displayName = string.IsNullOrWhiteSpace(successor.displayName) ? successor.buddyName : successor.displayName;
        newLeader.gobboType = successor.gobboType;
        newLeader.ageStage = successor.ageStage;
        newLeader.isLeader = true;
        newLeader.isDead = false;
        newLeader.health = Mathf.Max(1, newLeader.maxHealth);
        newLeader.EnsureRuntimeDefaults();

        state.RemoveBuddy(successor.uniqueId);
        state.SetLeader(newLeader);

        if (state.markedSuccessorId == successor.uniqueId)
            state.markedSuccessorId = "";

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

        BuddyData buddy = state.FindBuddy(gobboId);
        state.markedSuccessorId = buddy != null ? buddy.uniqueId : "";
    }

    public static string GetMarkedSuccessorId(this GameState state)
    {
        if (state == null) return "";
        state.RepairRosterState();
        return state.markedSuccessorId ?? "";
    }

    public static BuddyData GetMarkedSuccessor(this GameState state)
    {
        if (state == null) return null;
        state.RepairRosterState();
        return state.FindBuddy(state.markedSuccessorId);
    }

    public static GobboUnitSaveData GetMarkedSuccessorUnit(this GameState state)
    {
        return state.GetMarkedSuccessor();
    }

    public static GobboUnitSaveData CloneLeaderUnit(this GameState state)
    {
        GobboUnitSaveData leader = state.GetLeader();
        return leader != null ? leader.CloneUnit() : new GobboUnitSaveData { isLeader = true };
    }
}
