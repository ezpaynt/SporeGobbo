using UnityEngine;

public static class CampRecoveryService
{
    public static void Recover(GameState state, GobboController player, bool healPlayer, bool healBuddies, bool refreshVisibleBuddies)
    {
        if (state == null) return;

        if (healPlayer)
            HealPlayerForCamp(state, player);

        if (healBuddies)
            HealAllBuddiesForCamp(state, refreshVisibleBuddies);
    }

    public static void HealPlayerForCamp(GameState state, GobboController player = null)
    {
        if (state == null) return;

        GobboUnitSaveData leader = state.GetLeader();
        if (leader != null) leader.health = leader.maxHealth;
        if (player != null) player.health = player.maxHealth;
    }

    public static void HealAllBuddiesForCamp(GameState state, bool refreshVisibleBuddies = false)
    {
        if (state == null || state.ownedGobbos == null) return;

        foreach (GobboUnitSaveData buddy in state.ownedGobbos)
        {
            if (buddy == null) continue;
            buddy.EnsureRuntimeDefaults();
            buddy.health = buddy.maxHealth;
            buddy.hasBeenHit = false;
        }

        if (!refreshVisibleBuddies) return;

        BuddyUnit[] visibleBuddies = Object.FindObjectsByType<BuddyUnit>(FindObjectsSortMode.None);
        foreach (BuddyUnit unit in visibleBuddies)
        {
            if (unit == null || unit.unitData == null) continue;
            unit.unitData.health = unit.unitData.maxHealth;
            unit.unitData.hasBeenHit = false;
            unit.ApplyVisuals();
        }
    }
}
