using System.Collections.Generic;

public static class BuddyGrowthService
{
    public static List<GobboUnitSaveData> GetPendingGrowthBuddies(GameState state)
    {
        List<GobboUnitSaveData> result = new List<GobboUnitSaveData>();
        if (state == null || state.ownedGobbos == null) return result;

        foreach (GobboUnitSaveData buddy in state.ownedGobbos)
        {
            if (HasPendingGrowth(buddy)) result.Add(buddy);
        }

        return result;
    }

    public static bool HasPendingGrowth(GameState state)
    {
        if (state == null || state.ownedGobbos == null) return false;

        foreach (GobboUnitSaveData buddy in state.ownedGobbos)
        {
            if (HasPendingGrowth(buddy)) return true;
        }

        return false;
    }

    public static bool HasPendingGrowth(GobboUnitSaveData buddy)
    {
        if (buddy == null) return false;
        buddy.EnsureRuntimeDefaults();
        return HasCurrentPendingGrowth(buddy) || GetQueuedGrowthCount(buddy) > 0;
    }

    public static bool HasCurrentPendingGrowth(GobboUnitSaveData buddy)
    {
        if (buddy == null) return false;
        buddy.EnsureRuntimeDefaults();
        return buddy.pendingGrowthChoiceType != BuddyGrowthChoiceType.None || buddy.pendingEvolution;
    }

    public static BuddyGrowthChoiceType GetPendingGrowthChoiceType(GobboUnitSaveData buddy)
    {
        if (buddy == null) return BuddyGrowthChoiceType.None;
        buddy.EnsureRuntimeDefaults();
        if (buddy.pendingGrowthChoiceType != BuddyGrowthChoiceType.None) return buddy.pendingGrowthChoiceType;
        if (buddy.pendingEvolution) return BuddyGrowthChoiceType.Evolution;
        if (buddy.pendingGrowthQueue != null && buddy.pendingGrowthQueue.Count > 0 && buddy.pendingGrowthQueue[0] != null)
            return buddy.pendingGrowthQueue[0].growthType;
        return BuddyGrowthChoiceType.None;
    }

    public static int GetQueuedGrowthCount(GobboUnitSaveData buddy)
    {
        if (buddy == null) return 0;
        buddy.EnsureRuntimeDefaults();
        return buddy.pendingGrowthQueue != null ? buddy.pendingGrowthQueue.Count : 0;
    }
}
