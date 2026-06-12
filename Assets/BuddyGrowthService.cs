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
        return buddy.pendingGrowthChoiceType != BuddyGrowthChoiceType.None || buddy.pendingEvolution;
    }

    public static BuddyGrowthChoiceType GetPendingGrowthChoiceType(GobboUnitSaveData buddy)
    {
        if (buddy == null) return BuddyGrowthChoiceType.None;
        buddy.EnsureRuntimeDefaults();
        if (buddy.pendingGrowthChoiceType != BuddyGrowthChoiceType.None) return buddy.pendingGrowthChoiceType;
        return buddy.pendingEvolution ? BuddyGrowthChoiceType.Evolution : BuddyGrowthChoiceType.None;
    }
}
