using System.Collections.Generic;

public static class BuddyGrowthService
{
    public static List<GobboUnitSaveData> GetPendingGrowthBuddies(GameState state)
    {
        List<GobboUnitSaveData> result = new List<GobboUnitSaveData>();
        if (state == null || state.ownedGobbos == null) return result;

        foreach (GobboUnitSaveData buddy in state.ownedGobbos)
        {
            if (buddy == null) continue;
            buddy.EnsureRuntimeDefaults();
            if (buddy.pendingEvolution) result.Add(buddy);
        }

        return result;
    }

    public static bool HasPendingGrowth(GameState state)
    {
        if (state == null || state.ownedGobbos == null) return false;

        foreach (GobboUnitSaveData buddy in state.ownedGobbos)
        {
            if (buddy == null) continue;
            buddy.EnsureRuntimeDefaults();
            if (buddy.pendingEvolution) return true;
        }

        return false;
    }
}
